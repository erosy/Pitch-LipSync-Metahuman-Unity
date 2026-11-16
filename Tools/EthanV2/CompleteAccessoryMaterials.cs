using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Newtonsoft.Json;
public static class CompleteEthanAccessoryMaterials {
 public static object Run(){
  if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Edit Mode required; user controls Play Mode");
  const string root="Assets/_Sandbox/Characters/Ethan_V2/";
  var importer=(ModelImporter)AssetImporter.GetAtPath(root+"Ethan_V2_73_Baked.fbx");
  var names=new[]{"eyeEdge","eyeshell","saliva"};var outputs=new System.Collections.Generic.List<object>();
  foreach(var name in names){
   string label=name=="eyeEdge"?"EyeEdge":name=="eyeshell"?"EyeShell":"Saliva";
   string path=root+"Materials/Ethan_"+label+".mat";
   var m=AssetDatabase.LoadAssetAtPath<Material>(path);
   if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Unlit"));AssetDatabase.CreateAsset(m,path);}
   m.SetColor("_BaseColor",new Color(.8f,.8f,.8f,0));
   m.SetFloat("_Surface",1);m.SetFloat("_Blend",0);m.SetFloat("_AlphaClip",0);
   m.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);m.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
   if(m.HasProperty("_SrcBlendAlpha"))m.SetFloat("_SrcBlendAlpha",(float)BlendMode.One);
   if(m.HasProperty("_DstBlendAlpha"))m.SetFloat("_DstBlendAlpha",(float)BlendMode.OneMinusSrcAlpha);
   m.SetFloat("_ZWrite",0);m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");m.DisableKeyword("_ALPHATEST_ON");
   m.SetOverrideTag("RenderType","Transparent");m.renderQueue=(int)RenderQueue.Transparent;m.SetShaderPassEnabled("ShadowCaster",false);
   EditorUtility.SetDirty(m);
   string source="Ethan_NewMetaHumanCharacter_"+name+"_shader";
   importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),source),m);
   outputs.Add(new{source,material=path,alpha=0,sourceHasTexture=false});
  }
  AssetDatabase.SaveAssets();importer.SaveAndReimport();
  var remaps=importer.GetExternalObjectMap();
  foreach(var name in names){var id=new AssetImporter.SourceAssetIdentifier(typeof(Material),"Ethan_NewMetaHumanCharacter_"+name+"_shader");if(!remaps.TryGetValue(id,out var material)||!material)throw new Exception("Missing remap: "+name);}
  var result=new{created=outputs,materialRemapCount=remaps.Count,playing=EditorApplication.isPlaying};
  File.WriteAllText("Data/EthanV2/Materials/accessory_materials.json",JsonConvert.SerializeObject(result,Formatting.Indented));return result;
 }
}
