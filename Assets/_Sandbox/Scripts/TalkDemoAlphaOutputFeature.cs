using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.Universal;

// Attached only to the dedicated Talk Demo renderer. Do not change effect settings.
public sealed class TalkDemoAlphaOutputFeature : ScriptableRendererFeature
{
    public override void Create() { }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        var camera = renderingData.cameraData.camera;
        if (!camera || camera.cameraType != CameraType.Game || !camera.CompareTag("MainCamera")
            || camera.gameObject.scene.name != "Talk Demo") return;

        // A transparent clear alone is insufficient: TAA, UberPost and FinalBlit
        // select their alpha-preserving variants from this per-camera flag.
        if (!GraphicsFormatUtility.HasAlphaChannel(renderingData.cameraData.cameraTargetDescriptor.graphicsFormat))
        {
            Debug.LogError("Talk Demo transparency requires an RGBA camera target.", camera);
            return;
        }
        renderingData.cameraData.isAlphaOutputEnabled = true;
    }
}
