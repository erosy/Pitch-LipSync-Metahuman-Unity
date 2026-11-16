using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using Unity.Collections;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public static class RenderEthanNormals { const string Prefab="Assets/_Sandbox/Characters/Ethan_V2/Ethan_V2.prefab"; const string Output="Data/EthanV2/NormalRepair"; const string Target="Assets/_Sandbox/Characters/Ethan_V2/Meshes/EthanV2_Head_FacialHairOptimized.asset"; static void Guard(){if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Edit Mode required");} static SkinnedMeshRenderer Head(GameObject r)=>r.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(x=>x.name=="BAKED_head_lod0_mesh"); public static object Render(){
  Guard();var original=Head(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab));var source=AssetDatabase.LoadAssetAtPath<Mesh>(Target);var optimized=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/_Sandbox/Characters/Ethan_V2/Meshes/EthanV2_Head_NormalReview.asset");
  var scene=EditorSceneManager.NewPreviewScene();var meshes=new List<Mesh>();
  try{
   var go=new GameObject("Facial hair comparison");SceneManager.MoveGameObjectToScene(go,scene);go.layer=30;go.transform.SetPositionAndRotation(original.transform.position,original.transform.rotation);go.transform.localScale=original.transform.lossyScale;var filter=go.AddComponent<MeshFilter>();var renderer=go.AddComponent<MeshRenderer>();var reviewMat=new Material(original.sharedMaterials[0]);reviewMat.SetFloat("_ReceiveShadows",0);reviewMat.EnableKeyword("_RECEIVE_SHADOWS_OFF");renderer.sharedMaterial=reviewMat;
   var verts=optimized.vertices;var ids=optimized.GetTriangles(1).Concat(optimized.GetTriangles(2)).Distinct().ToArray();var area=new Bounds(original.transform.TransformPoint(verts[ids[0]]),Vector3.zero);foreach(int i in ids)area.Encapsulate(original.transform.TransformPoint(verts[i]));
   var cg=new GameObject("Comparison camera");SceneManager.MoveGameObjectToScene(cg,scene);var camera=cg.AddComponent<Camera>();camera.scene=scene;camera.cullingMask=1<<30;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.12f,.14f,.17f);camera.orthographic=true;camera.orthographicSize=Mathf.Max(area.extents.y*1.6f,area.extents.x*.75f);camera.nearClipPlane=.01f;camera.farClipPlane=2;camera.transform.position=area.center+Vector3.forward*.5f;camera.transform.LookAt(area.center);
   var lg=new GameObject("Comparison key light");SceneManager.MoveGameObjectToScene(lg,scene);var light=lg.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.3f;light.cullingMask=1<<30;lg.transform.rotation=camera.transform.rotation*Quaternion.Euler(20,-25,0);
   Directory.CreateDirectory(Output+"/SkinPreviews");
   foreach(string pose in new[]{"Neutral","eyeBlinkLeft","eyeBlinkRight","browInnerUp"})foreach(var pair in new[]{(name:"Before",mesh:source),(name:"Corrected",mesh:optimized)}){
    var m=new Mesh{indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};meshes.Add(m);var p=pair.mesh.vertices;var n=pair.mesh.normals;var tangent=pair.mesh.tangents;
    if(pose!="Neutral"){int s=pair.mesh.GetBlendShapeIndex(pose);var dp=new Vector3[p.Length];var dn=new Vector3[p.Length];var dt=new Vector3[p.Length];pair.mesh.GetBlendShapeFrameVertices(s,0,dp,dn,dt);for(int i=0;i<p.Length;i++){p[i]+=dp[i];n[i]=(n[i]+dn[i]).normalized;tangent[i]+=new Vector4(dt[i].x,dt[i].y,dt[i].z,0);}}
    m.vertices=p;m.normals=n;m.tangents=tangent;var uv=new List<Vector4>();pair.mesh.GetUVs(0,uv);m.SetUVs(0,uv);m.subMeshCount=1;m.SetTriangles(pair.mesh.GetTriangles(0),0);filter.sharedMesh=m;
    var rt=RenderTexture.GetTemporary(1024,512,24);try{camera.targetTexture=rt;camera.Render();var prior=RenderTexture.active;RenderTexture.active=rt;var texture=new Texture2D(1024,512,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1024,512),0,0);texture.Apply();RenderTexture.active=prior;File.WriteAllBytes(Output+"/SkinPreviews/"+pose+"_"+pair.name+".png",texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);}finally{camera.targetTexture=null;RenderTexture.ReleaseTemporary(rt);}
   }
   return new{images=8,poses=4};
  }finally{foreach(var m in meshes)UnityEngine.Object.DestroyImmediate(m);EditorSceneManager.ClosePreviewScene(scene);}
 }
}





