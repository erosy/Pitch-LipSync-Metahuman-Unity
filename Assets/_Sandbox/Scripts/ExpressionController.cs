using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>Uses speaking shapes throughout audio playback, holds them briefly after playback stops, then crossfades to the silent face.</summary>
public class ExpressionController : MonoBehaviour
{
    [System.Serializable]
    public struct ShapeWeight
    {
        public string shape;
        [Range(0f, 100f)] public float weight;
        public ShapeWeight(string shape, float weight) { this.shape = shape; this.weight = weight; }
    }

    [System.Serializable]
    public class Expression
    {
        public string name;
        [InspectorName("Default Face (Silent)")]
        public ShapeWeight defaultFace = new ShapeWeight("", 100f);
        [FormerlySerializedAs("shapes")]
        public ShapeWeight[] speakingShapes;
    }

    [SerializeField] SkinnedMeshRenderer face;
    [SerializeField] uLipSync.uLipSync lipSync;
    [SerializeField] uLipSync.uLipSyncBlendShape blendShapeDriver;
    [Tooltip("Scales every expression (1 = as authored below).")]
    [SerializeField, Range(0f, 1.5f)] float intensity = 1f;
    [Tooltip("Duration of the entire expression crossfade in seconds. All shapes fade together with a smooth start and finish.")]
    [SerializeField, Min(0.01f)] float fadeTime = 0.3f;
    [Tooltip("Seconds to hold the speaking expression after audio ends, before fading to the silent face. Does not delay the start of speech.")]
    [SerializeField, Min(0f)] float endOfSpeechDelay = 0.3f;
    [SerializeField] Expression[] expressions =
    {
        new Expression
        {
            name = "Joy",
            speakingShapes = new[]
            {
                new ShapeWeight("mouthSmileLeft", 55f),
                new ShapeWeight("mouthSmileRight", 55f),
                new ShapeWeight("cheekSquintLeft", 50f),
                new ShapeWeight("cheekSquintRight", 50f),
                new ShapeWeight("eyeSquintLeft", 30f),
                new ShapeWeight("eyeSquintRight", 30f),
                new ShapeWeight("browInnerUp", 10f),
            },
        },
    };

    public IReadOnlyList<Expression> Expressions => expressions;
    public SkinnedMeshRenderer Face => face;
    public bool IsSpeaking { get; private set; }
    public bool IsNeutral => string.IsNullOrEmpty(_expressionName);
    public bool IsTransitioning => _cacheDirty || _transitionDirty ||
        _fadeElapsed < Mathf.Max(0.01f, fadeTime);

    class WeightState
    {
        public float current;
        public float start;
        public float target;
        public float nextTarget;
    }

    Dictionary<int, WeightState> _weights = new Dictionary<int, WeightState>();
    readonly Dictionary<string, int> _indices = new Dictionary<string, int>();
    readonly HashSet<int> _visemes = new HashSet<int>();
    readonly HashSet<int> _latestVisemes = new HashSet<int>();
    SkinnedMeshRenderer _cachedFace;
    Mesh _cachedMesh;
    string _expressionName;
    bool _cacheDirty = true;
    bool _transitionDirty = true;
    float _fadeElapsed;
    bool _wasAudioPlaying;
    float _audioStoppedAt = float.NegativeInfinity;

    public void SetExpression(string expressionName) => _expressionName = expressionName;
    public void Joy() => SetExpression("Joy");
    public void Neutral() => SetExpression(null);

    void OnEnable()
    {
        _cacheDirty = true;
    }

    void OnDisable()
    {
        IsSpeaking = false;
        _wasAudioPlaying = false;
        _audioStoppedAt = float.NegativeInfinity;
        ClearWeights();
    }

    void OnValidate() => _cacheDirty = true;

    bool IsAudioPlaying()
    {
        if (!lipSync || !lipSync.isActiveAndEnabled || !blendShapeDriver ||
            !blendShapeDriver.isActiveAndEnabled || blendShapeDriver.skinnedMeshRenderer != face)
            return false;

        var audioSource = lipSync.audioSourceProxy
            ? lipSync.audioSourceProxy.GetComponent<AudioSource>() : lipSync.GetComponent<AudioSource>();
        return audioSource && audioSource.isActiveAndEnabled && audioSource.isPlaying;
    }

    void LateUpdate()
    {
        bool audioPlaying = IsAudioPlaying();
        if (audioPlaying)
            _audioStoppedAt = float.NegativeInfinity;
        else if (_wasAudioPlaying)
            _audioStoppedAt = Time.unscaledTime;

        _wasAudioPlaying = audioPlaying;
        IsSpeaking = audioPlaying ||
            Time.unscaledTime - _audioStoppedAt < Mathf.Max(0f, endOfSpeechDelay);

        var mesh = face ? face.sharedMesh : null;
        RefreshVisemes();
        if (_cacheDirty || _cachedFace != face || _cachedMesh != mesh) RebuildCache(mesh);
        if (!_cachedFace || !_cachedMesh) return;

        foreach (var state in _weights.Values) state.nextTarget = 0f;
        if (!string.IsNullOrEmpty(_expressionName) && expressions != null)
        {
            foreach (var expression in expressions)
            {
                if (expression == null || expression.name != _expressionName) continue;
                if (IsSpeaking)
                {
                    if (expression.speakingShapes != null)
                        foreach (var shape in expression.speakingShapes) SetTarget(shape);
                }
                else SetTarget(expression.defaultFace);
                break;
            }
        }

        foreach (var state in _weights.Values)
            if (state.target != state.nextTarget) _transitionDirty = true;

        if (_transitionDirty)
        {
            foreach (var state in _weights.Values)
            {
                state.start = state.current;
                state.target = state.nextTarget;
            }
            _fadeElapsed = 0f;
            _transitionDirty = false;
        }

        float duration = Mathf.Max(0.01f, fadeTime);
        _fadeElapsed = Mathf.Min(_fadeElapsed + Time.deltaTime, duration);
        float progress = Mathf.SmoothStep(0f, 1f, _fadeElapsed / duration);
        foreach (var pair in _weights)
        {
            pair.Value.current = Mathf.Lerp(pair.Value.start, pair.Value.target, progress);
            face.SetBlendShapeWeight(pair.Key, pair.Value.current);
        }
    }

    void SetTarget(ShapeWeight shape)
    {
        if (!string.IsNullOrEmpty(shape.shape) && _indices.TryGetValue(shape.shape, out int index))
            _weights[index].nextTarget = shape.weight * intensity;
    }

    void RefreshVisemes()
    {
        _latestVisemes.Clear();
        if (blendShapeDriver && blendShapeDriver.skinnedMeshRenderer == face && blendShapeDriver.blendShapes != null)
            foreach (var shape in blendShapeDriver.blendShapes)
                if (shape != null && shape.index >= 0) _latestVisemes.Add(shape.index);
        if (_visemes.SetEquals(_latestVisemes)) return;
        _visemes.Clear();
        _visemes.UnionWith(_latestVisemes);
        _cacheDirty = true;
    }

    void RebuildCache(Mesh mesh)
    {
        bool sameMesh = _cachedFace == face && _cachedMesh == mesh;
        if (!sameMesh) ClearWeights();
        var previous = _weights;
        _weights = new Dictionary<int, WeightState>();
        _indices.Clear();
        _cachedFace = face;
        _cachedMesh = mesh;
        _cacheDirty = false;
        _transitionDirty = true;
        if (!face || !mesh) return;

        var visited = new HashSet<string>();
        void Register(ShapeWeight shape)
        {
            if (string.IsNullOrEmpty(shape.shape) || !visited.Add(shape.shape)) return;
            int index = mesh.GetBlendShapeIndex(shape.shape);
            if (index < 0 || _visemes.Contains(index))
            {
                string reason = index < 0 ? "was not found" : "is controlled by uLipSync";
                Debug.LogWarning($"{name}: expression blendshape '{shape.shape}' {reason}; skipping it.", this);
                return;
            }
            _indices.Add(shape.shape, index);
            _weights[index] = previous.TryGetValue(index, out var state)
                ? state : new WeightState { current = face.GetBlendShapeWeight(index) };
        }

        if (expressions != null)
            foreach (var expression in expressions)
            {
                if (expression == null) continue;
                Register(expression.defaultFace);
                if (expression.speakingShapes != null)
                    foreach (var shape in expression.speakingShapes) Register(shape);
            }

        if (sameMesh)
            foreach (var pair in previous)
                if (!_weights.ContainsKey(pair.Key) && !_visemes.Contains(pair.Key))
                    face.SetBlendShapeWeight(pair.Key, 0f);
    }

    void ClearWeights()
    {
        if (_cachedFace && _cachedFace.sharedMesh == _cachedMesh)
            foreach (var pair in _weights)
                if (!_visemes.Contains(pair.Key)) _cachedFace.SetBlendShapeWeight(pair.Key, 0f);
        _weights.Clear();
        _indices.Clear();
    }
}
