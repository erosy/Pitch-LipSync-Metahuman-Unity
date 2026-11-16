using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
public static class DiagnoseLiveEthanFace {
 public static object CaptureGpu(){
  if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Edit Mode required");
  var head=UnityEngine.Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Include).Single(r=>r.name=="BAKED_head_lod0_mesh"&&r.gameObject.activeInHierarchy);var root=head.transform.root;var skins=root.GetComponentsInChildren<SkinnedMeshRenderer>(true);var writers=root.GetComponentsInChildren<Behaviour>(true).Where(b=>b is Animator||new[]{"ExpressionModule","BlendShapeDebugger","uLipSyncBlendShape"}.Contains(b.GetType().Name)).ToDictionary(b=>b,b=>b.enabled);foreach(var b in writers.Keys)b.enabled=false;var weights=skins.ToDictionary(r=>r,r=>Enumerable.Range(0,r.sharedMesh.blendShapeCount).Select(r.GetBlendShapeWeight).ToArray());foreach(var r in skins)for(int s=0;s<r.sharedMesh.blendShapeCount;s++)r.SetBlendShapeWeight(s,0);head.SetBlendShapeWeight(head.sharedMesh.GetBlendShapeIndex("emo_joy"),100);root.GetComponent<CharacterFacialMeshSync>().Apply();
  int frames=0;EditorApplication.CallbackFunction callback=null;callback=()=>{EditorApplication.QueuePlayerLoopUpdate();if(++frames<5)return;EditorApplication.update-=callback;var camera=Camera.main;var old=camera.targetTexture;var active=RenderTexture.active;var rt=RenderTexture.GetTemporary(1080,1920,24);try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var t=new Texture2D(1080,1920,TextureFormat.RGB24,false);t.ReadPixels(new Rect(0,0,1080,1920),0,0);t.Apply();File.WriteAllBytes("Data/EthanV2/SmileRepair/gpu_joy.png",t.EncodeToPNG());UnityEngine.Object.DestroyImmediate(t);File.WriteAllText("Data/EthanV2/SmileRepair/gpu_capture.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{done=true,weight=head.GetBlendShapeWeight(head.sharedMesh.GetBlendShapeIndex("emo_joy"))}));}finally{camera.targetTexture=old;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);foreach(var r in skins)for(int s=0;s<weights[r].Length;s++)r.SetBlendShapeWeight(s,weights[r][s]);root.GetComponent<CharacterFacialMeshSync>().Apply();foreach(var b in writers.Keys)b.enabled=writers[b];EditorApplication.QueuePlayerLoopUpdate();}};EditorApplication.update+=callback;EditorApplication.QueuePlayerLoopUpdate();return new{scheduled=true};
 }
 static Mesh Snapshot(SkinnedMeshRenderer r,Transform root){
  var source=r.sharedMesh;var mesh=UnityEngine.Object.Instantiate(source);var p=source.vertices;var n=source.normals;var t=source.tangents;var dp=new Vector3[p.Length];var dn=new Vector3[p.Length];var dt=new Vector3[p.Length];
  for(int s=0;s<source.blendShapeCount;s++){float w=r.GetBlendShapeWeight(s)*.01f;if(w==0)continue;source.GetBlendShapeFrameVertices(s,0,dp,dn,dt);for(int i=0;i<p.Length;i++){p[i]+=dp[i]*w;n[i]+=dn[i]*w;if(t.Length>0)t[i]+=new Vector4(dt[i].x,dt[i].y,dt[i].z,0)*w;}}
  var matrices=r.bones.Select((b,i)=>root.worldToLocalMatrix*b.localToWorldMatrix*source.bindposes[i]).ToArray();var normals=matrices.Select(m=>m.inverse.transpose).ToArray();var op=new Vector3[p.Length];var on=new Vector3[p.Length];var ot=new Vector4[p.Length];
  using(var counts=source.GetBonesPerVertex())using(var weights=source.GetAllBoneWeights()){int at=0;for(int i=0;i<p.Length;i++){var tangent=Vector3.zero;for(int k=0;k<counts[i];k++){var w=weights[at++];op[i]+=matrices[w.boneIndex].MultiplyPoint3x4(p[i])*w.weight;on[i]+=normals[w.boneIndex].MultiplyVector(n[i])*w.weight;if(t.Length>0)tangent+=matrices[w.boneIndex].MultiplyVector(t[i])*w.weight;}on[i].Normalize();tangent.Normalize();if(t.Length>0)ot[i]=new Vector4(tangent.x,tangent.y,tangent.z,t[i].w);}}
  mesh.ClearBlendShapes();mesh.vertices=op;mesh.normals=on;if(t.Length>0)mesh.tangents=ot;mesh.RecalculateBounds();return mesh;
 }
 public static object Run(){
  if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Edit Mode required");
  var head=UnityEngine.Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Include).Single(r=>r.name=="BAKED_head_lod0_mesh"&&r.gameObject.activeInHierarchy);
  var root=head.transform.root;var renderers=root.GetComponentsInChildren<Renderer>(true);var skins=root.GetComponentsInChildren<SkinnedMeshRenderer>(true);var allRenderers=UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include);
  var weights=skins.ToDictionary(r=>r,r=>Enumerable.Range(0,r.sharedMesh.blendShapeCount).Select(r.GetBlendShapeWeight).ToArray());var materials=renderers.ToDictionary(r=>r,r=>r.sharedMaterials);var shadows=allRenderers.ToDictionary(r=>r,r=>r.shadowCastingMode);var enabled=renderers.ToDictionary(r=>r,r=>r.enabled);
  var camera=Camera.main;var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;var temps=new List<UnityEngine.Object>();int images=0;
  Directory.CreateDirectory("Data/EthanV2/SmileRepair/Live");
  try{
   foreach(string pose in new[]{"Neutral","Joy","SmileSpeech"}){
    foreach(var r in skins)for(int s=0;s<r.sharedMesh.blendShapeCount;s++)r.SetBlendShapeWeight(s,0);
    Action<string,float> set=(name,value)=>{int s=head.sharedMesh.GetBlendShapeIndex(name);if(s>=0)head.SetBlendShapeWeight(s,value);};
    if(pose=="Joy")set("emo_joy",100);if(pose=="SmileSpeech"){set("mouthSmileLeft",60);set("mouthSmileRight",60);set("viseme_aa",70);}
    root.GetComponent<CharacterFacialMeshSync>().Apply();
    var snapshots=new Dictionary<SkinnedMeshRenderer,MeshRenderer>();
    foreach(var skin in skins){var mesh=Snapshot(skin,root);temps.Add(mesh);var go=new GameObject("Disposable posed snapshot");temps.Add(go);go.layer=skin.gameObject.layer;go.transform.SetPositionAndRotation(root.position,root.rotation);go.transform.localScale=root.lossyScale;go.AddComponent<MeshFilter>().sharedMesh=mesh;var mr=go.AddComponent<MeshRenderer>();mr.sharedMaterials=materials[skin];mr.shadowCastingMode=shadows[skin];mr.enabled=enabled[skin];snapshots.Add(skin,mr);skin.enabled=false;}
    foreach(string variant in new[]{"Current","NoNormalMap","NoShadows","Unlit"}){
     foreach(var r in renderers){r.sharedMaterials=materials[r];r.enabled=r is SkinnedMeshRenderer?false:enabled[r];}foreach(var r in allRenderers)r.shadowCastingMode=shadows[r];foreach(var pair in snapshots){pair.Value.sharedMaterials=materials[pair.Key];pair.Value.shadowCastingMode=shadows[pair.Key];}
     if(variant=="NoShadows"){foreach(var r in allRenderers)r.shadowCastingMode=ShadowCastingMode.Off;foreach(var r in snapshots.Values)r.shadowCastingMode=ShadowCastingMode.Off;}
     if(variant=="NoNormalMap"||variant=="Unlit")foreach(var r in skins){var mats=materials[r].Select(m=>{if(!m)return m;var copy=new Material(m);temps.Add(copy);if(variant=="NoNormalMap"){copy.DisableKeyword("_NORMALMAP");if(copy.HasProperty("_BumpScale"))copy.SetFloat("_BumpScale",0);}else {var tex=m.HasProperty("_BaseMap")?m.GetTexture("_BaseMap"):null;var color=m.HasProperty("_BaseColor")?m.GetColor("_BaseColor"):Color.white;copy.shader=Shader.Find("Universal Render Pipeline/Unlit");copy.SetTexture("_BaseMap",tex);copy.SetColor("_BaseColor",color);}return copy;}).ToArray();r.sharedMaterials=mats;}
     if(variant=="NoNormalMap"||variant=="Unlit")foreach(var pair in snapshots)pair.Value.sharedMaterials=pair.Key.sharedMaterials;
     var rt=RenderTexture.GetTemporary(1080,1920,24);try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var t=new Texture2D(1080,1920,TextureFormat.RGB24,false);t.ReadPixels(new Rect(0,0,1080,1920),0,0);t.Apply();File.WriteAllBytes("Data/EthanV2/SmileRepair/Live/"+pose+"_"+variant+".png",t.EncodeToPNG());UnityEngine.Object.DestroyImmediate(t);images++;}finally{camera.targetTexture=oldTarget;RenderTexture.active=oldActive;RenderTexture.ReleaseTemporary(rt);}
    }
    foreach(var pair in snapshots)UnityEngine.Object.DestroyImmediate(pair.Value.gameObject);
   }
   return new{images,sceneSaved=false};
  }finally{foreach(var r in renderers){r.sharedMaterials=materials[r];r.enabled=enabled[r];}foreach(var r in allRenderers)r.shadowCastingMode=shadows[r];foreach(var r in skins)for(int s=0;s<weights[r].Length;s++)r.SetBlendShapeWeight(s,weights[r][s]);root.GetComponent<CharacterFacialMeshSync>().Apply();camera.targetTexture=oldTarget;RenderTexture.active=oldActive;foreach(var o in temps)UnityEngine.Object.DestroyImmediate(o);}
 }
}

