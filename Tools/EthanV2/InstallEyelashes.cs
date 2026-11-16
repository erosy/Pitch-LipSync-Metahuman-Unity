using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Newtonsoft.Json;
public static class InstallEthanV2Eyelashes {
 public static object Run(){
  if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Edit Mode required; user controls Play Mode");
  const string root="Assets/_Sandbox/Characters/Ethan_V2/";const string path=root+"Ethan_V2_73_Baked.fbx";const string output="Data/EthanV2/Eyelashes/";
  string guid=AssetDatabase.AssetPathToGUID(path);
  Directory.CreateDirectory(output+"Before");
  if(File.Exists(output+"Before/Ethan_V2_73_Baked.fbx"))throw new Exception("Backup already exists; refuse overwrite");
  File.Copy(path,output+"Before/Ethan_V2_73_Baked.fbx");File.Copy(path+".meta",output+"Before/Ethan_V2_73_Baked.fbx.meta");
  const string materialPath=root+"Materials/Ethan_EyelashStrands.mat";
  var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
  if(!material){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,materialPath);material.SetColor("_BaseColor",new Color(.006f,.003f,.002f,1));material.SetFloat("_Smoothness",.35f);material.SetFloat("_Metallic",0);material.SetFloat("_Cull",0);EditorUtility.SetDirty(material);AssetDatabase.SaveAssets();}
  File.Copy(output+"Ethan_V2_73_WithBrowsAndLashes.fbx",path,true);
  var importer=(ModelImporter)AssetImporter.GetAtPath(path);
  importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),"Ethan_V2_EyelashStrands"),material);importer.SaveAndReimport();
  var model=AssetDatabase.LoadAssetAtPath<GameObject>(path);var renderers=model.GetComponentsInChildren<SkinnedMeshRenderer>(true);var head=renderers.First(r=>r.name=="BAKED_head_lod0_mesh");
  if(head.sharedMesh.blendShapeCount!=73||!head.sharedMaterials.Contains(material))throw new Exception("Lash import failed");
  if(renderers.Any(r=>r.name=="BAKED_eyelashes_lod0_mesh"))throw new Exception("Old lash cards still exported");
  if(!head.sharedMaterials.Any(m=>m&&m.name=="Ethan_Eyebrows"))throw new Exception("Brow material missing");
  if(AssetDatabase.AssetPathToGUID(path)!=guid)throw new Exception("Model GUID changed");
  var sceneRows=new System.Collections.Generic.List<object>();
  for(int s=0;s<SceneManager.sceneCount;s++)foreach(var r in SceneManager.GetSceneAt(s).GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<SkinnedMeshRenderer>(true))){
   if(r.name!="BAKED_head_lod0_mesh"||AssetDatabase.GetAssetPath(r.sharedMesh)!=path)continue;
   var mats=r.sharedMaterials;bool changed=mats.Length<3||!mats[2];
   if(changed){Undo.RecordObject(r,"Assign Ethan V2 mesh lashes");Array.Resize(ref mats,Math.Max(3,mats.Length));mats[2]=material;r.sharedMaterials=mats;PrefabUtility.RecordPrefabInstancePropertyModifications(r);}
   sceneRows.Add(new{scene=r.gameObject.scene.path,changed,materials=r.sharedMaterials.Select(m=>m?m.name:null).ToArray()});
  }
  var result=new{model=path,guidPreserved=true,headVertices=head.sharedMesh.vertexCount,shapes=head.sharedMesh.blendShapeCount,materials=head.sharedMaterials.Select(m=>m?m.name:null).ToArray(),renderers=renderers.Length,oldCardsRemoved=true,browsPreserved=true,sceneRows,playing=EditorApplication.isPlaying};
  File.WriteAllText(output+"unity_installation.json",JsonConvert.SerializeObject(result,Formatting.Indented));return result;
 }
}
