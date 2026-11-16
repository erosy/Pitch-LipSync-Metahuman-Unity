using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json;
public static class AssignEthanV2Materials {
 const string Root="Assets/_Sandbox/Characters/Ethan_V2/";
 static Texture2D Tex(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"Textures/"+name);
 static Material Mat(string name) => AssetDatabase.LoadAssetAtPath<Material>(Root+"Materials/"+name+".mat");
 static void Set(Material m,string slot,Texture t) { if(m.HasProperty(slot))m.SetTexture(slot,t); }
 static void Bind(string name,string color,string normal) {
  var m=Mat(name); if(!m)throw new Exception(name);
  if(color!=null){var t=Tex(color);if(!t)throw new Exception(color);Set(m,"_BaseMap",t);Set(m,"_MainTex",t);}
  if(normal!=null){var t=Tex(normal);if(!t)throw new Exception(normal);Set(m,"_BumpMap",t);m.EnableKeyword("_NORMALMAP");}
  EditorUtility.SetDirty(m);
 }
 static Color[] Pixels(Texture2D t) {
  var rt=RenderTexture.GetTemporary(t.width,t.height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
  var old=RenderTexture.active;Graphics.Blit(t,rt);RenderTexture.active=rt;
  var read=new Texture2D(t.width,t.height,TextureFormat.RGBA32,false,true);read.ReadPixels(new Rect(0,0,t.width,t.height),0,0);read.Apply();var p=read.GetPixels();UnityEngine.Object.DestroyImmediate(read);RenderTexture.active=old;RenderTexture.ReleaseTemporary(rt);return p;
 }
 public static object Run() {
  if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Edit Mode required; user controls Play Mode");
  var normalFiles=Directory.GetFiles(Root+"Textures","*.png").Where(p=>Path.GetFileName(p).Contains("Normal")).ToArray();
  foreach(var path in Directory.GetFiles(Root+"Textures","*.png")){
   var imp=(TextureImporter)AssetImporter.GetAtPath(path);var name=Path.GetFileName(path);
   bool normal=name.Contains("Normal");bool data=name.Contains("Metallic")||name.Contains("Roughness");bool changed=false;
   if(normal&&imp.textureType!=TextureImporterType.NormalMap){imp.textureType=TextureImporterType.NormalMap;changed=true;}
   if(data&&imp.sRGBTexture){imp.sRGBTexture=false;changed=true;}
   if(changed)imp.SaveAndReimport();
  }
  Bind("Ethan_Head","Head_Basecolor.png","Head_Normal.png");
  Bind("Ethan_Body","Body_Basecolor.png","Body_Normal.png");
  Bind("Ethan_Teeth","Teeth_Color.png","Teeth_Normal.png");
  // The copied teeth shader is Unlit; keep its stored normal reference current
  // even though this shader does not expose or render a normal-map property.
  var teethData=new SerializedObject(Mat("Ethan_Teeth"));var envs=teethData.FindProperty("m_SavedProperties.m_TexEnvs");
  for(int j=0;j<envs.arraySize;j++){var pair=envs.GetArrayElementAtIndex(j);if(pair.FindPropertyRelative("first").stringValue=="_BumpMap")pair.FindPropertyRelative("second.m_Texture").objectReferenceValue=Tex("Teeth_Normal.png");}teethData.ApplyModifiedPropertiesWithoutUndo();
  Bind("Ethan_Eyes","Dark Brown Iris Macro Texture.png","Eyes_Normal.png");
  Bind("Ethan_Outfit","Shirt_SHD_BaseColor.png","Shirt_SHD_Normal.png");
  var metal=Tex("Shirt_SHD_Metallic.png");var rough=Tex("Shirt_SHD_Roughness.png");
  if(metal.width!=rough.width||metal.height!=rough.height)throw new Exception("Shirt map sizes differ");
  var mp=Pixels(metal);var rp=Pixels(rough);for(int j=0;j<mp.Length;j++)mp[j]=new Color(mp[j].r,0,0,1-rp[j].r);
  var packed=new Texture2D(metal.width,metal.height,TextureFormat.RGBA32,false,true);packed.SetPixels(mp);packed.Apply();
  var packedPath=Root+"Textures/Shirt_MetallicSmoothness.png";File.WriteAllBytes(packedPath,packed.EncodeToPNG());UnityEngine.Object.DestroyImmediate(packed);
  AssetDatabase.ImportAsset(packedPath);var pi=(TextureImporter)AssetImporter.GetAtPath(packedPath);pi.sRGBTexture=false;pi.alphaSource=TextureImporterAlphaSource.FromInput;pi.SaveAndReimport();
  var outfit=Mat("Ethan_Outfit");Set(outfit,"_MetallicGlossMap",AssetDatabase.LoadAssetAtPath<Texture2D>(packedPath));outfit.EnableKeyword("_METALLICSPECGLOSSMAP");if(outfit.HasProperty("_Smoothness"))outfit.SetFloat("_Smoothness",1);if(outfit.HasProperty("_WorkflowMode"))outfit.SetFloat("_WorkflowMode",1);outfit.DisableKeyword("_SPECULAR_SETUP");EditorUtility.SetDirty(outfit);
  var lashPath=Root+"Materials/Ethan_Eyelashes.mat";var lash=AssetDatabase.LoadAssetAtPath<Material>(lashPath);
  if(!lash){lash=new Material(Mat("Ethan_Hair"));AssetDatabase.CreateAsset(lash,lashPath);}
  Bind("Ethan_Eyelashes","Eyelashes_Color.png",null);
  Set(lash,"_BumpMap",null);lash.DisableKeyword("_NORMALMAP");EditorUtility.SetDirty(lash);
  AssetDatabase.SaveAssets();
  var modelPath=Root+"Ethan_V2_73_Baked.fbx";var importer=AssetImporter.GetAtPath(modelPath) as ModelImporter;
  var model=AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);var remaps=new Dictionary<string,string>();
  foreach(var source in model.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).Where(m=>m).Distinct()){
   var n=source.name.ToLowerInvariant();string target=null;
   if(n.Contains("head_shader"))target="Ethan_Head";else if(n.Contains("body_shader"))target="Ethan_Body";else if(n.Contains("teeth_shader"))target="Ethan_Teeth";else if(n.Contains("eyeleft_shader")||n.Contains("eyeright_shader"))target="Ethan_Eyes";else if(n.Contains("shirt_shd"))target="Ethan_Outfit";else if(n.Contains("eyelashes_shader"))target="Ethan_Eyelashes";else if(n.Contains("cartilage_shader"))target="Ethan_Cartilage";
   if(target!=null){importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),source.name),Mat(target));remaps[source.name]=target;}
  }
  if(remaps.Count>0)importer.SaveAndReimport();
  var rows=new List<object>();foreach(var name in new[]{"Ethan_Head","Ethan_Body","Ethan_Teeth","Ethan_Eyes","Ethan_Outfit","Ethan_Eyelashes"}){
   var m=Mat(name);var slots=new Dictionary<string,string>();foreach(var slot in m.GetTexturePropertyNames()){var t=m.GetTexture(slot);if(t)slots[slot]=AssetDatabase.GetAssetPath(t);}rows.Add(new{name,shader=m.shader.name,textures=slots});
  }
  Directory.CreateDirectory("Data/EthanV2/Materials");var result=new{materials=rows,remaps,playing=EditorApplication.isPlaying};File.WriteAllText("Data/EthanV2/Materials/assignment_report.json",JsonConvert.SerializeObject(result,Formatting.Indented));return result;
 }
}
