using System;using System.IO;using System.Linq;using UnityEditor;using UnityEngine;using Newtonsoft.Json;
public static class AuditV2ClothingRepair {
 static float[][] V(Vector3[] a)=>a.Select(v=>new[]{v.x,v.y,v.z}).ToArray();
 static Vector3[] Skin(SkinnedMeshRenderer r,Transform root){var v=r.sharedMesh.vertices;var m=r.bones.Select((b,j)=>root.worldToLocalMatrix*b.localToWorldMatrix*r.sharedMesh.bindposes[j]).ToArray();var output=new Vector3[v.Length];using(var counts=r.sharedMesh.GetBonesPerVertex())using(var weights=r.sharedMesh.GetAllBoneWeights()){int at=0;for(int j=0;j<v.Length;j++)for(int k=0;k<counts[j];k++){var w=weights[at++];output[j]+=m[w.boneIndex].MultiplyPoint3x4(v[j])*w.weight;}}return output;}
 public static object Run(){
 if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Edit Mode required; user controls Play Mode");
 var r=PrefabUtility.LoadPrefabContents("Assets/_Sandbox/Characters/Ethan_V2/Ethan_V2.prefab");try{
 var all=r.GetComponentsInChildren<SkinnedMeshRenderer>(true);var body=all.Single(x=>x.name=="BAKED_body_lod0_mesh");var cloth=all.Single(x=>x.name=="BAKED_Outfits_LOD0");
 byte[] counts;object[] weights;using(var c=body.sharedMesh.GetBonesPerVertex())using(var w=body.sharedMesh.GetAllBoneWeights()){counts=c.ToArray();weights=w.Select(x=>(object)new{bone=x.boneIndex,weight=x.weight}).ToArray();}
 var json=new{bodyVertices=V(Skin(body,r.transform)),bodyTriangles=body.sharedMesh.triangles,bodyCounts=counts.Select(c=>(int)c).ToArray(),bodyWeights=weights,clothVertices=V(Skin(cloth,r.transform)),clothTriangles=cloth.sharedMesh.triangles};
 File.WriteAllText("Data/EthanV2/ClothingRepair/binding_input.json",JsonConvert.SerializeObject(json));return new{body=body.sharedMesh.vertexCount,cloth=cloth.sharedMesh.vertexCount};}finally{PrefabUtility.UnloadPrefabContents(r);}}
}
