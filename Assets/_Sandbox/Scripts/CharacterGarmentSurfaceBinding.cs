using System;
using UnityEngine;

/// <summary>Fits a garment to an animated body surface without removing skin.</summary>
[DisallowMultipleComponent, DefaultExecutionOrder(32000)]
public sealed class CharacterGarmentSurfaceBinding : MonoBehaviour
{
    [Serializable] public struct Binding
    {
        public int a, b, c;
        public Vector3 barycentric, offset;
        [Range(0, 1)] public float influence;
    }
    [SerializeField] Transform characterSpace;
    [SerializeField] SkinnedMeshRenderer body, garment;
    [SerializeField] Binding[] bindings = Array.Empty<Binding>();
    SkinCache bodyCache, garmentCache;
    Mesh generatedMesh;
    GameObject generatedObject;
    Vector3[] fitted;
    bool originalEnabled;
    CharacterHairStartupWarmup startup;
    public int BindingCount => bindings.Length;
    public Mesh OutputMesh => generatedMesh;

    sealed class SkinCache
    {
        public SkinnedMeshRenderer renderer;
        public Vector3[] vertices, posed;
        public Matrix4x4[] bindposes, matrices;
        public Transform[] bones;
        public byte[] counts;
        public BoneWeight1[] weights;
        public SkinCache(SkinnedMeshRenderer source)
        {
            renderer = source; var mesh = source.sharedMesh;
            vertices = mesh.vertices; posed = new Vector3[vertices.Length];
            bindposes = mesh.bindposes; matrices = new Matrix4x4[bindposes.Length]; bones = source.bones;
            using (var c = mesh.GetBonesPerVertex()) counts = c.ToArray();
            using (var w = mesh.GetAllBoneWeights()) weights = w.ToArray();
        }
        public void Update(Transform space)
        {
            int limit = renderer.quality == SkinQuality.Auto ? (int)QualitySettings.skinWeights : (int)renderer.quality;
            if (limit <= 0) limit = 4;
            var inverse = space.worldToLocalMatrix;
            for (int i = 0; i < matrices.Length; i++)
                matrices[i] = inverse * bones[i].localToWorldMatrix * bindposes[i];
            int at = 0;
            for (int i = 0; i < vertices.Length; i++)
            {
                var position = Vector3.zero; float total = 0;
                for (int j = 0; j < counts[i]; j++)
                {
                    var weight = weights[at++];
                    if (j >= limit) continue;
                    position += matrices[weight.boneIndex].MultiplyPoint3x4(vertices[i]) * weight.weight;
                    total += weight.weight;
                }
                posed[i] = total > 0 ? position / total : vertices[i];
            }
        }
    }

    public void Configure(Transform space, SkinnedMeshRenderer skin, SkinnedMeshRenderer clothing, Binding[] data)
    {
        Release(); characterSpace = space; body = skin; garment = clothing; bindings = data;
    }
    bool Ready => !startup || !startup.isActiveAndEnabled || startup.IsReady;
    void OnEnable()
    {
        startup = GetComponent<CharacterHairStartupWarmup>();
        if (Application.isPlaying && Ready) ApplyPose();
    }
    void LateUpdate() { if (Application.isPlaying && Ready) ApplyPose(); }
    void OnDisable() { Release(); }
    void OnDestroy() { Release(); }

    // Also used explicitly on disposable Edit Mode verification instances.
    public void ApplyPose()
    {
        if (!body || !garment || !characterSpace || bindings.Length != garment.sharedMesh.vertexCount) return;
        if (!generatedMesh)
        {
            bodyCache = new SkinCache(body); garmentCache = new SkinCache(garment);
            fitted = new Vector3[bindings.Length];
            generatedMesh = Instantiate(garment.sharedMesh);
            generatedMesh.name = garment.sharedMesh.name + " (surface fitted)";
            generatedMesh.hideFlags = HideFlags.DontSave;
            generatedMesh.ClearBlendShapes(); generatedMesh.MarkDynamic();
            generatedObject = new GameObject("Ethan_ClothingSurface");
            generatedObject.hideFlags = HideFlags.DontSave;
            generatedObject.layer = garment.gameObject.layer;
            generatedObject.transform.SetParent(characterSpace, false);
            generatedObject.AddComponent<MeshFilter>().sharedMesh = generatedMesh;
            var output = generatedObject.AddComponent<MeshRenderer>();
            output.sharedMaterials = garment.sharedMaterials;
            output.shadowCastingMode = garment.shadowCastingMode;
            output.receiveShadows = garment.receiveShadows;
            output.lightProbeUsage = garment.lightProbeUsage;
            output.reflectionProbeUsage = garment.reflectionProbeUsage;
            originalEnabled = garment.enabled;
            output.enabled = originalEnabled; garment.enabled = false;
        }
        bodyCache.Update(characterSpace); garmentCache.Update(characterSpace);
        var positions = bodyCache.posed;
        for (int i = 0; i < bindings.Length; i++)
        {
            var binding = bindings[i];
            var a = positions[binding.a]; var b = positions[binding.b]; var c = positions[binding.c];
            var tangent = (b - a).normalized;
            var normal = Vector3.Cross(tangent, c - a).normalized;
            if (normal.sqrMagnitude < .5f) { fitted[i] = garmentCache.posed[i]; continue; }
            var bitangent = Vector3.Cross(normal, tangent);
            var surface = a * binding.barycentric.x + b * binding.barycentric.y + c * binding.barycentric.z
                + tangent * binding.offset.x + bitangent * binding.offset.y + normal * binding.offset.z;
            fitted[i] = Vector3.Lerp(garmentCache.posed[i], surface, binding.influence);
        }
        generatedMesh.vertices = fitted;
        generatedMesh.RecalculateNormals(); generatedMesh.RecalculateTangents(); generatedMesh.RecalculateBounds();
    }
    public void Release()
    {
        if (generatedObject && garment) garment.enabled = originalEnabled;
        if (generatedObject) { generatedObject.SetActive(false); if (Application.isPlaying) Destroy(generatedObject); else DestroyImmediate(generatedObject); }
        if (generatedMesh) { if (Application.isPlaying) Destroy(generatedMesh); else DestroyImmediate(generatedMesh); }
        generatedObject = null; generatedMesh = null; bodyCache = null; garmentCache = null; fitted = null;
    }
}
