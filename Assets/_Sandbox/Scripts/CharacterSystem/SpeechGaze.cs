using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sandbox.CharacterSystem
{
    /// <summary>Applies audience gaze during speech and optional idle tracking; never writes eyelids or visemes.</summary>
    [Serializable]
    public sealed class SpeechGaze
    {
        [Tooltip("Master switch for audience gaze. Idle tracking also requires Look At Player During Idle.")]
        public bool enabled = true;
        [Tooltip("Also look toward Target (or the main camera) while idle. Eye-direction expressions and occasional gaze motions take priority during idle.")]
        public bool lookAtPlayerDuringIdle;
        [Tooltip("Use the mesh's cameraEyeLookDownLeft/Right shapes for downward camera tracking. These rotate only the eyeballs; authored expressions and darts keep the original shapes.")]
        public bool eyeOnlyDownwardGaze;
        [Tooltip("Optional audience target. Otherwise use Camera.main each frame.")]
        public Transform target;
        [Tooltip("Optional head frame (+Z forward, +Y up). Automatically resolves the Face's Head bone when empty.")]
        public Transform head;
        [Min(.01f)] public float blendDuration = .15f;
        [Tooltip("Estimated eye turn at blendshape weight 100. Tune to match the mesh.")]
        [Range(1, 60)] public float horizontalEyeRange = 30;
        [Tooltip("Estimated eye turn at blendshape weight 100. Tune to match the mesh.")]
        [Range(1, 45)] public float verticalEyeRange = 20;
        public bool assistHead = true;
        [Range(0, 45)] public float maximumHeadCorrection = 20;

        internal static readonly string[] EyeShapes = {
            "eyeLookOutLeft", "eyeLookInRight", "eyeLookInLeft", "eyeLookOutRight",
            "eyeLookUpLeft", "eyeLookUpRight", "eyeLookDownLeft", "eyeLookDownRight"
        };
        internal static readonly string[] CameraDownShapes = { "cameraEyeLookDownLeft", "cameraEyeLookDownRight" };
        [NonSerialized] float blend;
        [NonSerialized] float horizontalBlend, verticalBlend;
        [NonSerialized] Transform boundHead;
        [NonSerialized] SkinnedMeshRenderer boundFace;
        [NonSerialized] Mesh boundMesh;
        [NonSerialized] Quaternion originalHead, appliedHead;
        [NonSerialized] bool movedHead;
        [NonSerialized] bool wroteEyes;
        [NonSerialized] int[] indices;
        [NonSerialized] float[] originalEyes, appliedEyes;

        internal void Validate(List<string> errors, Mesh mesh = null)
        {
            if (eyeOnlyDownwardGaze && mesh)
                foreach (var shape in CameraDownShapes)
                    if (mesh.GetBlendShapeIndex(shape) < 0) errors.Add("Speech Gaze: eye-only downward gaze requires mesh shape '" + shape + "'.");
            if (!Finite(blendDuration) || blendDuration < .01f
                || !Finite(horizontalEyeRange) || horizontalEyeRange < 1 || horizontalEyeRange > 60
                || !Finite(verticalEyeRange) || verticalEyeRange < 1 || verticalEyeRange > 45
                || !Finite(maximumHeadCorrection) || maximumHeadCorrection < 0 || maximumHeadCorrection > 45)
                errors.Add("Speech Gaze: use finite blend duration >= .01, eye ranges 1–60 / 1–45, and head correction 0–45 degrees.");
        }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        internal void Restore()
        {
            // Animation/expression writers may already have supplied a new pose. Preserve it.
            if (movedHead && boundHead && Quaternion.Angle(boundHead.localRotation, appliedHead) < .001f)
                boundHead.localRotation = originalHead;
            movedHead = false;
            if (wroteEyes && boundFace && boundFace.sharedMesh == boundMesh && indices != null)
                for (int i = 0; i < indices.Length; i++)
                    if (indices[i] >= 0 && Mathf.Abs(boundFace.GetBlendShapeWeight(indices[i]) - appliedEyes[i]) < .001f)
                        boundFace.SetBlendShapeWeight(indices[i], originalEyes[i]);
            wroteEyes = false;
        }

        internal void Release()
        {
            Restore(); blend = horizontalBlend = verticalBlend = 0; boundFace = null; boundMesh = null; boundHead = null;
        }

        internal void Apply(SkinnedMeshRenderer face, Transform character, bool speaking, float delta,
            HashSet<string> reserved, bool idle = false, HashSet<string> expressionShapes = null,
            IReadOnlyDictionary<string, float> basePose = null, IReadOnlyDictionary<string, float> motionTargets = null,
            IReadOnlyDictionary<string, float> motionInfluences = null)
        {
            Restore();
            if (!face || !face.sharedMesh || !character) { Release(); return; }
            if (boundFace != face || boundMesh != face.sharedMesh)
            {
                blend = horizontalBlend = verticalBlend = 0; boundHead = null;
                boundFace = face; boundMesh = face.sharedMesh;
            }
            var frame = head;
            if (!frame)
            {
                var bones = face.bones;
                if (boundHead && Array.IndexOf(bones, boundHead) < 0) boundHead = null;
                if (!boundHead)
                    foreach (var bone in bones)
                        if (bone && string.Equals(bone.name, "Head", StringComparison.OrdinalIgnoreCase))
                        { boundHead = bone; break; }
                frame = boundHead;
            }
            // Without a head frame we cannot compensate for animation safely.
            if (!frame) { Release(); return; }
            var audience = target;
            if (!audience)
            {
                var camera = Camera.main;
                if (camera) audience = camera.transform;
            }
            bool validTarget = audience && (audience.position - frame.position).sqrMagnitude > .000001f;
            float duration = Finite(blendDuration) ? Mathf.Max(.01f, blendDuration) : .15f;
            bool idleGaze = !speaking && idle && lookAtPlayerDuringIdle;
            bool horizontalExpression = idleGaze && HasExpression(expressionShapes, 0, 4);
            bool verticalExpression = idleGaze && HasExpression(expressionShapes, 4, 8);
            float horizontalMotion = idleGaze ? MotionInfluence(motionInfluences, 0, 4) : 0;
            float verticalMotion = idleGaze ? MotionInfluence(motionInfluences, 4, 8) : 0;
            blend = Mathf.MoveTowards(blend, enabled && (speaking || idleGaze) && validTarget ? 1 : 0,
                Mathf.Max(0, delta) / duration);
            horizontalBlend = horizontalExpression ? 0 : Mathf.MoveTowards(horizontalBlend, blend, Mathf.Max(0, delta) / duration);
            verticalBlend = verticalExpression ? 0 : Mathf.MoveTowards(verticalBlend, blend, Mathf.Max(0, delta) / duration);
            if (blend <= 0) return;

            float yaw = 0, pitch = 0;
            if (validTarget) Angles(frame, audience.position, out yaw, out pitch);
            float yawRange = Finite(horizontalEyeRange) ? Mathf.Clamp(horizontalEyeRange, 1, 60) : 30;
            float pitchRange = Finite(verticalEyeRange) ? Mathf.Clamp(verticalEyeRange, 1, 45) : 20;
            // During an authored eye pose or dart, preserve the animated head as well.
            bool idleOverride = horizontalExpression || verticalExpression || horizontalMotion > 0 || verticalMotion > 0;
            if (assistHead && !idleOverride && validTarget && maximumHeadCorrection > 0 && Finite(maximumHeadCorrection))
            {
                float excessYaw = yaw - Mathf.Clamp(yaw, -yawRange, yawRange);
                float excessPitch = pitch - Mathf.Clamp(pitch, -pitchRange, pitchRange);
                var correction = Quaternion.Euler(-excessPitch, excessYaw, 0);
                correction = Quaternion.RotateTowards(Quaternion.identity, correction, Mathf.Min(45, maximumHeadCorrection));
                boundHead = frame; originalHead = frame.localRotation;
                frame.rotation = frame.rotation * Quaternion.Slerp(Quaternion.identity, correction, blend);
                appliedHead = frame.localRotation; movedHead = true;
                Angles(frame, audience.position, out yaw, out pitch);
            }
            float right = Mathf.Clamp(yaw / yawRange, -1, 1);
            float up = Mathf.Clamp(pitch / pitchRange, -1, 1);
            if (indices == null)
            {
                indices = new int[EyeShapes.Length + CameraDownShapes.Length]; originalEyes = new float[indices.Length]; appliedEyes = new float[indices.Length];
            }
            bool splitDownward = eyeOnlyDownwardGaze;
            for (int side = 0; side < CameraDownShapes.Length; side++)
            {
                int slot = EyeShapes.Length + side;
                indices[slot] = boundMesh.GetBlendShapeIndex(CameraDownShapes[side]);
                if (indices[slot] < 0 || (reserved != null && reserved.Contains(CameraDownShapes[side]))) splitDownward = false;
            }
            float leftCameraDown = 0, rightCameraDown = 0;
            for (int i = 0; i < EyeShapes.Length; i++)
            {
                indices[i] = reserved != null && reserved.Contains(EyeShapes[i]) ? -1 : boundMesh.GetBlendShapeIndex(EyeShapes[i]);
                if (indices[i] < 0) continue;
                originalEyes[i] = face.GetBlendShapeWeight(indices[i]);
                if (i < 4 ? horizontalExpression : verticalExpression) { indices[i] = -1; continue; }
                float desired = i < 2 ? -right : i < 4 ? right : i < 6 ? up : -up;
                float baseline = idleGaze && basePose != null && basePose.TryGetValue(EyeShapes[i], out var baseWeight)
                    ? baseWeight : originalEyes[i];
                float eyeBlend = idleGaze ? (i < 4 ? horizontalBlend : verticalBlend) : blend;
                appliedEyes[i] = Mathf.Lerp(baseline, Mathf.Max(0, desired) * 100, eyeBlend);
                bool splitShape = splitDownward && i >= 6;
                float cameraDown = splitShape ? Mathf.Max(0, desired) * 100 * eyeBlend : 0;
                if (splitShape) appliedEyes[i] = baseline * (1 - eyeBlend);
                if (idleGaze)
                {
                    // Compose darts over camera gaze, including the opposite direction's release.
                    // Starting from the expression base avoids applying a dart's easing twice.
                    float influence = i < 4 ? horizontalMotion : verticalMotion;
                    float targetWeight = motionTargets != null && motionTargets.TryGetValue(EyeShapes[i], out var motionWeight) ? FacialMath.Weight(motionWeight) : 0;
                    appliedEyes[i] = Mathf.Lerp(appliedEyes[i], targetWeight, influence);
                    cameraDown *= 1 - influence;
                }
                if (i == 6) leftCameraDown = cameraDown;
                if (i == 7) rightCameraDown = cameraDown;
                face.SetBlendShapeWeight(indices[i], appliedEyes[i]);
            }
            for (int side = 0; side < CameraDownShapes.Length; side++)
            {
                int slot = EyeShapes.Length + side;
                if (!splitDownward) { indices[slot] = -1; continue; }
                originalEyes[slot] = face.GetBlendShapeWeight(indices[slot]);
                appliedEyes[slot] = side == 0 ? leftCameraDown : rightCameraDown;
                face.SetBlendShapeWeight(indices[slot], appliedEyes[slot]);
            }
            wroteEyes = true;
        }

        static bool HasExpression(HashSet<string> shapes, int start, int end)
        {
            if (shapes == null) return false;
            for (int i = start; i < end; i++) if (shapes.Contains(EyeShapes[i])) return true;
            return false;
        }

        static float MotionInfluence(IReadOnlyDictionary<string, float> influences, int start, int end)
        {
            float result = 0;
            if (influences != null)
                for (int i = start; i < end; i++)
                    if (influences.TryGetValue(EyeShapes[i], out var value)) result = Mathf.Max(result, Mathf.Clamp01(value));
            return result;
        }

        static void Angles(Transform frame, Vector3 targetPosition, out float yaw, out float pitch)
        {
            var direction = Quaternion.Inverse(frame.rotation) * (targetPosition - frame.position);
            yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            pitch = Mathf.Atan2(direction.y, new Vector2(direction.x, direction.z).magnitude) * Mathf.Rad2Deg;
        }
    }
}
