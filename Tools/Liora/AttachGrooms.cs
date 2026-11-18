using System;using System.IO;using System.Linq;using UnityEngine;using UnityEditor;using Unity.DemoTeam.Hair;using Newtonsoft.Json;using Newtonsoft.Json.Linq;
public static class AttachLioraGrooms {
 const string Base="Assets/_Sandbox/Characters/Liora/";const string Folder=Base+"Candidate/";const string Groom=Base+"Groom/";const string Data="Data/Liora/Integration/";
 static Vector3 V(JToken a)=>new Vector3((float)a[0],(float)a[1],(float)a[2]);
 static Vector3 Read(BinaryReader r)=>new Vector3(r.ReadSingle(),r.ReadSingle(),r.ReadSingle());
 static Mesh Facial(string name,Transform head,Transform rig,JObject config){
 string path=Folder+"Liora_"+name+".asset";if(File.Exists(path))throw new Exception("Mesh exists "+path);
 using(var r=new BinaryReader(File.OpenRead(Data+name+".bin"))){int n=r.ReadInt32(),t=r.ReadInt32(),shapes=r.ReadInt32();var p=new Vector3[n];var normal=new Vector3[n];var uv=new Vector2[n];var triangles=new int[t];
 for(int i=0;i<n;i++)p[i]=Read(r);for(int i=0;i<n;i++)normal[i]=Read(r);for(int i=0;i<n;i++)uv[i]=new Vector2(r.ReadSingle(),r.ReadSingle());for(int i=0;i<t;i++)triangles[i]=r.ReadInt32();
 var mesh=new Mesh{name="Liora_"+name,indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.vertices=p;mesh.normals=normal;mesh.uv=uv;mesh.triangles=triangles;
 mesh.bindposes=new[]{Matrix4x4.identity};mesh.boneWeights=Enumerable.Repeat(new BoneWeight{boneIndex0=0,weight0=1},n).ToArray();
 var delta=new Vector3[n];var dn=new Vector3[n];float maxError=0;
 for(int s=0;s<shapes;s++){for(int i=0;i<n;i++)delta[i]=Read(r);for(int i=0;i<n;i++)dn[i]=Read(r);var sentinel=Enumerable.Range(0,n).Select(j=>delta[j].sqrMagnitude>0||dn[j].sqrMagnitude>0?Vector3.right:Vector3.zero).ToArray();mesh.AddBlendShapeFrame((string)config["shape_names"][s],100,delta,dn,sentinel);var check=new Vector3[n];mesh.GetBlendShapeFrameVertices(s,0,check,null,null);for(int i=0;i<n;i++)maxError=Mathf.Max(maxError,Vector3.Distance(check[i],delta[i]));}
 if(maxError>1e-7f)throw new Exception("Facial groom shape data lost "+name+" "+maxError);
 mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,path);return mesh;}
 }
 public static object Run(){
 if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Edit Mode required");var config=JObject.Parse(File.ReadAllText(Data+"groom_export.json"));
 foreach(var file in new[]{"Liora_Scalp32.bytes","Liora_HeadSDF.bytes"})AssetDatabase.ImportAsset(Groom+file);
 var oldRoot=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Sandbox/Characters/Ethan_V2/Candidate/EthanV2_Candidate.prefab");var old=oldRoot.GetComponentInChildren<HairInstance>(true);var oldAsset=old.strandGroupProviders[0].hairAsset;
 
 var provider=AssetDatabase.LoadAssetAtPath<BlenderFittedHairData>(Groom+"Liora_Curves.asset")??ScriptableObject.CreateInstance<BlenderFittedHairData>();provider.curveFile=AssetDatabase.LoadAssetAtPath<TextAsset>(Groom+"Liora_Scalp32.bytes");if(!AssetDatabase.Contains(provider))AssetDatabase.CreateAsset(provider,Groom+"Liora_Curves.asset");
 var asset=AssetDatabase.LoadAssetAtPath<HairAsset>(Groom+"Liora_Scalp32.asset")??ScriptableObject.CreateInstance<HairAsset>();asset.settingsBasic=oldAsset.settingsBasic;asset.settingsBasic.type=HairAsset.Type.Custom;asset.settingsBasic.kLODClusters=false;asset.settingsCustom=oldAsset.settingsCustom;asset.settingsCustom.dataProvider=provider;asset.settingsCustom.settingsResolve.resampleCurves=false;asset.settingsCustom.settingsResolve.resampleResolution=32;asset.strandGroupsAutoBuild=false;
 if(!AssetDatabase.Contains(asset))AssetDatabase.CreateAsset(asset,Groom+"Liora_Scalp32.asset");HairAssetBuilder.BuildHairAsset(asset,HairAssetBuilder.BuildFlags.DisableProgress);EditorUtility.SetDirty(asset);
 var group=asset.strandGroups.Single();if(group.strandCount!=(int)config["strands"]||group.strandParticleCount!=32)throw new Exception("Scalp topology mismatch");float endpointError=0;
 using(var r=new BinaryReader(File.OpenRead(Groom+"Liora_Scalp32.bytes"))){r.ReadInt32();r.ReadInt32();r.ReadInt32();for(int j=0;j<group.strandCount;j++){HairAssetUtility.DeclareStrandIterator(group,j,out int start,out int step,out int end);for(int k=0;k<32;k++){var p=Read(r);if(k==0||k==31)endpointError=Mathf.Max(endpointError,Vector3.Distance(p,group.particlePosition[start+k*step]));}}}if(endpointError>1e-7f)throw new Exception("Scalp endpoints changed");
 int n=(int)config["sdf_resolution"];var tex=new Texture3D(n,n,n,TextureFormat.RFloat,false){name="Liora_HeadSDF",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};var bytes=File.ReadAllBytes(Groom+"Liora_HeadSDF.bytes");var values=new float[bytes.Length/4];Buffer.BlockCopy(bytes,0,values,0,bytes.Length);tex.SetPixelData(values,0);tex.Apply(false,false);if(File.Exists(Groom+"Liora_HeadSDF.asset")){UnityEngine.Object.DestroyImmediate(tex);tex=AssetDatabase.LoadAssetAtPath<Texture3D>(Groom+"Liora_HeadSDF.asset");}else AssetDatabase.CreateAsset(tex,Groom+"Liora_HeadSDF.asset");
 var root=PrefabUtility.LoadPrefabContents(Folder+"Liora_Candidate.prefab");
 try{
 var rig=root.transform.Find("Armature");var head=rig.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="head");
 var proxy=new GameObject("Hair").transform;proxy.SetParent(head,false);if((proxy.lossyScale-Vector3.one).magnitude>1e-4f)throw new Exception("Head frame must be in meters");
 var collision=new GameObject("HeadCollision");collision.transform.SetParent(proxy,false);var lo=V(config["sdf_min"]);var hi=V(config["sdf_max"]);collision.transform.localPosition=(lo+hi)*.5f;var boundary=collision.AddComponent<HairBoundary>();boundary.settings.mode=HairBoundary.Settings.Mode.Standalone;boundary.settings.type=HairBoundary.Settings.Type.DiscreteSDF;boundary.settingsSDF.kSDFTexture=tex;boundary.settingsSDF.kSDFWorldSize=hi-lo;
 var go=new GameObject("ScalpHair");go.SetActive(false);go.transform.SetParent(proxy,false);var hair=go.AddComponent<HairInstance>();hair.settingsExecutive=old.settingsExecutive;hair.settingsExecutive.updateSimulationInEditor=false;hair.settingsExecutive.updateSimulation=true;hair.settingsExecutive.updateSimulationRate=HairInstance.SettingsExecutive.UpdateRate.Fixed30Hz;
 hair.strandGroupDefaults=old.strandGroupDefaults;hair.strandGroupDefaults.settingsGeometry.strandScale=HairSim.SettingsGeometry.StrandScale.Fixed;hair.strandGroupDefaults.settingsGeometry.strandLength=false;hair.strandGroupDefaults.settingsGeometry.strandDiameter=false;hair.strandGroupDefaults.settingsRendering.material=true;hair.strandGroupDefaults.settingsRendering.materialAsset=AssetDatabase.LoadAssetAtPath<Material>(Base+"Materials/Liora_StrandHair.mat");
 hair.settingsVolumetrics=old.settingsVolumetrics;hair.settingsEnvironment=old.settingsEnvironment;hair.settingsEnvironment.boundaryCapture=false;hair.settingsEnvironment.boundaryResident=new[]{boundary};hair.strandGroupProviders=new[]{new HairInstance.GroupProvider{hairAsset=asset}};HairInstanceBuilder.BuildHairInstance(hair,hair.strandGroupProviders);go.SetActive(true);
 var stats=new System.Collections.Generic.List<object>();foreach(var name in new[]{"Eyebrows","Eyelashes"}){
 var mesh=Facial(name,head,rig,config);var facial=new GameObject(name);facial.transform.SetParent(head,false);var renderer=facial.AddComponent<SkinnedMeshRenderer>();renderer.sharedMesh=mesh;renderer.bones=new[]{head};renderer.rootBone=head;renderer.updateWhenOffscreen=true;renderer.localBounds=mesh.bounds;
 var matName=name=="Eyebrows"?"Ethan_Eyebrows":"Ethan_EyelashStrands";var material=new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/_Sandbox/Characters/Ethan_V2/Materials/"+matName+".mat"));material.name="Liora_"+name;AssetDatabase.CreateAsset(material,Folder+material.name+".mat");renderer.sharedMaterial=material;
 stats.Add(new{name,mesh.vertexCount,triangles=mesh.triangles.Length/3,shapes=mesh.blendShapeCount,memoryBytes=UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(mesh)});
 }
 // The source eyelash cards overlap the new evaluated strand mesh; retain data
 // in the source FBX and disable only this candidate's redundant card renderer.
 root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r=>r.name=="BAKED_eyelashes_lod0_mesh").enabled=false;
 PrefabUtility.SaveAsPrefabAsset(root,Folder+"Liora_Candidate.prefab",out var success);if(!success)throw new Exception("Save failed");AssetDatabase.SaveAssets();
 var report=new{strands=group.strandCount,points=32,simulationHz=30,editModeSimulation=false,fixedScale=true,endpointErrorM=endpointError,collisionResolution=n,facialHair=stats,sourceCardsPreservedDisabled=true};File.WriteAllText(Data+"groom_install.json",JsonConvert.SerializeObject(report,Formatting.Indented));return report;
 }finally{PrefabUtility.UnloadPrefabContents(root);}
 }
}


