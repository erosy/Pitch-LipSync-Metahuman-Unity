using System;using System.IO;using System.Linq;using UnityEngine;using UnityEditor;using UnityEngine.SceneManagement;using Unity.DemoTeam.Hair;using Newtonsoft.Json;using Newtonsoft.Json.Linq;
public static class InstallV2ScalpGroom {
 const string Root="Assets/_Sandbox/Characters/Ethan_V2/";const string Dir=Root+"Groom/";
 static Vector3 V(JToken a)=>new Vector3((float)a[0],(float)a[1],(float)a[2]);
 static void Ref(UnityEngine.Object o,string f,UnityEngine.Object v){var s=new SerializedObject(o);s.FindProperty(f).objectReferenceValue=v;s.ApplyModifiedPropertiesWithoutUndo();}
 public static object Run(){
  if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Edit Mode required; user controls Play Mode");
  if(AssetDatabase.LoadAssetAtPath<HairAsset>(Dir+"EthanV2_Scalp32.asset"))throw new Exception("V2 scalp already exists; inspect before replacing");
  var config=JObject.Parse(File.ReadAllText("Data/EthanV2/Groom/export.json"));
  AssetDatabase.Refresh();
  var oldRoot=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Sandbox/Characters/Ethan/Ethan.prefab");var old=oldRoot.GetComponentInChildren<HairInstance>(true);var oldAsset=old.strandGroupProviders[0].hairAsset;
  var provider=ScriptableObject.CreateInstance<BlenderFittedHairData>();provider.curveFile=AssetDatabase.LoadAssetAtPath<TextAsset>(Dir+"EthanV2_Scalp32.bytes");AssetDatabase.CreateAsset(provider,Dir+"EthanV2_Curves.asset");
  var asset=ScriptableObject.CreateInstance<HairAsset>();asset.settingsBasic=oldAsset.settingsBasic;asset.settingsBasic.type=HairAsset.Type.Custom;asset.settingsBasic.kLODClusters=false;
  asset.settingsCustom=oldAsset.settingsCustom;asset.settingsCustom.dataProvider=provider;asset.settingsCustom.settingsResolve.resampleCurves=false;asset.settingsCustom.settingsResolve.resampleResolution=32;asset.strandGroupsAutoBuild=false;
  AssetDatabase.CreateAsset(asset,Dir+"EthanV2_Scalp32.asset");HairAssetBuilder.BuildHairAsset(asset,HairAssetBuilder.BuildFlags.DisableProgress);EditorUtility.SetDirty(asset);AssetDatabase.SaveAssets();
  var group=asset.strandGroups.Single();if(group.strandCount!=94408||group.strandParticleCount!=32)throw new Exception("Unexpected strand topology");
  // Verify every root and tip survived the provider/build memory-layout conversion.
  float endpointError=0;using(var reader=new BinaryReader(File.OpenRead(Dir+"EthanV2_Scalp32.bytes"))){reader.ReadInt32();reader.ReadInt32();reader.ReadInt32();for(int j=0;j<94408;j++){HairAssetUtility.DeclareStrandIterator(group,j,out int start,out int step,out int end);for(int k=0;k<32;k++){var p=new Vector3(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());if(k==0||k==31)endpointError=Mathf.Max(endpointError,Vector3.Distance(p,group.particlePosition[start+k*step]));}}}if(endpointError>1e-6f)throw new Exception("Groom endpoints changed");
  int n=(int)config["sdf_resolution"];var tex=new Texture3D(n,n,n,TextureFormat.RFloat,false){name="EthanV2_HeadSDF",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};var bytes=File.ReadAllBytes(Dir+"EthanV2_HeadSDF.bytes");var values=new float[bytes.Length/4];Buffer.BlockCopy(bytes,0,values,0,bytes.Length);tex.SetPixelData(values,0);tex.Apply(false,false);AssetDatabase.CreateAsset(tex,Dir+"EthanV2_HeadSDF.asset");
  var roots=Enumerable.Range(0,SceneManager.sceneCount).SelectMany(i=>SceneManager.GetSceneAt(i).GetRootGameObjects());
  var root=roots.FirstOrDefault(r=>r.GetComponentsInChildren<SkinnedMeshRenderer>(true).Any(x=>AssetDatabase.GetAssetPath(x.sharedMesh)==Root+"Ethan_V2_73_Baked.fbx"));
  if(!root){root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"Ethan_V2_73_Baked.fbx"));}
  if(root.GetComponentInChildren<HairInstance>(true))throw new Exception("V2 already has hair; refusing duplicate");
  var head=root.transform.Find((string)config["head_bone_path"]);if(!head)throw new Exception("Head bone missing");
  Undo.RegisterFullObjectHierarchyUndo(root,"Install Ethan V2 scalp groom");
  var proxy=new GameObject("EthanV2_HeadMeters");proxy.transform.SetParent(head,false);var scale=head.lossyScale;proxy.transform.localScale=new Vector3(1/Mathf.Abs(scale.x),1/Mathf.Abs(scale.y),1/Mathf.Abs(scale.z));
  var bounds=new GameObject("EthanV2_HeadCollision");bounds.transform.SetParent(proxy.transform,false);var lo=V(config["sdf_min"]);var hi=V(config["sdf_max"]);bounds.transform.localPosition=(lo+hi)*.5f;var boundary=bounds.AddComponent<HairBoundary>();boundary.settings.mode=HairBoundary.Settings.Mode.Standalone;boundary.settings.type=HairBoundary.Settings.Type.DiscreteSDF;boundary.settingsSDF.kSDFTexture=tex;boundary.settingsSDF.kSDFWorldSize=hi-lo;
  var go=new GameObject("EthanV2_ScalpHair");go.SetActive(false);go.transform.SetParent(proxy.transform,false);var hair=go.AddComponent<HairInstance>();
  hair.settingsExecutive=old.settingsExecutive;hair.settingsExecutive.updateSimulationInEditor=false;hair.settingsExecutive.updateSimulation=true;hair.settingsExecutive.updateSimulationRate=HairInstance.SettingsExecutive.UpdateRate.Fixed30Hz;
  hair.strandGroupDefaults=old.strandGroupDefaults;hair.strandGroupDefaults.settingsGeometry.strandScale=HairSim.SettingsGeometry.StrandScale.Fixed;hair.strandGroupDefaults.settingsGeometry.strandLength=false;hair.strandGroupDefaults.settingsGeometry.strandDiameter=false;
  var material=AssetDatabase.LoadAssetAtPath<Material>(Root+"Materials/Ethan_StrandHair.mat");hair.strandGroupDefaults.settingsRendering.material=true;hair.strandGroupDefaults.settingsRendering.materialAsset=material;
  hair.settingsVolumetrics=old.settingsVolumetrics;hair.settingsEnvironment=old.settingsEnvironment;hair.settingsEnvironment.boundaryCapture=false;hair.settingsEnvironment.boundaryResident=new[]{boundary};hair.settingsEnvironment.defaultSolidMargin=.15f;
  hair.strandGroupProviders=new[]{new HairInstance.GroupProvider{hairAsset=asset}};HairInstanceBuilder.BuildHairInstance(hair,hair.strandGroupProviders);go.SetActive(true);
  var warmup=root.GetComponent<CharacterHairStartupWarmup>()??root.AddComponent<CharacterHairStartupWarmup>();Ref(warmup,"targetAnimator",root.GetComponentInChildren<Animator>(true));var so=new SerializedObject(warmup);var hs=so.FindProperty("strandHair");hs.arraySize=1;hs.GetArrayElementAtIndex(0).objectReferenceValue=hair;so.ApplyModifiedPropertiesWithoutUndo();
  var ready=root.GetComponent<CharacterReadiness>()??root.AddComponent<CharacterReadiness>();Ref(ready,"startupWarmup",warmup);
  const string prefab=Root+"Ethan_V2.prefab";if(File.Exists(prefab))throw new Exception("V2 prefab exists; refuse overwrite");PrefabUtility.SaveAsPrefabAssetAndConnect(root,prefab,InteractionMode.AutomatedAction);AssetDatabase.SaveAssets();
  var saved=AssetDatabase.LoadAssetAtPath<GameObject>(prefab).GetComponentInChildren<HairInstance>(true);if(saved.settingsExecutive.updateSimulationInEditor||saved.settingsExecutive.updateSimulationRate!=HairInstance.SettingsExecutive.UpdateRate.Fixed30Hz)throw new Exception("Saved simulation settings invalid");
  if((go.transform.lossyScale-Vector3.one).magnitude>.001f||go.transform.localScale!=Vector3.one||go.transform.localPosition!=Vector3.zero)throw new Exception("Meter attachment invalid");
  var result=new{prefab,strands=group.strandCount,points=group.strandParticleCount,simulationHz=30,endpointError,worldScale=new[]{go.transform.lossyScale.x,go.transform.lossyScale.y,go.transform.lossyScale.z},editModeSimulation=false,collisionResolution=n,collisionMin=values.Min(),collisionMax=values.Max(),startupWired=true,playModeTested=false};File.WriteAllText("Data/EthanV2/Groom/installation.json",JsonConvert.SerializeObject(result,Formatting.Indented));return result;
 }
}
