using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
public static class VerifyTalkDemoAlphaOutput {
 public static object Run() {
  if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Edit Mode only");
  var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();var dirty=scene.isDirty;
  using(var frame=new ContextContainer()) {
   var cameraData=frame.Create<UniversalCameraData>();cameraData.camera=Camera.main;
   cameraData.cameraTargetDescriptor=new RenderTextureDescriptor(16,16,RenderTextureFormat.ARGB32,0);
   cameraData.isAlphaOutputEnabled=false;
   var ctor=typeof(RenderingData).GetConstructor(BindingFlags.Instance|BindingFlags.NonPublic,null,new[]{typeof(ContextContainer)},null);
   var data=(RenderingData)ctor.Invoke(new object[]{frame});
   var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/_Sandbox/TalkDemoWeb/Renderer0.asset");
   var feature=renderer.rendererFeatures.Find(f=>f is TalkDemoAlphaOutputFeature) as TalkDemoAlphaOutputFeature;
   if(!feature || !feature.isActive)throw new Exception("Alpha feature missing or disabled");
   var extra=Camera.main.GetUniversalAdditionalCameraData();var pp=extra.renderPostProcessing;var aa=extra.antialiasing;
   feature.AddRenderPasses(null,ref data);
   if(!cameraData.isAlphaOutputEnabled)throw new Exception("Alpha flag was not enabled");
   if(pp!=extra.renderPostProcessing||aa!=extra.antialiasing)throw new Exception("Post-processing changed");
   var temp=new GameObject("Alpha test camera",typeof(Camera));temp.hideFlags=HideFlags.HideAndDontSave;
   try {cameraData.camera=temp.GetComponent<Camera>();cameraData.isAlphaOutputEnabled=false;feature.AddRenderPasses(null,ref data);if(cameraData.isAlphaOutputEnabled)throw new Exception("Unrelated camera changed");}
   finally{UnityEngine.Object.DestroyImmediate(temp);}
   return new { alphaEnabledFromFalse=true, unrelatedCameraUnchanged=true, postProcessing=pp, antialiasing=aa.ToString(), sceneDirtyBefore=dirty,sceneDirtyAfter=scene.isDirty,buildPerformed=false };
  }
 }
}
