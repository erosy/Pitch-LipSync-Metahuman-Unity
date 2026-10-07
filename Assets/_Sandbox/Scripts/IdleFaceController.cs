using System.Collections.Generic;
using UnityEngine;

/// <summary>Independent mouth and gaze gestures for settled, silent Neutral expressions.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(200)]
[AddComponentMenu("Character/Idle Face Controller")]
public class IdleFaceController : MonoBehaviour
{
    const float MinimumDuration = 0.001f;

    [Header("References")]
    [SerializeField] SkinnedMeshRenderer face;
    [SerializeField] ExpressionController expressionController;
    [Tooltip("Idle gestures yield while this debugger holds a preview on the same face.")]
    [SerializeField] BlendShapeDebugger blendShapeDebugger;

    [Header("Mouth Scheduler")]
    [SerializeField] bool enableMouthGestures = true;
    [Tooltip("Minimum and maximum seconds after a mouth gesture before the next one.")]
    [SerializeField] Vector2 mouthInterval = new Vector2(8f, 16f);

    [Header("Lip Purse")]
    [SerializeField, Range(0f, 100f)] float puckerWeight = 15f;
    [SerializeField, Range(0f, 100f)] float lipPressWeight = 5f;
    [SerializeField, Min(MinimumDuration)] float purseEaseIn = 0.25f;
    [SerializeField] Vector2 purseHold = new Vector2(0.25f, 0.5f);
    [SerializeField, Min(MinimumDuration)] float purseEaseOut = 0.35f;

    [Header("Lip Twitch")]
    [SerializeField] Vector2 twitchWeight = new Vector2(5f, 10f);
    [SerializeField, Min(MinimumDuration)] float twitchEaseIn = 0.06f;
    [SerializeField, Min(MinimumDuration)] float twitchHold = 0.03f;
    [SerializeField, Min(MinimumDuration)] float twitchEaseOut = 0.12f;

    [Header("Gaze Scheduler")]
    [SerializeField] bool enableGazeShifts = true;
    [Tooltip("Minimum and maximum seconds after a gaze shift before the next one.")]
    [SerializeField] Vector2 gazeInterval = new Vector2(4f, 9f);
    [SerializeField] Vector2 gazeWeight = new Vector2(15f, 25f);
    [SerializeField, Min(MinimumDuration)] float gazeEaseIn = 0.18f;
    [SerializeField] Vector2 gazeHold = new Vector2(0.8f, 1.8f);
    [SerializeField, Min(MinimumDuration)] float gazeEaseOut = 0.25f;

    enum Phase { Waiting, EasingIn, Holding, EasingOut }

    struct ShapeTarget
    {
        public int index;
        public float weight;
        public ShapeTarget(int index, float weight) { this.index = index; this.weight = weight; }
    }

    class Channel
    {
        public readonly List<ShapeTarget> targets = new List<ShapeTarget>(3);
        public bool active;
        public bool hasWritten;
        public Phase phase;
        public float elapsed;
        public float duration;
        public float easeIn;
        public float hold;
        public float easeOut;

        public void Enter(Phase next, float seconds)
        {
            phase = next;
            elapsed = 0f;
            duration = seconds;
        }

        public void Reset()
        {
            active = false;
            hasWritten = false;
            targets.Clear();
        }
    }

    readonly Channel _mouth = new Channel();
    readonly Channel _gaze = new Channel();
    readonly HashSet<string> _warnings = new HashSet<string>();
    readonly List<int> _twitchIndices = new List<int>(2);
    readonly List<Vector2Int> _gazePairs = new List<Vector2Int>(4);
    int _puckerIndex;
    int _pressLeftIndex;
    int _pressRightIndex;
    bool _purseAvailable;
    bool _cacheDirty = true;
    bool _previewWasHolding;
    SkinnedMeshRenderer _cachedFace;
    Mesh _cachedMesh;

    void OnEnable()
    {
        if (!Application.isPlaying) return;
        ValidateSettings();
        _warnings.Clear();
        _cacheDirty = true;
        _previewWasHolding = false;
        RebuildCache();
    }

    void OnDisable()
    {
        CancelChannels();
        _cacheDirty = true;
        _previewWasHolding = false;
    }

    void OnValidate()
    {
        ValidateSettings();
        _cacheDirty = true;
    }

    void ValidateSettings()
    {
        mouthInterval = ClampRange(mouthInterval, 0f, float.MaxValue);
        gazeInterval = ClampRange(gazeInterval, 0f, float.MaxValue);
        purseHold = ClampRange(purseHold, MinimumDuration, float.MaxValue);
        gazeHold = ClampRange(gazeHold, MinimumDuration, float.MaxValue);
        twitchWeight = ClampRange(twitchWeight, 0f, 100f);
        gazeWeight = ClampRange(gazeWeight, 0f, 100f);
        puckerWeight = Mathf.Clamp(puckerWeight, 0f, 100f);
        lipPressWeight = Mathf.Clamp(lipPressWeight, 0f, 100f);
        purseEaseIn = Mathf.Max(MinimumDuration, purseEaseIn);
        purseEaseOut = Mathf.Max(MinimumDuration, purseEaseOut);
        twitchEaseIn = Mathf.Max(MinimumDuration, twitchEaseIn);
        twitchHold = Mathf.Max(MinimumDuration, twitchHold);
        twitchEaseOut = Mathf.Max(MinimumDuration, twitchEaseOut);
        gazeEaseIn = Mathf.Max(MinimumDuration, gazeEaseIn);
        gazeEaseOut = Mathf.Max(MinimumDuration, gazeEaseOut);
    }

    static Vector2 ClampRange(Vector2 range, float minimum, float maximum)
    {
        range.x = Mathf.Clamp(range.x, minimum, maximum);
        range.y = Mathf.Clamp(range.y, range.x, maximum);
        return range;
    }

    void WarnOnce(string key, string message)
    {
        if (_warnings.Add(key)) Debug.LogWarning($"{name}: {message}", this);
    }

    bool PreviewOwnsFace(SkinnedMeshRenderer renderer)
    {
        return renderer && blendShapeDebugger && blendShapeDebugger.Face == renderer &&
            blendShapeDebugger.IsHolding;
    }

    int FindShape(string shape)
    {
        int index = _cachedMesh.GetBlendShapeIndex(shape);
        if (index < 0)
            WarnOnce(shape, $"idle gesture blendshape '{shape}' is missing; gestures requiring it are skipped.");
        return index;
    }

    void AddGazePair(string leftEyeShape, string rightEyeShape)
    {
        int left = FindShape(leftEyeShape);
        int right = FindShape(rightEyeShape);
        if (left >= 0 && right >= 0) _gazePairs.Add(new Vector2Int(left, right));
    }

    void RebuildCache()
    {
        CancelChannels();
        _cachedFace = face;
        _cachedMesh = face ? face.sharedMesh : null;
        _cacheDirty = false;
        _purseAvailable = false;
        _twitchIndices.Clear();
        _gazePairs.Clear();
        if (!_cachedMesh)
        {
            WarnOnce("face", "assign a Face renderer with blendshapes to IdleFaceController.");
            return;
        }

        _puckerIndex = FindShape("mouthPucker");
        _pressLeftIndex = FindShape("mouthPressLeft");
        _pressRightIndex = FindShape("mouthPressRight");
        _purseAvailable = _puckerIndex >= 0 && _pressLeftIndex >= 0 && _pressRightIndex >= 0;
        int twitchLeft = FindShape("mouthLeft");
        int twitchRight = FindShape("mouthRight");
        if (twitchLeft >= 0) _twitchIndices.Add(twitchLeft);
        if (twitchRight >= 0) _twitchIndices.Add(twitchRight);

        // Anatomical left/right: one eye looks out while the other looks in.
        AddGazePair("eyeLookOutLeft", "eyeLookInRight");
        AddGazePair("eyeLookInLeft", "eyeLookOutRight");
        AddGazePair("eyeLookUpLeft", "eyeLookUpRight");
        AddGazePair("eyeLookDownLeft", "eyeLookDownRight");
    }

    bool CanRunGestures()
    {
        if (!expressionController)
        {
            WarnOnce("expression", "assign INA's ExpressionController to gate idle gestures.");
            return false;
        }
        if (!expressionController.isActiveAndEnabled) return false;
        if (expressionController.Face != face)
        {
            WarnOnce("expressionFace", "IdleFaceController and ExpressionController must reference the same Face.");
            return false;
        }
        return expressionController.IsNeutral && !expressionController.IsSpeaking &&
            !expressionController.IsTransitioning;
    }

    void LateUpdate()
    {
        if (!Application.isPlaying) return;

        // Preserve both current weights and debugger restoration values until release.
        if (PreviewOwnsFace(_cachedFace ? _cachedFace : face))
        {
            _previewWasHolding = true;
            return;
        }
        if (_previewWasHolding)
        {
            _previewWasHolding = false;
            CancelChannels();
        }

        if (_cacheDirty || _cachedFace != face || _cachedMesh != (face ? face.sharedMesh : null))
            RebuildCache();
        if (!_cachedFace || !_cachedMesh || !CanRunGestures())
        {
            CancelChannels();
            return;
        }

        UpdateChannel(_mouth, enableMouthGestures && (_purseAvailable || _twitchIndices.Count > 0), true);
        UpdateChannel(_gaze, enableGazeShifts && _gazePairs.Count > 0, false);
    }

    void UpdateChannel(Channel channel, bool enabled, bool mouth)
    {
        if (!enabled)
        {
            ClearChannelWeights(channel);
            channel.Reset();
            return;
        }
        if (!channel.active)
        {
            channel.active = true;
            ScheduleWait(channel, mouth);
        }

        Advance(channel, Time.deltaTime, mouth);
        if (channel.targets.Count == 0) return;
        float progress = Mathf.Clamp01(channel.elapsed / channel.duration);
        float factor = 1f;
        if (channel.phase == Phase.EasingIn) factor = Mathf.SmoothStep(0f, 1f, progress);
        else if (channel.phase == Phase.EasingOut) factor = Mathf.SmoothStep(1f, 0f, progress);
        foreach (var target in channel.targets)
            _cachedFace.SetBlendShapeWeight(target.index, target.weight * factor);
        channel.hasWritten = true;
    }

    void Advance(Channel channel, float deltaTime, bool mouth)
    {
        // Carry surplus time across phases; bound work following an extreme hitch.
        for (int transitions = 0; deltaTime > 0f && transitions < 64; transitions++)
        {
            float remaining = Mathf.Max(0f, channel.duration - channel.elapsed);
            if (deltaTime < remaining)
            {
                channel.elapsed += deltaTime;
                return;
            }
            deltaTime -= remaining;
            switch (channel.phase)
            {
                case Phase.Waiting:
                    BeginGesture(channel, mouth);
                    break;
                case Phase.EasingIn:
                    channel.Enter(Phase.Holding, channel.hold);
                    break;
                case Phase.Holding:
                    channel.Enter(Phase.EasingOut, channel.easeOut);
                    break;
                case Phase.EasingOut:
                    ClearChannelWeights(channel);
                    channel.targets.Clear();
                    ScheduleWait(channel, mouth);
                    break;
            }
        }
    }

    void BeginGesture(Channel channel, bool mouth)
    {
        channel.targets.Clear();
        if (mouth)
        {
            bool purse = _purseAvailable && (_twitchIndices.Count == 0 || Random.value < 0.5f);
            if (purse)
            {
                channel.targets.Add(new ShapeTarget(_puckerIndex, puckerWeight));
                channel.targets.Add(new ShapeTarget(_pressLeftIndex, lipPressWeight));
                channel.targets.Add(new ShapeTarget(_pressRightIndex, lipPressWeight));
                channel.easeIn = purseEaseIn;
                channel.hold = Random.Range(purseHold.x, purseHold.y);
                channel.easeOut = purseEaseOut;
            }
            else
            {
                int index = _twitchIndices[Random.Range(0, _twitchIndices.Count)];
                channel.targets.Add(new ShapeTarget(index, Random.Range(twitchWeight.x, twitchWeight.y)));
                channel.easeIn = twitchEaseIn;
                channel.hold = twitchHold;
                channel.easeOut = twitchEaseOut;
            }
        }
        else
        {
            var pair = _gazePairs[Random.Range(0, _gazePairs.Count)];
            float weight = Random.Range(gazeWeight.x, gazeWeight.y);
            channel.targets.Add(new ShapeTarget(pair.x, weight));
            channel.targets.Add(new ShapeTarget(pair.y, weight));
            channel.easeIn = gazeEaseIn;
            channel.hold = Random.Range(gazeHold.x, gazeHold.y);
            channel.easeOut = gazeEaseOut;
        }
        channel.Enter(Phase.EasingIn, channel.easeIn);
    }

    void ScheduleWait(Channel channel, bool mouth)
    {
        var interval = mouth ? mouthInterval : gazeInterval;
        channel.Enter(Phase.Waiting, Random.Range(interval.x, interval.y));
    }

    void ClearChannelWeights(Channel channel)
    {
        if (channel.hasWritten && _cachedFace && _cachedMesh &&
            _cachedFace.sharedMesh == _cachedMesh && !PreviewOwnsFace(_cachedFace))
            foreach (var target in channel.targets)
                _cachedFace.SetBlendShapeWeight(target.index, 0f);
        channel.hasWritten = false;
    }

    void CancelChannels()
    {
        ClearChannelWeights(_mouth);
        ClearChannelWeights(_gaze);
        _mouth.Reset();
        _gaze.Reset();
    }
}
