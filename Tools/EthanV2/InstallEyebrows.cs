using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json;
public static class InstallEthanV2Eyebrows {
 public static object Run(){return Install(false);}
 public static object UpdateThickness(){return Install(true);}
 static object Install(bool thicknessUpdate){
  if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Edit Mode required; user controls Play Mode");
  const string path="Assets/_Sandbox/Characters/Ethan_V2/Ethan_V2_73_Baked.fbx";
  const string output="Data/EthanV2/Eyebrows/";
  var model=AssetDatabase.LoadAssetAtPath<GameObject>(path);
  var beforeLashes=model.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r=>r.name=="BAKED_eyelashes_lod0_mesh").sharedMesh;
  int lashVertices=beforeLashes.vertexCount;int lashShapes=beforeLashes.blendShapeCount;
  string guid=AssetDatabase.AssetPathToGUID(path);
  if(!thicknessUpdate){
   Directory.CreateDirectory(output+"Before");
   if(File.Exists(output+"Before/Ethan_V2_73_Baked.fbx"))throw new Exception("Backup already exists; refuse to overwrite");
   File.Copy(path,output+"Before/Ethan_V2_73_Baked.fbx");File.Copy(path+".meta",output+"Before/Ethan_V2_73_Baked.fbx.meta");
  } else if(!File.Exists(output+"BeforeThickness/Installed_Ethan_V2.fbx"))throw new Exception("Thickness backup missing");
  const string materialPath="Assets/_Sandbox/Characters/Ethan_V2/Materials/Ethan_Eyebrows.mat";
  var m=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
  if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,materialPath);}
  if(!thicknessUpdate){m.SetColor("_BaseColor",new Color(.018f,.009f,.005f,1));m.SetFloat("_Smoothness",.35f);m.SetFloat("_Metallic",0);m.SetFloat("_Cull",0);EditorUtility.SetDirty(m);AssetDatabase.SaveAssets();}
  File.Copy(output+"Ethan_V2_73_WithEyebrows.fbx",path,true);
  var importer=(ModelImporter)AssetImporter.GetAtPath(path);
  importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),"Ethan_V2_Eyebrows"),m);
  importer.SaveAndReimport();
  model=AssetDatabase.LoadAssetAtPath<GameObject>(path);
  var renderers=model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
  var head=renderers.First(r=>r.name=="BAKED_head_lod0_mesh");
  var lash=renderers.First(r=>r.name=="BAKED_eyelashes_lod0_mesh").sharedMesh;
  if(head.sharedMesh.blendShapeCount!=73||!head.sharedMaterials.Contains(m))throw new Exception("Eyebrow import validation failed");
  if(lash.vertexCount!=lashVertices||lash.blendShapeCount!=lashShapes)throw new Exception("Lash vertex/shape counts changed");
  if(AssetDatabase.AssetPathToGUID(path)!=guid)throw new Exception("Model GUID changed");
  var result=new{model=path,guidPreserved=true,headVertices=head.sharedMesh.vertexCount,shapes=head.sharedMesh.blendShapeCount,materials=head.sharedMaterials.Select(x=>x?x.name:null).ToArray(),lashVertices=lash.vertexCount,lashesUnchanged=true,renderers=renderers.Length,materialRemaps=importer.GetExternalObjectMap().Count,playing=EditorApplication.isPlaying};
  File.WriteAllText(output+"unity_installation.json",JsonConvert.SerializeObject(result,Formatting.Indented));return result;
 }
}
