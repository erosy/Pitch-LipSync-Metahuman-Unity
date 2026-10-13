using Unity.DemoTeam.Hair;
using UnityEngine;

/// <summary>Use the existing skinned cards when the graphics API cannot simulate strands.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-10000)]
public sealed class EthanWebHairFallback : MonoBehaviour
{
    [SerializeField] HairInstance[] strandHair;
    [SerializeField] EthanHairStartupWarmup startupWarmup;
    [SerializeField] GameObject bakedHair;

    public bool IsUsingFallback { get; private set; }

    void Awake()
    {
        if (!SystemInfo.supportsComputeShaders) ActivateFallback();
    }

    [ContextMenu("Preview fallback (Play Mode)")]
    public void ActivateFallback()
    {
        if (!Application.isPlaying || IsUsingFallback) return;
        // This runs before the late startup warmup can hide or reset unsupported strands.
        if (startupWarmup) startupWarmup.enabled = false;
        if (strandHair != null)
            foreach (var hair in strandHair)
                if (hair) hair.gameObject.SetActive(false);

        if (!bakedHair)
        {
            Debug.LogError("Ethan needs a baked hair mesh assigned for this graphics API.", this);
            return;
        }

        bakedHair.SetActive(true);
        IsUsingFallback = true;
        Debug.Log("Ethan: using baked hair because strand simulation is unavailable or fallback preview was requested.", this);
    }
}
