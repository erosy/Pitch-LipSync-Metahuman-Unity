using System;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json;
public static class InspectEthan {
 public static string Run() {
  var root=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Sandbox/Characters/Ethan/Ethan.prefab");
  var report=new { playing=EditorApplication.isPlaying, meshes=root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(r=>new {name=r.name, mesh=r.sharedMesh.name, path=AssetDatabase.GetAssetPath(r.sharedMesh), vertices=r.sharedMesh.vertexCount, shapes=Enumerable.Range(0,r.sharedMesh.blendShapeCount).Select(r.sharedMesh.GetBlendShapeName).ToArray(),bones=r.bones.Select(b=>b?b.name:null).ToArray(),materials=r.sharedMaterials.Select(m=>m?m.name:null).ToArray()}).ToArray()};
  var json=JsonConvert.SerializeObject(report,Formatting.Indented);
  Directory.CreateDirectory("Data/EthanV2"); File.WriteAllText("Data/EthanV2/current_ethan.json",json); return json;
 }
}
