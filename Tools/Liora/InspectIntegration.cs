using System;using System.IO;using System.Linq;using UnityEngine;using UnityEditor;using UnityEngine.SceneManagement;using Newtonsoft.Json;
public static class InspectLioraIntegration {
 static float[] M(Matrix4x4 m)=>Enumerable.Range(0,16).Select(j=>m[j/4,j%4]).ToArray();
 static object Inspect(GameObject g)=>new{name=g.name,path=AssetDatabase.GetAssetPath(g),components=g.GetComponentsInChildren<Component>(true).Where(c=>c&&!(c is Transform)&&!(c is Renderer)&&!(c is MeshFilter)).Select(c=>new{type=c.GetType().FullName,path=AnimationUtility.CalculateTransformPath(c.transform,g.transform),json=EditorJsonUtility.ToJson(c)}),meshes=g.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(s=>new{s.name,mesh=AssetDatabase.GetAssetPath(s.sharedMesh),materials=s.sharedMaterials.Select(m=>AssetDatabase.GetAssetPath(m)),bones=s.bones.Select(b=>b?b.name:null),weighted=s.sharedMesh.GetAllBoneWeights().ToArray().Where(w=>w.weight>0).Select(w=>w.boneIndex).Distinct().Select(i=>s.bones[i].name)}),bones=g.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="head"||t.name=="Face_head").Select(t=>new{path=AnimationUtility.CalculateTransformPath(t,g.transform),matrix=M(g.transform.worldToLocalMatrix*t.localToWorldMatrix)})};
 public static object Run(){
 Directory.CreateDirectory("Data/Liora/Integration");
 var target=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Sandbox/Characters/Liora/Liora_73_Baked.fbx");
 var template=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Sandbox/Characters/Ethan_V2/Candidate/EthanV2_Candidate.prefab");
 var report=new{target=Inspect(target),template=Inspect(template),scenes=Enumerable.Range(0,SceneManager.sceneCount).Select(j=>SceneManager.GetSceneAt(j)).Select(s=>new{s.path,s.isDirty,roots=s.GetRootGameObjects().Select(r=>new{r.name,prefab=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(r)})})};
 File.WriteAllText("Data/Liora/Integration/inspection.json",JsonConvert.SerializeObject(report,Formatting.Indented));
 return new{template=template.name,target=target.name,playing=EditorApplication.isPlaying};}
}
