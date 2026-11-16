using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json;

public static class AuditV2Fingers
{
    public static object Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Edit Mode required");
        var root = PrefabUtility.LoadPrefabContents("Assets/_Sandbox/Characters/Ethan_V2/Ethan_V2.prefab");
        try
        {
            var bridge = root.GetComponent<CharacterSkeletonPoseBridge>();
            var body = root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r => r.name == "BAKED_body_lod0_mesh");
            var rows = bridge.Bindings.Where(p => new[]{"thumb", "index", "middle", "ring", "pinky", "hand"}.Any(f => p.target.name.StartsWith(f)))
                .Select(p => new { target = p.target.name, parent = p.target.parent.name, source = p.source.name, sourceParent = p.source.parent.name,
                    targetPosition = ((Vector3)p.targetBind.GetColumn(3)).ToString("F6"), sourcePosition = ((Vector3)p.sourceBindInverse.inverse.GetColumn(3)).ToString("F6"),
                    targetLocal = p.target.localPosition.ToString("F6"), sourceLocal = p.source.localPosition.ToString("F6"),
                    rotationDifference = Quaternion.Angle(p.sourceBindInverse.inverse.rotation,p.targetBind.rotation) }).ToArray();
            var result = new { rows, bodyBones = body.bones.Select(t=>new {t.name, parent=t.parent.name}).ToArray() };
            Directory.CreateDirectory("Data/EthanV2/FingerRepair");
            File.WriteAllText("Data/EthanV2/FingerRepair/before_audit.json", JsonConvert.SerializeObject(result, Formatting.Indented));
            return rows;
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
