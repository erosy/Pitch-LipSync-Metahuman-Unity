// Register in Edit Mode before entering Play Mode. Observe actual hidden render frames.
System.IO.Directory.CreateDirectory("Docs/HairRendering/StartupWarmup");
var frames=0;var records=new System.Collections.Generic.List<object>();
var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static;
var capture=System.AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Unity.Pipeline.Editor.Commands.Capture.CaptureCommands")).First(t=>t!=null);
System.Action<UnityEngine.Rendering.ScriptableRenderContext,UnityEngine.Camera> handler=null;
handler=(context,camera)=>{
    if(!UnityEditor.EditorApplication.isPlaying||camera!=UnityEngine.Camera.main)return;
    var warmup=UnityEngine.Object.FindObjectsByType<EthanHairStartupWarmup>(UnityEngine.FindObjectsInactive.Exclude).FirstOrDefault();
    if(warmup==null)return;
    frames++;
    var hair=warmup.GetComponentInChildren<Unity.DemoTeam.Hair.HairInstance>();
    var visible=warmup.GetComponentsInChildren<UnityEngine.SkinnedMeshRenderer>(true).Count(r=>r.enabled&&r.gameObject.activeInHierarchy);
    if(frames<=24||frames==300||frames==1200)records.Add(new{frame=frames,ready=warmup.IsReady,resets=warmup.ResetCount,visibleBodyRenderers=visible,hairRenderer=hair.strandGroupDefaults.settingsRendering.renderer.ToString(),elapsed=hair.execState.elapsedTime});
    if(frames==4||frames==24||frames==1200){
        var size=((int,int))capture.GetMethod("GetGameViewTargetSize",flags).Invoke(null,null);
        var png=(byte[])capture.GetMethod("EncodeScreenToPng",flags).Invoke(null,new object[]{size.Item1,size.Item2});
        System.IO.File.WriteAllBytes("Docs/HairRendering/StartupWarmup/Frame"+frames+".png",png);
    }
    if(frames==24||frames==300||frames==1200)System.IO.File.WriteAllText("Docs/HairRendering/StartupWarmup/Frames.json",Newtonsoft.Json.JsonConvert.SerializeObject(records,Newtonsoft.Json.Formatting.Indented));
    if(frames==1200)UnityEngine.Rendering.RenderPipelineManager.endCameraRendering-=handler;
};
UnityEngine.Rendering.RenderPipelineManager.endCameraRendering+=handler;
return "Startup visibility/reset probe registered for 1200 Game frames.";
