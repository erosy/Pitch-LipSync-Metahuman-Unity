using System;using System.IO;using System.Linq;using UnityEngine;using UnityEditor;using Unity.DemoTeam.Hair;using Newtonsoft.Json;using Newtonsoft.Json.Linq;
public static class VerifyV2ScalpGroom {
 public static object Run(){
 if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Edit Mode required");const string root="Assets/_Sandbox/Characters/Ethan_V2/";const string dir=root+"Groom/";
 var config=JObject.Parse(File.ReadAllText("Data/EthanV2/Groom/export.json"));int n=(int)config["sdf_resolution"];
 var tex=AssetDatabase.LoadAssetAtPath<Texture3D>(dir+"EthanV2_HeadSDF.asset");
 // Refresh only the newly generated collision field after independent coordinate checks.
 if(tex.width!=n){tex=AssetDatabase.LoadAssetAtPath<Texture3D>(dir+"EthanV2_HeadSDF96.asset");if(!tex){tex=new Texture3D(n,n,n,TextureFormat.RFloat,false){name="EthanV2_HeadSDF96",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};AssetDatabase.CreateAsset(tex,dir+"EthanV2_HeadSDF96.asset");}}
 var raw=File.ReadAllBytes(dir+"EthanV2_HeadSDF.bytes");var values=new float[raw.Length/4];Buffer.BlockCopy(raw,0,values,0,raw.Length);tex.SetPixelData(values,0);tex.Apply(false,false);EditorUtility.SetDirty(tex);AssetDatabase.SaveAssetIfDirty(tex);
 var edit=PrefabUtility.LoadPrefabContents(root+"Ethan_V2.prefab");try{edit.GetComponentInChildren<HairBoundary>(true).settingsSDF.kSDFTexture=tex;PrefabUtility.SaveAsPrefabAsset(edit,root+"Ethan_V2.prefab");}finally{PrefabUtility.UnloadPrefabContents(edit);}
 var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(root+"Ethan_V2.prefab");var hair=prefab.GetComponentInChildren<HairInstance>(true);var g=hair.strandGroupProviders[0].hairAsset.strandGroups.Single();
 if(g.strandCount!=94408||g.strandParticleCount!=32||hair.settingsExecutive.updateSimulationInEditor||hair.settingsExecutive.updateSimulationRate!=HairInstance.SettingsExecutive.UpdateRate.Fixed30Hz||hair.strandGroupDefaults.settingsGeometry.strandScale!=HairSim.SettingsGeometry.StrandScale.Fixed)throw new Exception("Invalid saved scalp settings");
 if(!prefab.GetComponent<CharacterHairStartupWarmup>()||!prefab.GetComponent<CharacterReadiness>())throw new Exception("Startup wiring missing");
 float maxEndpointError=0;using(var reader=new BinaryReader(File.OpenRead(dir+"EthanV2_Scalp32.bytes"))){reader.ReadBytes(12);for(int j=0;j<94408;j++){HairAssetUtility.DeclareStrandIterator(g,j,out int start,out int step,out int end);for(int k=0;k<32;k++){var p=new Vector3(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());if(k==0||k==31)maxEndpointError=Mathf.Max(maxEndpointError,Vector3.Distance(p,g.particlePosition[start+k*step]));}}}if(maxEndpointError>1e-6f)throw new Exception("Endpoints differ");
 var instance=PrefabUtility.LoadPrefabContents(root+"Ethan_V2.prefab");float scaleError;bool follows;
 try{var h=instance.GetComponentInChildren<HairInstance>(true);var proxy=h.transform.parent;var head=proxy.parent;var before=head.localRotation;head.localRotation=before*Quaternion.Euler(0,15,0);scaleError=(h.transform.lossyScale-Vector3.one).magnitude;follows=h.settingsEnvironment.boundaryResident[0].transform.parent==proxy;if(scaleError>.001f||!follows)throw new Exception("Attachment/collision motion check failed");}finally{PrefabUtility.UnloadPrefabContents(instance);}
 var result=new{passed=true,strands=g.strandCount,points=g.strandParticleCount,simulationHz=30,maxEndpointError,normalizedAttachmentScaleError=scaleError,collisionFollowsHead=follows,sdfResolution=n,sdfMin=values.Min(),sdfMax=values.Max(),warmup=true,readiness=true,editModeSimulation=false,playModeTested=false,sceneSaved=false};File.WriteAllText("Data/EthanV2/Groom/installation.json",JsonConvert.SerializeObject(result,Formatting.Indented));return result;
 }
}
