if (!UnityEditor.EditorApplication.isPlaying) throw new System.Exception("Enter Play Mode first.");
var h=UnityEngine.Object.FindObjectsByType<Unity.DemoTeam.Hair.HairInstance>(UnityEngine.FindObjectsInactive.Exclude).First();
var original=UnityEditor.AssetDatabase.LoadAssetAtPath<Unity.DemoTeam.Hair.HairAsset>("Assets/_Sandbox/Groom Hair Asset/Fitted/Ethan_BlenderFitted.asset");
var corrected=UnityEditor.AssetDatabase.LoadAssetAtPath<Unity.DemoTeam.Hair.HairAsset>("Assets/_Sandbox/Groom Hair Asset/Fitted/Ethan_EarClearance.asset");
var animator=h.GetComponentInParent<UnityEngine.Animator>();var speed=animator.speed;
var state=animator.GetCurrentAnimatorStateInfo(0).fullPathHash;
var camera=UnityEngine.Camera.main;
var f=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static;
var capture=System.AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Unity.Pipeline.Editor.Commands.Capture.CaptureCommands")).First(t=>t!=null);
var ticks=0;var phase=0;
System.Action<Unity.DemoTeam.Hair.HairAsset> apply=asset=>{
    h.strandGroupProviders=new[]{new Unity.DemoTeam.Hair.HairInstance.GroupProvider{hairAsset=asset}};
    h.ResetSimulationState();animator.Play(state,0,.25f);animator.Update(0);animator.speed=0;
    camera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().resetHistory=true;
};
apply(original);
System.Action<UnityEngine.Rendering.ScriptableRenderContext,UnityEngine.Camera> handler=null;
handler=(context,c)=>{
    if(c!=camera)return;ticks++;
    if(ticks==180){
        var size=((int,int))capture.GetMethod("GetGameViewTargetSize",f).Invoke(null,null);
        var bytes=(byte[])capture.GetMethod("EncodeScreenToPng",f).Invoke(null,new object[]{size.Item1,size.Item2});
        System.IO.File.WriteAllBytes("Docs/HairRendering/EarCorrection/"+(phase==0?"BaselineMatched":"CorrectedMatched")+".png",bytes);
        if(phase==0){phase=1;ticks=0;apply(corrected);}
        else{UnityEngine.Rendering.RenderPipelineManager.endCameraRendering-=handler;animator.speed=speed;System.IO.File.WriteAllText("Docs/HairRendering/EarCorrection/Status.txt","Matched comparison complete; corrected groom running with original animation.");}
    }
};
UnityEngine.Rendering.RenderPipelineManager.endCameraRendering+=handler;
return "Capturing original and corrected groom at the same stationary pose after 180 rendered frames each.";
