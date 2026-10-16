using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sandbox.CharacterSystem
{
    /// <summary>Applies speech gaze after expression/animation output; never writes eyelids or visemes.</summary>
    [Serializable]
    public sealed class SpeechGaze
    {
        public bool enabled = true;
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
        [NonSerialized] float blend;
        [NonSerialized] Transform boundHead;
        [NonSerialized] SkinnedMeshRenderer boundFace;
        [NonSerialized] Mesh boundMesh;
        [NonSerialized] Quaternion originalHead, appliedHead;
        [NonSerialized] bool movedHead;
        [NonSerialized] bool wroteEyes;
        [NonSerialized] int[] indices;
        [NonSerialized] float[] originalEyes, appliedEyes;

        internal void Validate(List<string> errors)
        {
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
            Restore(); blend = 0; boundFace = null; boundMesh = null; boundHead = null;
        }

        internal void Apply(SkinnedMeshRenderer face, Transform character, bool speaking, float delta,
            HashSet<string> reserved)
        {
            Restore();
            if (!face || !face.sharedMesh || !character) { Release(); return; }
            if (boundFace != face || boundMesh != face.sharedMesh)
            {
                blend = 0; boundHead = null;
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
            blend = Mathf.MoveTowards(blend, enabled && speaking && validTarget ? 1 : 0,
                Mathf.Max(0, delta) / duration);
            if (blend <= 0) return;

            float yaw = 0, pitch = 0;
            if (validTarget) Angles(frame, audience.position, out yaw, out pitch);
            float yawRange = Finite(horizontalEyeRange) ? Mathf.Clamp(horizontalEyeRange, 1, 60) : 30;
            float pitchRange = Finite(verticalEyeRange) ? Mathf.Clamp(verticalEyeRange, 1, 45) : 20;
            if (assistHead && validTarget && maximumHeadCorrection > 0 && Finite(maximumHeadCorrection))
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
                indices = new int[EyeShapes.Length]; originalEyes = new float[indices.Length]; appliedEyes = new float[indices.Length];
            }
            for (int i = 0; i < indices.Length; i++)
            {
                indices[i] = reserved != null && reserved.Contains(EyeShapes[i]) ? -1 : boundMesh.GetBlendShapeIndex(EyeShapes[i]);
                if (indices[i] < 0) continue;
                originalEyes[i] = face.GetBlendShapeWeight(indices[i]);
                float desired = i < 2 ? -right : i < 4 ? right : i < 6 ? up : -up;
                appliedEyes[i] = Mathf.Lerp(originalEyes[i], Mathf.Max(0, desired) * 100, blend);
                face.SetBlendShapeWeight(indices[i], appliedEyes[i]);
            }
            wroteEyes = true;
        }

        static void Angles(Transform frame, Vector3 targetPosition, out float yaw, out float pitch)
        {
            var direction = Quaternion.Inverse(frame.rotation) * (targetPosition - frame.position);
            yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            pitch = Mathf.Atan2(direction.y, new Vector2(direction.x, direction.z).magnitude) * Mathf.Rad2Deg;
        }
    }
}
