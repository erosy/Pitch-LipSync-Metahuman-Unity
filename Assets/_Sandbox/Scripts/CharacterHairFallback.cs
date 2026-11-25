using System.Collections.Generic;
using System.Linq;
using Unity.DemoTeam.Hair;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Scripting.APIUpdating;
using Sirenix.OdinInspector;

/// <summary>Switch scalp and facial grooms to character-specific mesh/card replacements.</summary>
[MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp", sourceClassName: "EthanWebHairFallback")]
[DisallowMultipleComponent]
[DefaultExecutionOrder(-10000)]
public sealed class CharacterHairFallback : MonoBehaviour
{
    [Tooltip("Empty discovers the Hair Instances below this character root.")]
    [SerializeField] HairInstance[] strandHair;
    [SerializeField] CharacterHairStartupWarmup startupWarmup;
    [FormerlySerializedAs("bakedHair"), SerializeField] GameObject fallbackRoot;
    [SerializeField, Tooltip("Optional additional replacements, such as eyebrows and eyelashes.")]
    GameObject[] additionalFallbackRoots;

    public bool IsUsingFallback { get; private set; }
    // Both WebGL2 and WebGPU demos use the authored mesh/card replacements.
    public bool RequiresFallback => Application.platform == RuntimePlatform.WebGLPlayer
        || !SystemInfo.supportsComputeShaders;

    HairInstance[] ResolveStrands() => strandHair != null && strandHair.Length > 0
        ? strandHair.Where(h => h).Distinct().ToArray() : GetComponentsInChildren<HairInstance>(true);
    GameObject[] ResolveFallbacks() => new[] { fallbackRoot }
        .Concat(additionalFallbackRoots ?? new GameObject[0]).Where(g => g).Distinct().ToArray();

    public void ValidateConfiguration(List<string> errors)
    {
        var replacements = ResolveFallbacks();
        if (replacements.Length == 0) errors.Add(name + ": assign a mesh/card hair fallback before switching off strands.");
        foreach (var replacement in replacements)
        {
            if (replacement == gameObject || !replacement.transform.IsChildOf(transform))
                errors.Add(name + ": fallback objects must be children of this character root.");
            foreach (var hair in ResolveStrands())
                if (replacement.transform == hair.transform || replacement.transform.IsChildOf(hair.transform))
                    errors.Add(name + ": a fallback cannot be inside a strand object that will be deactivated.");
        }
        foreach (var hair in ResolveStrands())
            if (hair.gameObject == gameObject || !hair.transform.IsChildOf(transform))
                errors.Add(name + ": strand objects must be children of this character root.");
    }

    void Awake()
    {
        if (RequiresFallback) ActivateFallback();
    }

    [ContextMenu("Preview fallback (Play Mode)")]
    [Button("Preview Mesh Hair"), EnableIf("@UnityEngine.Application.isPlaying")]
    public void ActivateFallback()
    {
        if (!Application.isPlaying || IsUsingFallback) return;
        ApplyFallback();
    }

    [Button("Restore Strand Hair"), EnableIf("@UnityEngine.Application.isPlaying")]
    public void RestoreStrands()
    {
        if (!Application.isPlaying || RequiresFallback || !IsUsingFallback) return;
        foreach (var replacement in ResolveFallbacks()) replacement.SetActive(false);
        IsUsingFallback = false;
        foreach (var hair in ResolveStrands()) hair.gameObject.SetActive(true);
        if (startupWarmup) startupWarmup.enabled = true;
    }

    bool ApplyFallback()
    {
        var errors = new List<string>();
        ValidateConfiguration(errors);
        if (errors.Count > 0)
        {
            Debug.LogError(string.Join("\n", errors), this);
            return false;
        }
        // This runs before the late startup warmup can hide or reset unsupported strands.
        if (startupWarmup) startupWarmup.enabled = false;
        foreach (var hair in ResolveStrands()) hair.gameObject.SetActive(false);
        foreach (var replacement in ResolveFallbacks()) replacement.SetActive(true);
        IsUsingFallback = true;
        Debug.Log(name + ": using mesh/card hair fallback.", this);
        return true;
    }
}
