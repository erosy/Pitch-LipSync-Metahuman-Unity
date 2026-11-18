using System;using System.IO;using System.Linq;using UnityEngine;using UnityEditor;
public static class LioraHeadFrame {
 public static object Run(){var model=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Sandbox/Characters/Liora/Liora_73_Baked.fbx");var r=model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(s=>s.name=="BAKED_head_lod0_mesh");var m=new Mesh();r.BakeMesh(m);try{using(var w=new BinaryWriter(File.Create("Data/Liora/Integration/unity_head_world.bin"))){w.Write(m.vertexCount);foreach(var p in m.vertices){var q=r.transform.TransformPoint(p);w.Write(q.x);w.Write(q.y);w.Write(q.z);}}return new{m.vertexCount};}finally{UnityEngine.Object.DestroyImmediate(m);}}
}
