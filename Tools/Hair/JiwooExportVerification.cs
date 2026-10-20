using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Formats.Alembic.Importer;

public static class JiwooExportVerification
{
    private const string Output = "D:/UnityProject/MetahumanTemplate/Data/JiwooReimport/Batch03";
    [Serializable] public class Bone { public string name; public Vector3 position, scale; public float[] matrix; }
    [Serializable] public class Sample { public int index; public Vector3 position; }
    [Serializable] public class Report
    {
        public string status;
        public string unityVersion;
        public bool avatarValid, avatarHuman;
        public Vector3 characterMin, characterMax, headScale, rootScale, hairMin, hairMax;
        public Bone[] bones;
        public string[] blendShapes, materials;
        public int vertices, curveCount, hairPoints, widthCount, skinnedBoneCount;
        public float widthMin, widthMax;
        public Sample[] hairSamples;
    }

    private static float[] Flatten(Matrix4x4 matrix)
    {
        var result = new float[16];
        for (var row = 0; row < 4; ++row)
            for (var col = 0; col < 4; ++col)
                result[row * 4 + col] = matrix[row, col];
        return result;
    }

    public static void Run()
    {
        try
        {
            const string fbx = "Assets/Exports/Jiwoo_Normalized.fbx";
            const string abc = "Assets/Exports/Jiwoo_Bob_HeadLocal.abc";
            var importer = (ModelImporter)AssetImporter.GetAtPath(fbx);
            importer.globalScale = 1;
            importer.useFileScale = true;
            importer.bakeAxisConversion = true;
            importer.importBlendShapes = true;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.optimizeGameObjects = false;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.isReadable = true;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.skinWeights = ModelImporterSkinWeights.Custom;
            importer.maxBonesPerVertex = 8;
            importer.minBoneWeight = 0.001f;
            importer.SaveAndReimport();

            var abcImporter = AssetImporter.GetAtPath(abc);
            var abcObject = new SerializedObject(abcImporter);
            abcObject.FindProperty("streamSettings.scaleFactor").floatValue = 1;
            abcObject.FindProperty("streamSettings.swapHandedness").boolValue = true;
            abcObject.FindProperty("streamSettings.importCurves").boolValue = true;
            abcObject.FindProperty("streamSettings.importMeshes").boolValue = false;
            abcObject.FindProperty("streamSettings.importCameras").boolValue = false;
            abcObject.FindProperty("streamSettings.importPoints").boolValue = false;
            abcObject.FindProperty("streamSettings.createCurveRenderers").boolValue = false;
            abcObject.ApplyModifiedPropertiesWithoutUndo();
            abcImporter.SaveAndReimport();

            var model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(fbx));
            var abcInstance = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(abc));
            var renderer = model.GetComponentInChildren<SkinnedMeshRenderer>(true);
            var mesh = renderer.sharedMesh;
            var head = model.GetComponentsInChildren<Transform>(true).Single(t => t.name == "head");
            var animator = model.GetComponent<Animator>();
            var curves = abcInstance.GetComponentInChildren<AlembicCurves>(true);
            if (curves == null || curves.Positions.Length == 0)
                throw new Exception("Alembic curves did not load.");
            var vertices = mesh.vertices.Select(renderer.transform.TransformPoint).ToArray();
            var report = new Report
            {
                status = "IMPORTED; geometric comparison pending",
                unityVersion = Application.unityVersion,
                avatarValid = animator != null && animator.avatar != null && animator.avatar.isValid,
                avatarHuman = animator != null && animator.avatar != null && animator.avatar.isHuman,
                vertices = mesh.vertexCount,
                skinnedBoneCount = renderer.bones.Length,
                blendShapes = Enumerable.Range(0, mesh.blendShapeCount).Select(mesh.GetBlendShapeName).ToArray(),
                materials = renderer.sharedMaterials.Select(m => m == null ? "null" : m.name).ToArray(),
                characterMin = vertices.Aggregate(Vector3.Min), characterMax = vertices.Aggregate(Vector3.Max),
                rootScale = model.transform.lossyScale, headScale = head.lossyScale,
                bones = model.GetComponentsInChildren<Transform>(true).Select(t => new Bone
                { name = t.name, position = t.position, scale = t.lossyScale, matrix = Flatten(t.localToWorldMatrix) }).ToArray(),
                curveCount = curves.CurveOffsets.Length, hairPoints = curves.Positions.Length,
                hairMin = curves.Positions.Aggregate(Vector3.Min), hairMax = curves.Positions.Aggregate(Vector3.Max),
                widthCount = curves.Widths.Length,
                widthMin = curves.Widths.Length > 0 ? curves.Widths.Min() : 0,
                widthMax = curves.Widths.Length > 0 ? curves.Widths.Max() : 0,
                hairSamples = Enumerable.Range(0, 2048).Select(i =>
                {
                    var index = (int)((long)i * (curves.Positions.Length - 1) / 2047);
                    return new Sample { index = index, position = curves.Positions[index] };
                }).ToArray(),
            };
            File.WriteAllText(Output + "/unity_import_report.json", JsonUtility.ToJson(report, true));
            using (var writer = new BinaryWriter(File.Create(Output + "/unity_vertices.bin")))
                foreach (var vertex in vertices) { writer.Write(vertex.x); writer.Write(vertex.y); writer.Write(vertex.z); }
            Debug.Log("JIWOO_ISOLATED_IMPORT_COMPLETE " + Output);
            UnityEngine.Object.DestroyImmediate(model);
            UnityEngine.Object.DestroyImmediate(abcInstance);
        }
        catch (Exception e)
        {
            File.WriteAllText(Output + "/unity_import_error.txt", e.ToString());
            Debug.LogException(e);
            EditorApplication.Exit(1);
        }
    }
}
