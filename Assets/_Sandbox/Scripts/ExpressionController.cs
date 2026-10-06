using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fades the face between expressions built from individual blendshapes (smile corners, cheeks, eye squint...).
/// It only touches the shapes listed in its expressions, so uLipSync keeps driving the visemes on top.
/// </summary>
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
        public ShapeWeight[] shapes;
    }

    [SerializeField] SkinnedMeshRenderer face;
    [Tooltip("Scales every expression (1 = as authored below).")]
    [SerializeField, Range(0f, 1.5f)] float intensity = 1f;
    [Tooltip("Seconds to fade from one expression to another.")]
    [SerializeField, Min(0.01f)] float fadeTime = 0.3f;
    [SerializeField] Expression[] expressions =
    {
        new Expression
        {
            name = "Joy",
            shapes = new[]
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

    // every blendshape any expression uses; neutral sets all of them to 0
    readonly List<int> _indices = new List<int>();
    readonly Dictionary<string, int> _slot = new Dictionary<string, int>();
    float[] _current;
    float[] _target;

    public void SetExpression(string expressionName)
    {
        Init();
        System.Array.Clear(_target, 0, _target.Length);
        foreach (var expression in expressions)
        {
            if (expression.name != expressionName) continue;
            foreach (var sw in expression.shapes)
                if (_slot.TryGetValue(sw.shape, out var slot)) _target[slot] = sw.weight * intensity;
        }
    }

    public void Joy() => SetExpression("Joy");
    public void Neutral() => SetExpression(null);

    void Init()
    {
        if (_current != null) return;
        foreach (var expression in expressions)
        foreach (var sw in expression.shapes)
        {
            if (_slot.ContainsKey(sw.shape)) continue;
            int index = face.sharedMesh.GetBlendShapeIndex(sw.shape);
            if (index < 0)
            {
                Debug.LogWarning($"{name}: blendshape '{sw.shape}' not found on {face.name}");
                continue;
            }
            _slot[sw.shape] = _indices.Count;
            _indices.Add(index);
        }
        _current = new float[_indices.Count];
        _target = new float[_indices.Count];
    }

    void Update()
    {
        Init();
        float step = 100f / fadeTime * Time.deltaTime;
        for (int i = 0; i < _indices.Count; i++)
        {
            _current[i] = Mathf.MoveTowards(_current[i], _target[i], step);
            face.SetBlendShapeWeight(_indices[i], _current[i]);
        }
    }
}
