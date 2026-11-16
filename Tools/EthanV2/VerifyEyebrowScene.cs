using System;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Newtonsoft.Json;
public static class VerifyEthanEyebrowScene {
 public static object Run(){
  if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Edit Mode required");
  const string root="Assets/_Sandbox/Characters/Ethan_V2/";
  var brow=AssetDatabase.LoadAssetAtPath<Material>(root+"Materials/Ethan_Eyebrows.mat");
  var rows=new System.Collections.Generic.List<object>();
  for(int s=0;s<SceneManager.sceneCount;s++)foreach(var head in SceneManager.GetSceneAt(s).GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<SkinnedMeshRenderer>(true))){
   if(head.name!="BAKED_head_lod0_mesh"||AssetDatabase.GetAssetPath(head.sharedMesh)!=root+"Ethan_V2_73_Baked.fbx")continue;
   var materials=head.sharedMaterials;bool changed=materials.Length<2||!materials[1];
   if(changed){Undo.RecordObject(head,"Assign Ethan V2 eyebrow material");Array.Resize(ref materials,Math.Max(2,materials.Length));materials[1]=brow;head.sharedMaterials=materials;PrefabUtility.RecordPrefabInstancePropertyModifications(head);}
   rows.Add(new{scene=head.gameObject.scene.path,head=head.name,changed,materials=head.sharedMaterials.Select(m=>m?m.name:null).ToArray()});
  }
  File.WriteAllText("Data/EthanV2/Eyebrows/scene_material_check.json",JsonConvert.SerializeObject(rows,Formatting.Indented));return rows;
 }
}
