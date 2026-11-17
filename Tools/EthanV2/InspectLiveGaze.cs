using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Sandbox.CharacterSystem;

public static class InspectLiveGaze
{
    public static object Run()
    {
        var camera = Camera.main;
        return new {
            playing = Application.isPlaying,
            camera = camera ? new { camera.name, position = camera.transform.position.ToString("G8"), forward = camera.transform.forward.ToString("G8") } : null,
            characters = UnityEngine.Object.FindObjectsByType<Character>(FindObjectsInactive.Include)
                .Where(c => c.name.Contains("EthanV2")).Select(c => {
                    var module = c.GetComponentInChildren<ExpressionModule>(true);
                    var gaze = (SpeechGaze)typeof(ExpressionModule).GetField("speechGaze", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(module);
                    var head = gaze.head; var target = gaze.target ? gaze.target : camera ? camera.transform : null;
                    var direction = head && target ? Quaternion.Inverse(head.rotation) * (target.position - head.position) : Vector3.zero;
                    return new { c.name, c.gameObject.activeInHierarchy, c.IsInitialized,
                        rootForward = c.transform.forward.ToString("G8"),
                        eyeBones = c.GetComponentsInChildren<Transform>(true).Where(t => t.name == "FACIAL_L_Eye" || t.name == "FACIAL_R_Eye" || t.name == "eye_l" || t.name == "eye_r")
                            .Select(t => new { t.name, position = t.position.ToString("G8") }).ToArray(),
                        activity = c.CurrentActivity ? c.CurrentActivity.name : null,
                        idle = c.CurrentActivity && c.CurrentActivity.IsIdle,
                        expression = module.CurrentDefinition ? module.CurrentDefinition.name : null,
                        defaultExpression = c.DefaultIdle && c.DefaultIdle.Expression ? c.DefaultIdle.Expression.name : null,
                        defaultTargets = c.DefaultIdle && c.DefaultIdle.Expression ? c.DefaultIdle.Expression.Targets.Select(t => new { t.Shape, t.Weight }).ToArray() : null,
                        module.IsPreviewing,
                        gaze.enabled, gaze.lookAtPlayerDuringIdle, gaze.horizontalEyeRange, gaze.verticalEyeRange,
                        target = target ? target.name : null, frame = head ? head.name : null,
                        headPosition = head ? head.position.ToString("G8") : null, headForward = head ? head.forward.ToString("G8") : null,
                        targetYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg,
                        targetPitch = Mathf.Atan2(direction.y, new Vector2(direction.x, direction.z).magnitude) * Mathf.Rad2Deg,
                        eyes = Enumerable.Range(0, module.Face.sharedMesh.blendShapeCount).Where(i => {
                            var n = module.Face.sharedMesh.GetBlendShapeName(i);
                            return n.StartsWith("eye") || n.StartsWith("cheekSquint") || n.StartsWith("browDown") || n.StartsWith("emo_"); })
                            .Select(i => new { name = module.Face.sharedMesh.GetBlendShapeName(i), weight = module.Face.GetBlendShapeWeight(i) }).ToArray()
                    };
                }).ToArray()
        };
    }
}
