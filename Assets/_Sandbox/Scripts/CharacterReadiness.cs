using System.Collections.Generic;
using Sandbox.CharacterSystem;
using Unity.DemoTeam.Hair;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

/// <summary>Bridges optional hair startup/fallback to the reusable character flow system.</summary>
[MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp", sourceClassName: "EthanFlowReadiness")]
[DisallowMultipleComponent]
public sealed class CharacterReadiness : MonoBehaviour, ICharacterReadiness
{
    [SerializeField] CharacterHairStartupWarmup startupWarmup;
    [SerializeField] CharacterHairFallback hairFallback;

    bool HasActiveStrands()
    {
        foreach (var hair in GetComponentsInChildren<HairInstance>(true))
            if (hair.isActiveAndEnabled) return true;
        return false;
    }

    public bool IsReady => (hairFallback && hairFallback.IsUsingFallback)
        || (!(hairFallback && hairFallback.RequiresFallback) &&
            (startupWarmup ? startupWarmup.IsReady : !HasActiveStrands()));

    void Reset()
    {
        startupWarmup = GetComponent<CharacterHairStartupWarmup>();
        hairFallback = GetComponent<CharacterHairFallback>();
    }

    public void ValidateConfiguration(List<string> errors)
    {
        if (hairFallback && hairFallback.IsUsingFallback) return;
        if (hairFallback && hairFallback.RequiresFallback) hairFallback.ValidateConfiguration(errors);
        if (!startupWarmup && HasActiveStrands()) errors.Add("Character Readiness: assign Startup Warmup for active strands.");
        if (startupWarmup && !startupWarmup.isActiveAndEnabled && !startupWarmup.IsReady
            && (!hairFallback || !hairFallback.IsUsingFallback))
            errors.Add("Character Readiness: enable Startup Warmup or activate the hair fallback.");
    }
}
