using System.Collections.Generic;
using Sandbox.CharacterSystem;
using UnityEngine;

/// <summary>Bridges Ethan's rendering setup to the reusable character flow system.</summary>
[DisallowMultipleComponent]
public sealed class EthanFlowReadiness : MonoBehaviour, ICharacterReadiness
{
    [SerializeField] EthanHairStartupWarmup startupWarmup;
    [SerializeField] EthanWebHairFallback hairFallback;

    public bool IsReady => (hairFallback && hairFallback.IsUsingFallback) || (startupWarmup && startupWarmup.IsReady);

    void Reset()
    {
        startupWarmup = GetComponent<EthanHairStartupWarmup>();
        hairFallback = GetComponent<EthanWebHairFallback>();
    }

    public void ValidateConfiguration(List<string> errors)
    {
        if (!startupWarmup) errors.Add("Ethan Readiness: assign Startup Warmup.");
        if (startupWarmup && !startupWarmup.isActiveAndEnabled && !startupWarmup.IsReady
            && (!hairFallback || !hairFallback.IsUsingFallback))
            errors.Add("Ethan Readiness: enable Startup Warmup or activate the hair fallback.");
    }
}
