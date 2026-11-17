using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.Playables;
using UnityEngine.Animations;
using Sandbox.CharacterSystem;

public static class RenderLipRepair {
 static Mesh Snapshot(SkinnedMeshRenderer r,Transform root){
  var source=r.sharedMesh;var mesh=UnityEngine.Object.Instantiate(source);var p=source.vertices;var n=source.normals;var t=source.tangents;var dp=new Vector3[p.Length];var dn=new Vector3[p.Length];var dt=new Vector3[p.Length];
  for(int s=0;s<source.blendShapeCount;s++){float w=r.GetBlendShapeWeight(s)*.01f;if(w==0)continue;source.GetBlendShapeFrameVertices(s,0,dp,dn,dt);for(int i=0;i<p.Length;i++){p[i]+=dp[i]*w;n[i]+=dn[i]*w;if(t.Length>0)t[i]+=new Vector4(dt[i].x,dt[i].y,dt[i].z,0)*w;}}
  var matrices=r.bones.Select((b,i)=>root.worldToLocalMatrix*b.localToWorldMatrix*source.bindposes[i]).ToArray();var normalMatrices=matrices.Select(m=>m.inverse.transpose).ToArray();var op=new Vector3[p.Length];var on=new Vector3[p.Length];var ot=new Vector4[p.Length];
  using(var counts=source.GetBonesPerVertex())using(var weights=source.GetAllBoneWeights()){int at=0;for(int i=0;i<p.Length;i++){var tangent=Vector3.zero;for(int k=0;k<counts[i];k++){var w=weights[at++];op[i]+=matrices[w.boneIndex].MultiplyPoint3x4(p[i])*w.weight;on[i]+=normalMatrices[w.boneIndex].MultiplyVector(n[i])*w.weight;if(t.Length>0)tangent+=matrices[w.boneIndex].MultiplyVector(t[i])*w.weight;}on[i].Normalize();tangent.Normalize();if(t.Length>0)ot[i]=new Vector4(tangent.x,tangent.y,tangent.z,t[i].w);}}
  mesh.ClearBlendShapes();mesh.vertices=op;mesh.normals=on;if(t.Length>0)mesh.tangents=ot;mesh.RecalculateBounds();return mesh;
 }
 public static object Run(){
  if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Edit Mode required");Directory.CreateDirectory("Data/EthanV2/LipNormalRepair/After");
  var source=PrefabUtility.LoadPrefabContents("Assets/_Sandbox/Characters/Ethan_V2/Ethan_V2.prefab");var candidate=PrefabUtility.LoadPrefabContents("Assets/_Sandbox/Characters/Ethan_V2/Candidate/EthanV2_Candidate.prefab");var scene=EditorSceneManager.NewPreviewScene();var resources=new List<UnityEngine.Object>();int images=0;
  try{
   var cg=new GameObject("Candidate review camera");SceneManager.MoveGameObjectToScene(cg,scene);var camera=cg.AddComponent<Camera>();camera.scene=scene;camera.cameraType=CameraType.Preview;camera.cullingMask=1<<30;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.12f,.14f,.17f);camera.orthographic=true;camera.nearClipPlane=.01f;camera.farClipPlane=10;
   foreach(var data in new[]{(rotation:new Vector3(20,-25,0),intensity:1.2f),(rotation:new Vector3(-10,35,0),intensity:.6f)}){var go=new GameObject("Review light");SceneManager.MoveGameObjectToScene(go,scene);var l=go.AddComponent<Light>();l.type=LightType.Directional;l.intensity=data.intensity;l.cullingMask=1<<30;go.transform.rotation=Quaternion.Euler(data.rotation+new Vector3(0,180,0));}
   foreach(string pose in new[]{"Neutral","viseme_aa","viseme_FF","viseme_O","emo_joy","Speech","eyeBlinkLeft","browInnerUp"})foreach(var pair in new[]{(name:"Candidate",root:candidate)}){
    var root=pair.root;var transformState=root.GetComponentsInChildren<Transform>(true).ToDictionary(t=>t,t=>(t.localPosition,t.localRotation,t.localScale));var originalSkins=root.GetComponentsInChildren<SkinnedMeshRenderer>(true);foreach(var r in originalSkins)for(int s=0;s<r.sharedMesh.blendShapeCount;s++)r.SetBlendShapeWeight(s,0);var face=originalSkins.Single(r=>r.name==(pair.name=="Source"?"BAKED_head_lod0_mesh":"Face"));
    Action<string,float> set=(n,w)=>{int i=face.sharedMesh.GetBlendShapeIndex(n);if(i>=0)face.SetBlendShapeWeight(i,w);};
    bool motion=new[]{"Waving","Thinking","Thumbs Up"}.Contains(pose);
    if(!motion&&pose!="Neutral"){if(pose=="Speech"){set("mouthSmileLeft",35);set("mouthSmileRight",35);set("viseme_aa",70);}else set(pose,100);var sync=root.GetComponent<CharacterFacialMeshSync>();if(sync)sync.Apply();}
    if(motion){var definition=AssetDatabase.FindAssets("t:AnimationDefinition",new[]{"Assets/_Sandbox/Data"}).Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<AnimationDefinition>).Single(d=>d&&d.name==pose);var animator=root.GetComponentInChildren<AnimationModule>(true).Animator;var graph=PlayableGraph.Create("Candidate review pose");try{graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);var playable=AnimationClipPlayable.Create(graph,definition.Clip);playable.SetApplyFootIK(false);AnimationPlayableOutput.Create(graph,"pose",animator).SetSourcePlayable(playable);graph.Play();playable.SetTime(definition.Clip.length*.45);graph.Evaluate(0);var bridge=root.GetComponent<CharacterSkeletonPoseBridge>();if(bridge)bridge.ApplyPose();}finally{graph.Destroy();}}
    var objects=new List<GameObject>();var frameMeshes=new List<Mesh>();
    try{
     foreach(var r in originalSkins.Where(r=>r.enabled)){var mesh=Snapshot(r,root.transform);frameMeshes.Add(mesh);var go=new GameObject("Evaluated "+r.name);objects.Add(go);SceneManager.MoveGameObjectToScene(go,scene);go.layer=30;go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterials=r.sharedMaterials;}
     var center=motion?new Vector3(0,.95f,-.5f):new Vector3(0,1.65f,-.5f);camera.orthographicSize=motion?1.05f:.24f;camera.transform.position=center+Vector3.forward*3;camera.transform.LookAt(center);
     int width=motion?640:512,height=motion?900:640;var rt=RenderTexture.GetTemporary(width,height,24);var previous=RenderTexture.active;try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var tex=new Texture2D(width,height,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,width,height),0,0);tex.Apply();File.WriteAllBytes("Data/EthanV2/LipNormalRepair/After/"+pose.Replace(" ","")+"_"+pair.name+".png",tex.EncodeToPNG());UnityEngine.Object.DestroyImmediate(tex);images++;}finally{RenderTexture.active=previous;camera.targetTexture=null;RenderTexture.ReleaseTemporary(rt);}
    }finally{foreach(var go in objects)UnityEngine.Object.DestroyImmediate(go);foreach(var m in frameMeshes)UnityEngine.Object.DestroyImmediate(m);foreach(var p in transformState){p.Key.localPosition=p.Value.Item1;p.Key.localRotation=p.Value.Item2;p.Key.localScale=p.Value.Item3;}}
   }
   return new{images,staticEvaluatedMeshes=true,scalpSimulationNotRendered=true,sceneSaved=false};
  }finally{PrefabUtility.UnloadPrefabContents(source);PrefabUtility.UnloadPrefabContents(candidate);EditorSceneManager.ClosePreviewScene(scene);foreach(var o in resources)if(o)UnityEngine.Object.DestroyImmediate(o);}
 }
}

