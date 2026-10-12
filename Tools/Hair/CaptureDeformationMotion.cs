if (!UnityEditor.EditorApplication.isPlaying) throw new System.Exception("Enter Play Mode first.");
var hair = UnityEngine.Object.FindObjectsByType<Unity.DemoTeam.Hair.HairInstance>(UnityEngine.FindObjectsInactive.Exclude).First();
var animator = hair.GetComponentInParent<UnityEngine.Animator>();
var originalSpeed = animator.speed;
var originalEnabled = animator.enabled;
animator.speed = 0; animator.enabled = false;
var head = hair.transform.parent;
var originalRotation = head.localRotation;
var frame = 0;
var debug = UnityEngine.Rendering.Universal.UniversalRenderPipelineDebugDisplaySettings.Instance.renderingSettings;
debug.fullScreenDebugMode = UnityEngine.Rendering.Universal.DebugFullScreenMode.MotionVector;
debug.fullScreenDebugModeOutputSizeScreenPercent = 100;
var camera = UnityEngine.Camera.main;
var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static;
var capture = System.AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Unity.Pipeline.Editor.Commands.Capture.CaptureCommands")).First(t => t != null);
System.Action<UnityEngine.Rendering.ScriptableRenderContext, UnityEngine.Camera> handler = null;
handler = (context, current) => {
    if (current != camera) return;
    frame++;
    if (frame == 1) head.localRotation = originalRotation * UnityEngine.Quaternion.Euler(0, 8, 0);
    if (frame == 8) head.localRotation = originalRotation;
    if (frame == 10 || frame == 12 || frame == 16) {
        var size = ((int, int))capture.GetMethod("GetGameViewTargetSize", flags).Invoke(null, null);
        var png = (byte[])capture.GetMethod("EncodeScreenToPng", flags).Invoke(null, new object[] {size.Item1, size.Item2});
        System.IO.File.WriteAllBytes("Docs/HairRendering/MotionVectorsDeformation_" + frame + ".png", png);
    }
    if (frame == 18) {
        UnityEngine.Rendering.RenderPipelineManager.endCameraRendering -= handler;
        head.localRotation = originalRotation; animator.speed = originalSpeed; animator.enabled = originalEnabled;
        debug.fullScreenDebugMode = UnityEngine.Rendering.Universal.DebugFullScreenMode.None;
    }
};
UnityEngine.Rendering.RenderPipelineManager.endCameraRendering += handler;
return "Capturing residual strand motion with the head fixed after a temporary 8-degree nudge.";
