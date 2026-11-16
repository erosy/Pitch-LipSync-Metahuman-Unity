using System;using System.Linq;using System.IO;using UnityEngine;using UnityEditor;using UnityEngine.SceneManagement;using Unity.DemoTeam.Hair;using Newtonsoft.Json;
public static class AuditV2Groom {
 static float[] M(Matrix4x4 m)=>Enumerable.Range(0,16).Select(i=>m[i/4,i%4]).ToArray();
 public static object Run(){
 var model=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Sandbox/Characters/Ethan_V2/Ethan_V2_73_Baked.fbx");var r=model.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(x=>x.name=="BAKED_head_lod0_mesh");
 var bones=r.bones.Where(b=>b&&(b.name=="head"||b.name=="DEF-spine.006"||b.name.Contains("FacialRoot"))).Select(b=>new{name=b.name,path=AnimationUtility.CalculateTransformPath(b,model.transform),matrix=M(model.transform.worldToLocalMatrix*b.localToWorldMatrix)}).ToArray();
 var scenes=Enumerable.Range(0,SceneManager.sceneCount).SelectMany(i=>SceneManager.GetSceneAt(i).GetRootGameObjects()).SelectMany(x=>x.GetComponentsInChildren<SkinnedMeshRenderer>(true)).Where(x=>AssetDatabase.GetAssetPath(x.sharedMesh).Contains("Ethan_V2")).Select(x=>new{x.name,root=x.transform.root.name,path=AnimationUtility.CalculateTransformPath(x.transform,x.transform.root)}).ToArray();
 var old=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Sandbox/Characters/Ethan/Ethan.prefab").GetComponentInChildren<HairInstance>(true);
 var report=new{bones,meshMatrix=M(model.transform.worldToLocalMatrix*r.transform.localToWorldMatrix),first=r.sharedMesh.vertices.Take(3).Select(v=>new[]{v.x,v.y,v.z}),scenes,oldHair=EditorJsonUtility.ToJson(old),oldAsset=AssetDatabase.GetAssetPath(old.strandGroupProviders[0].hairAsset)};
 Directory.CreateDirectory("Data/EthanV2/Groom");File.WriteAllText("Data/EthanV2/Groom/audit.json",JsonConvert.SerializeObject(report,Formatting.Indented));return new{bones,scenes,oldAsset=report.oldAsset};}
}

