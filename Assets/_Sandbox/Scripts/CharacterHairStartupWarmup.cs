using System.Collections;
using System.Linq;
using Unity.DemoTeam.Hair;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Scripting.APIUpdating;

/// <summary>Initialize the current animated pose and reset hair on every activation.</summary>
[MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp", sourceClassName: "EthanHairStartupWarmup")]
[DisallowMultipleComponent]
[DefaultExecutionOrder(10000)]
public sealed class CharacterHairStartupWarmup : MonoBehaviour
{
    [SerializeField, Tooltip("Optional Animator; otherwise discover one below this character root.")]
    Animator targetAnimator;
    [SerializeField, Tooltip("Optional explicit groom list. Empty discovers all grooms below this root.")]
    HairInstance[] strandHair;
    [SerializeField, Min(1)] int poseWarmupFrames = 8;
    [SerializeField, Min(1)] int framesAfterReset = 3;

    public bool IsReady { get; private set; }
    public int ResetCount { get; private set; }

    Renderer[] renderers;
    bool[] rendererStates;
    Animator animator;
    AnimatorCullingMode previousCulling;
    HairInstance[] hairs;
    HairInstance.GroupSettings[] defaults;
    HairInstance.GroupSettings[][] overrides;
    Coroutine warmup;
    bool hidden;

    void OnEnable()
    {
        IsReady = false;
        if (Application.isPlaying && !SystemInfo.supportsComputeShaders)
        {
            IsReady = true;
            return;
        }
        if (Application.isPlaying && warmup == null)
            warmup = StartCoroutine(InitializeHidden());
    }

    IEnumerator InitializeHidden()
    {
        IsReady = false;
        animator = targetAnimator ? targetAnimator : GetComponentInChildren<Animator>(true);
        if (animator)
        {
            previousCulling = animator.cullingMode;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }
        renderers = GetComponentsInChildren<Renderer>(true);
        rendererStates = new bool[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            rendererStates[i] = renderers[i].enabled;
            renderers[i].enabled = false;
        }
        hairs = strandHair != null && strandHair.Length > 0
            ? strandHair.Where(h => h).Distinct().ToArray()
            : GetComponentsInChildren<HairInstance>(true);
        defaults = new HairInstance.GroupSettings[hairs.Length];
        overrides = new HairInstance.GroupSettings[hairs.Length][];
        for (int i = 0; i < hairs.Length; i++)
        {
            var hair = hairs[i];
            defaults[i] = hair.strandGroupDefaults;
            overrides[i] = hair.strandGroupSettings == null ? null : (HairInstance.GroupSettings[])hair.strandGroupSettings.Clone();
            // Hair uses procedural indirect draws, so disabling its MeshRenderer alone
            // is insufficient. Disable the rendering mode while simulation keeps running.
            hair.strandGroupDefaults.settingsRendering.renderer = HairSim.SettingsRendering.Renderer.Disabled;
            hair.strandGroupDefaults.settingsRendering.rendererShadows = ShadowCastingMode.Off;
            if (hair.strandGroupSettings != null)
                for (int j = 0; j < hair.strandGroupSettings.Length; j++)
                    if (hair.strandGroupSettings[j].settingsRenderingToggle)
                    {
                        hair.strandGroupSettings[j].settingsRendering.renderer = HairSim.SettingsRendering.Renderer.Disabled;
                        hair.strandGroupSettings[j].settingsRendering.rendererShadows = ShadowCastingMode.Off;
                    }
        }
        hidden = true;
        for (int frame = 0; frame < Mathf.Max(1, poseWarmupFrames); frame++) yield return null;
        foreach (var hair in hairs)
            if (hair && hair.isActiveAndEnabled) hair.ResetSimulationState();
        ResetCount++;
        for (int frame = 0; frame < Mathf.Max(1, framesAfterReset); frame++) yield return null;
        RestoreVisibility();
        IsReady = true;
        warmup = null;
    }

    void LateUpdate()
    {
        if (!hidden) return;
        foreach (var renderer in renderers)
            if (renderer) renderer.enabled = false;
    }

    void OnDisable()
    {
        IsReady = false;
        if (warmup != null) { StopCoroutine(warmup); warmup = null; }
        RestoreVisibility();
    }

    void RestoreVisibility()
    {
        if (!hidden) return;
        for (int i = 0; i < hairs.Length; i++)
            if (hairs[i])
            {
                // Restore rendering only; do not overwrite live physics or geometry edits.
                hairs[i].strandGroupDefaults.settingsRendering = defaults[i].settingsRendering;
                if (overrides[i] != null && hairs[i].strandGroupSettings != null)
                    for (int j = 0; j < Mathf.Min(overrides[i].Length, hairs[i].strandGroupSettings.Length); j++)
                        hairs[i].strandGroupSettings[j].settingsRendering = overrides[i][j].settingsRendering;
            }
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i]) renderers[i].enabled = rendererStates[i];
        if (animator) animator.cullingMode = previousCulling;
        hidden = false;
    }
}
