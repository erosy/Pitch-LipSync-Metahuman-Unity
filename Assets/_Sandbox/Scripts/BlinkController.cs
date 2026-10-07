using UnityEngine;

/// <summary>Random synchronized blinks, independent of speech and mouth animation.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]
[AddComponentMenu("Character/Blink Controller")]
public class BlinkController : MonoBehaviour
{
    const string LeftShape = "eyeBlinkLeft";
    const string RightShape = "eyeBlinkRight";
    const float MinimumDuration = 0.001f;

    [SerializeField] SkinnedMeshRenderer face;
    [Tooltip("Automatic blinking yields while this debugger holds a preview on the same face.")]
    [SerializeField] BlendShapeDebugger blendShapeDebugger;
    [Tooltip("Minimum and maximum seconds to wait after a complete blink sequence.")]
    [SerializeField] Vector2 blinkInterval = new Vector2(2f, 6f);
    [SerializeField, Min(MinimumDuration)] float closingDuration = 0.06f;
    [SerializeField, Min(MinimumDuration)] float closedHoldDuration = 0.03f;
    [SerializeField, Min(MinimumDuration)] float openingDuration = 0.12f;
    [SerializeField, Range(0f, 100f)] float closureWeight = 100f;
    [Tooltip("Chance of exactly one additional blink per sequence. 0.15 means 15%.")]
    [SerializeField, Range(0f, 1f)] float doubleBlinkProbability = 0.15f;
    [Tooltip("Minimum and maximum seconds between the two blinks.")]
    [SerializeField] Vector2 doubleBlinkGap = new Vector2(0.12f, 0.25f);

    enum Phase { Waiting, Closing, Holding, Opening, DoubleGap }

    SkinnedMeshRenderer _cachedFace;
    Mesh _cachedMesh;
    int _leftIndex = -1;
    int _rightIndex = -1;
    bool _ready;
    bool _cacheDirty = true;
    bool _warned;
    bool _previewWasHolding;
    bool _secondBlinkPending;
    Phase _phase;
    float _elapsed;
    float _phaseDuration;

    void OnEnable()
    {
        if (!Application.isPlaying) return;
        ValidateSettings();
        _warned = false;
        _previewWasHolding = false;
        _cacheDirty = true;
        RebuildCache();
    }

    void OnDisable()
    {
        RestoreOpenEyes();
        _ready = false;
        _cacheDirty = true;
        _previewWasHolding = false;
        _secondBlinkPending = false;
    }

    void OnValidate()
    {
        ValidateSettings();
        _cacheDirty = true;
    }

    void ValidateSettings()
    {
        blinkInterval.x = Mathf.Max(0f, blinkInterval.x);
        blinkInterval.y = Mathf.Max(blinkInterval.x, blinkInterval.y);
        doubleBlinkGap.x = Mathf.Max(0f, doubleBlinkGap.x);
        doubleBlinkGap.y = Mathf.Max(doubleBlinkGap.x, doubleBlinkGap.y);
        closingDuration = Mathf.Max(MinimumDuration, closingDuration);
        closedHoldDuration = Mathf.Max(MinimumDuration, closedHoldDuration);
        openingDuration = Mathf.Max(MinimumDuration, openingDuration);
        closureWeight = Mathf.Clamp(closureWeight, 0f, 100f);
        doubleBlinkProbability = Mathf.Clamp01(doubleBlinkProbability);
    }

    bool PreviewOwnsFace(SkinnedMeshRenderer renderer)
    {
        return blendShapeDebugger && blendShapeDebugger.Face == renderer &&
            blendShapeDebugger.IsHolding;
    }

    void RebuildCache()
    {
        RestoreOpenEyes();
        _cachedFace = face;
        _cachedMesh = face ? face.sharedMesh : null;
        _leftIndex = _cachedMesh ? _cachedMesh.GetBlendShapeIndex(LeftShape) : -1;
        _rightIndex = _cachedMesh ? _cachedMesh.GetBlendShapeIndex(RightShape) : -1;
        _cacheDirty = false;
        _ready = _cachedFace && _cachedMesh && _leftIndex >= 0 && _rightIndex >= 0;

        if (!_ready)
        {
            if (!_warned)
            {
                Debug.LogWarning($"{name}: blinking needs a Face renderer with '{LeftShape}' and '{RightShape}'. Assign a valid Face to BlinkController.", this);
                _warned = true;
            }
            return;
        }

        ScheduleNextSequence();
        RestoreOpenEyes();
    }

    void LateUpdate()
    {
        if (!Application.isPlaying) return;
        if (_cacheDirty || _cachedFace != face || _cachedMesh != (face ? face.sharedMesh : null))
            RebuildCache();
        if (!_ready) return;

        if (PreviewOwnsFace(_cachedFace))
        {
            _previewWasHolding = true;
            _secondBlinkPending = false;
            return;
        }

        if (_previewWasHolding)
        {
            _previewWasHolding = false;
            ScheduleNextSequence();
            WriteWeight(0f);
            return;
        }

        Advance(Time.deltaTime);
        float progress = _phaseDuration > 0f ? Mathf.Clamp01(_elapsed / _phaseDuration) : 1f;
        float weight = 0f;
        if (_phase == Phase.Closing) weight = Mathf.SmoothStep(0f, closureWeight, progress);
        else if (_phase == Phase.Holding) weight = closureWeight;
        else if (_phase == Phase.Opening) weight = Mathf.SmoothStep(closureWeight, 0f, progress);
        WriteWeight(weight);
    }

    void Advance(float deltaTime)
    {
        // Carry frame time across phases, including a zero-length wait or double gap.
        // Bound work after extreme hitches rather than replaying many unseen sequences.
        for (int transitions = 0; deltaTime > 0f && transitions < 64; transitions++)
        {
            float remaining = Mathf.Max(0f, _phaseDuration - _elapsed);
            if (deltaTime < remaining)
            {
                _elapsed += deltaTime;
                return;
            }

            deltaTime -= remaining;
            NextPhase();
        }
    }

    void NextPhase()
    {
        switch (_phase)
        {
            case Phase.Waiting:
                _secondBlinkPending = doubleBlinkProbability > 0f &&
                    (doubleBlinkProbability >= 1f || Random.value < doubleBlinkProbability);
                EnterPhase(Phase.Closing, closingDuration);
                break;
            case Phase.Closing:
                EnterPhase(Phase.Holding, closedHoldDuration);
                break;
            case Phase.Holding:
                EnterPhase(Phase.Opening, openingDuration);
                break;
            case Phase.Opening:
                if (_secondBlinkPending)
                {
                    _secondBlinkPending = false;
                    EnterPhase(Phase.DoubleGap, Random.Range(doubleBlinkGap.x, doubleBlinkGap.y));
                }
                else ScheduleNextSequence();
                break;
            case Phase.DoubleGap:
                EnterPhase(Phase.Closing, closingDuration);
                break;
        }
    }

    void ScheduleNextSequence()
    {
        _secondBlinkPending = false;
        EnterPhase(Phase.Waiting, Random.Range(blinkInterval.x, blinkInterval.y));
    }

    void EnterPhase(Phase phase, float duration)
    {
        _phase = phase;
        _elapsed = 0f;
        _phaseDuration = duration;
    }

    void RestoreOpenEyes()
    {
        if (_ready && !PreviewOwnsFace(_cachedFace)) WriteWeight(0f);
    }

    void WriteWeight(float weight)
    {
        if (!_cachedFace || !_cachedMesh || _cachedFace.sharedMesh != _cachedMesh) return;
        _cachedFace.SetBlendShapeWeight(_leftIndex, weight);
        _cachedFace.SetBlendShapeWeight(_rightIndex, weight);
    }
}
