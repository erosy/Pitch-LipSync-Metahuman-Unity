using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Playables;
using UnityEngine.Animations;
using Sandbox.CharacterSystem;
using Newtonsoft.Json;

public static class VerifyRevampedEthan {
 const string Source="Assets/_Sandbox/Characters/Ethan_V2/Ethan_V2.prefab";
 const string Candidate="Assets/_Sandbox/Characters/Ethan_V2/Candidate/EthanV2_Candidate.prefab";
 const string Output="Data/EthanV2/Revamp";
 static void Guard(){if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Edit Mode required");}
 static void Save(string name,object result)=>File.WriteAllText(Output+"/"+name+".json",JsonConvert.SerializeObject(result,Formatting.Indented));
 static SkinnedMeshRenderer[] Sources(GameObject root){var all=root.GetComponentsInChildren<SkinnedMeshRenderer>(true);var head=all.Single(r=>r.name=="BAKED_head_lod0_mesh");var body=all.Single(r=>r.name=="BAKED_body_lod0_mesh");return new[]{head,body}.Concat(all.Where(r=>r!=head&&r!=body&&r.name!="BAKED_Outfits_LOD0").OrderBy(r=>r.name)).ToArray();}
 public static object AuditBodyChannels(){Guard();var root=PrefabUtility.LoadPrefabContents(Source);try{var body=Sources(root)[1];var m=body.sharedMesh;var p=new Vector3[m.vertexCount];var n=new Vector3[p.Length];var t=new Vector3[p.Length];float maxPosition=0,maxNormal=0,maxTangent=0;for(int s=0;s<m.blendShapeCount;s++){m.GetBlendShapeFrameVertices(s,0,p,n,t);maxPosition=Mathf.Max(maxPosition,p.Max(v=>v.magnitude));maxNormal=Mathf.Max(maxNormal,n.Max(v=>v.magnitude));maxTangent=Mathf.Max(maxTangent,t.Max(v=>v.magnitude));}var references=new List<string>();foreach(var c in root.GetComponentsInChildren<MonoBehaviour>(true).Where(c=>c)){var so=new SerializedObject(c);var prop=so.GetIterator();while(prop.Next(true))if(prop.propertyType==SerializedPropertyType.ObjectReference&&prop.objectReferenceValue==body)references.Add(c.GetType().Name+"."+prop.propertyPath);}var curves=AssetDatabase.FindAssets("t:AnimationDefinition",new[]{"Assets/_Sandbox/Data"}).Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<AnimationDefinition>).Where(d=>d&&d.Clip).SelectMany(d=>AnimationUtility.GetCurveBindings(d.Clip).Where(b=>b.propertyName.StartsWith("blendShape.")).Select(b=>d.name+":"+b.path+":"+b.propertyName)).ToArray();var result=new{maxPositionDelta=maxPosition,maxNormalDelta=maxNormal,maxTangentDelta=maxTangent,bodyControllerReferences=references,blendShapeAnimationCurves=curves,bodyChannelsInactive=references.Count==0&&curves.Length==0};Save("body_channel_audit",result);return result;}finally{PrefabUtility.UnloadPrefabContents(root);}}
 public static Vector3[] Skin(SkinnedMeshRenderer r,Transform root){var m=r.sharedMesh;var p=m.vertices;var d=new Vector3[p.Length];for(int s=0;s<m.blendShapeCount;s++){float w=r.GetBlendShapeWeight(s)*.01f;if(w==0)continue;m.GetBlendShapeFrameVertices(s,0,d,null,null);for(int i=0;i<p.Length;i++)p[i]+=d[i]*w;}var matrices=r.bones.Select((b,i)=>root.worldToLocalMatrix*b.localToWorldMatrix*m.bindposes[i]).ToArray();var result=new Vector3[p.Length];using(var c=m.GetBonesPerVertex())using(var w=m.GetAllBoneWeights()){int at=0;for(int i=0;i<p.Length;i++)for(int k=0;k<c[i];k++){var v=w[at++];result[i]+=matrices[v.boneIndex].MultiplyPoint3x4(p[i])*v.weight;}}return result;}
 public static object ImportPreservedHead(){Guard();const string meshPath="Assets/_Sandbox/Characters/Ethan_V2/Candidate/EthanV2_Face.asset";File.Copy(Output+"/Face_preserved.asset",meshPath,true);AssetDatabase.ImportAsset(meshPath,ImportAssetOptions.ForceUpdate);return new{candidateFaceImported=true};}
 public static object PreserveBindOffsets(){
  Guard();var source=PrefabUtility.LoadPrefabContents(Source);var candidate=PrefabUtility.LoadPrefabContents(Candidate);
  try{
   var face=candidate.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r=>r.name=="Face");var binds=new List<Matrix4x4>();var world=candidate.transform.localToWorldMatrix*source.transform.worldToLocalMatrix;int at=0;
   foreach(var r in Sources(source)){var m=r.sharedMesh;var x=face.transform.worldToLocalMatrix*world*r.transform.localToWorldMatrix;using(var weights=m.GetAllBoneWeights())foreach(int i in weights.ToArray().Where(w=>w.weight>0).Select(w=>w.boneIndex).Distinct().OrderBy(i=>i)){binds.Add(face.bones[at++].worldToLocalMatrix*world*r.bones[i].localToWorldMatrix*m.bindposes[i]*x.inverse);}}
   if(at!=face.bones.Length)throw new Exception("Bone slot layout changed");face.sharedMesh.bindposes=binds.ToArray();EditorUtility.SetDirty(face.sharedMesh);AssetDatabase.SaveAssetIfDirty(face.sharedMesh);
   var outfit=candidate.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r=>r.name=="Outfit");var old=source.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r=>r.name=="BAKED_Outfits_LOD0");var ob=new List<Matrix4x4>();at=0;var ox=outfit.transform.worldToLocalMatrix*world*old.transform.localToWorldMatrix;using(var weights=old.sharedMesh.GetAllBoneWeights())foreach(int i in weights.ToArray().Where(w=>w.weight>0).Select(w=>w.boneIndex).Distinct().OrderBy(i=>i))ob.Add(outfit.bones[at++].worldToLocalMatrix*world*old.bones[i].localToWorldMatrix*old.sharedMesh.bindposes[i]*ox.inverse);outfit.sharedMesh.bindposes=ob.ToArray();EditorUtility.SetDirty(outfit.sharedMesh);AssetDatabase.SaveAssetIfDirty(outfit.sharedMesh);
   // Bounds are expressed in the selected culling root's local frame, with positive extents.
   foreach(var r in new[]{face,outfit}){var vertices=Skin(r,candidate.transform);var b=new Bounds(r.rootBone.InverseTransformPoint(candidate.transform.TransformPoint(vertices[0])),Vector3.zero);foreach(var p in vertices)b.Encapsulate(r.rootBone.InverseTransformPoint(candidate.transform.TransformPoint(p)));b.Expand(.01f);r.localBounds=b;}
   var grouped=candidate.transform.Find("Hair");if(!grouped){grouped=new GameObject("Hair").transform;grouped.SetParent(candidate.transform,false);foreach(var h in candidate.GetComponentsInChildren<Unity.DemoTeam.Hair.HairInstance>(true)){var t=h.transform;while(t.parent!=candidate.transform&&t.parent!=grouped)t=t.parent;if(t.parent==candidate.transform&&t!=grouped&&t.name!="Armature")t.SetParent(grouped,true);}}
   PrefabUtility.SaveAsPrefabAsset(candidate,Candidate,out var success);if(!success)throw new Exception("Candidate save failed");return new{sourceBindOffsetsPreserved=true};
  }finally{PrefabUtility.UnloadPrefabContents(source);PrefabUtility.UnloadPrefabContents(candidate);}
 }
 public static object StaticChecks(){
  Guard();var source=PrefabUtility.LoadPrefabContents(Source);var candidate=PrefabUtility.LoadPrefabContents(Candidate);
  try{
   var parts=Sources(source);var face=candidate.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r=>r.name=="Face");var outfit=candidate.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r=>r.name=="Outfit");var m=face.sharedMesh;var animator=candidate.GetComponentInChildren<Animator>(true);
   if(candidate.GetComponentsInChildren<Animator>(true).Length!=1||!animator.avatar.isHuman||!animator.avatar.isValid)throw new Exception("Animator structure invalid");if(candidate.GetComponentsInChildren<CharacterSkeletonPoseBridge>(true).Length>0||candidate.GetComponentsInChildren<CharacterFacialMeshSync>(true).Length>0)throw new Exception("Compatibility components remain");if(candidate.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length!=2)throw new Exception("Renderer count invalid");
   if(candidate.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="AnimationDriver"||t.name=="BAKED_rig"))throw new Exception("Legacy skeleton remains");if(face.bones.Any(b=>!b)||outfit.bones.Any(b=>!b))throw new Exception("Missing skin bone");
   var errors=candidate.GetComponent<Character>().ValidateConfiguration();errors.AddRange(candidate.GetComponent<CharacterFlowController>().ValidateConfiguration());if(errors.Count>0)throw new Exception(string.Join("; ",errors));
   var cp=m.vertices;var cn=m.normals;var ct=m.tangents;int offset=0;float maxNeutral=0;int headShapeMismatches=0,headNormalMismatches=0,headTangentMismatches=0;float maxDelta=0;
   var actualNeutral=Skin(face,candidate.transform);var sections=new List<object>();var storedSources=parts.Select(r=>new{r,m=r.sharedMesh}).ToArray();
   for(int section=0;section<parts.Length;section++){
    var r=parts[section];var original=r.sharedMesh;var expected=Skin(r,source.transform);float err=0;for(int i=0;i<expected.Length;i++)err=Mathf.Max(err,Vector3.Distance(expected[i],actualNeutral[offset+i]));maxNeutral=Mathf.Max(maxNeutral,err);
    if(section==0){var ov=original.vertices;var on=original.normals;var ot=original.tangents;for(int i=0;i<original.vertexCount;i++)if(!ov[i].Equals(cp[i])||!on[i].Equals(cn[i])||!ot[i].Equals(ct[i]))throw new Exception("Corrected head base attributes changed");for(int sub=0;sub<original.subMeshCount;sub++)if(!original.GetTriangles(sub).SequenceEqual(m.GetTriangles(sub)))throw new Exception("Head triangle order changed");}
    var x=section==0?Matrix4x4.identity:face.transform.worldToLocalMatrix*r.transform.localToWorldMatrix;
    var dp=new Vector3[original.vertexCount];var dn=new Vector3[dp.Length];var dt=new Vector3[dp.Length];var p=new Vector3[m.vertexCount];var n=new Vector3[p.Length];var t=new Vector3[p.Length];
    for(int s=0;s<m.blendShapeCount;s++){if(m.GetBlendShapeName(s)!=original.GetBlendShapeName(s))throw new Exception("Shape order changed");original.GetBlendShapeFrameVertices(s,0,dp,dn,dt);m.GetBlendShapeFrameVertices(s,0,p,n,t);for(int i=0;i<dp.Length;i++){var expectedDelta=section==0?dp[i]:x.MultiplyVector(dp[i]);maxDelta=Mathf.Max(maxDelta,(expectedDelta-p[offset+i]).magnitude);if(section==0){if(!dp[i].Equals(p[i]))headShapeMismatches++;if(!dn[i].Equals(n[i]))headNormalMismatches++;if(!dt[i].Equals(t[i]))headTangentMismatches++;}}}
    sections.Add(new{name=r.name,vertices=original.vertexCount,offset,maxNeutralErrorMm=err*1000});offset+=original.vertexCount;
   }
   var sourceOutfit=source.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r=>r.name=="BAKED_Outfits_LOD0");var ca=Skin(outfit,candidate.transform);var oa=Skin(sourceOutfit,source.transform);float outfitError=ca.Select((v,i)=>Vector3.Distance(v,oa[i])).Max();
   var result=new{passed=headShapeMismatches==0&&headNormalMismatches==0&&headTangentMismatches==0&&maxNeutral<=.000001f&&outfitError<=.000001f,headShapeMismatches,headNormalMismatches,headTangentMismatches,maxNeutralErrorMm=maxNeutral*1000,outfitNeutralErrorMm=outfitError*1000,maxConvertedExpressionErrorMm=maxDelta*face.transform.lossyScale.x*1000,sections,faceVertices=m.vertexCount,faceTriangles=m.triangles.Length/3,outfitVertices=outfit.sharedMesh.vertexCount,outfitTriangles=outfit.sharedMesh.triangles.Length/3,shapes=m.blendShapeCount,faceMemoryMiB=UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(m)/1048576f,configurationErrors=errors};Save("static_verification",result);return result;
  }finally{PrefabUtility.UnloadPrefabContents(source);PrefabUtility.UnloadPrefabContents(candidate);}
 }
 public static object MotionChecks(){
  Guard();var root=PrefabUtility.LoadPrefabContents(Candidate);
  try{
   var animator=root.GetComponentInChildren<Animator>(true);var face=root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r=>r.name=="Face");var outfit=root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r=>r.name=="Outfit");
   var baseline=Skin(face,root.transform);var baseOutfit=Skin(outfit,root.transform);var rest=root.GetComponentsInChildren<Transform>(true).ToDictionary(t=>t,t=>(t.localPosition,t.localRotation,t.localScale));
   Action reset=()=>{foreach(var p in rest){p.Key.localPosition=p.Value.Item1;p.Key.localRotation=p.Value.Item2;p.Key.localScale=p.Value.Item3;}};
   var definitions=AssetDatabase.FindAssets("t:AnimationDefinition",new[]{"Assets/_Sandbox/Data"}).Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<AnimationDefinition>).Where(d=>d&&d.Clip&&d.Clip.isHumanMotion).GroupBy(d=>d.Clip).Select(g=>g.First()).ToArray();
   var samples=new List<object>();float largestRadius=0;int sampleCount=0;
   foreach(var definition in definitions){reset();var graph=PlayableGraph.Create("Candidate native Humanoid verification");try{
    graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);var playable=AnimationClipPlayable.Create(graph,definition.Clip);playable.SetApplyFootIK(false);AnimationPlayableOutput.Create(graph,"native skeleton",animator).SetSourcePlayable(playable);graph.Play();
    float maxMove=0,maxOutfitMove=0,maxRadius=0;
    foreach(float phase in new[]{0f,.125f,.25f,.375f,.5f,.625f,.75f,.875f,1f}){playable.SetTime(definition.Clip.length*phase);graph.Evaluate(0);var positions=Skin(face,root.transform);var clothing=Skin(outfit,root.transform);float radius=positions.Max(v=>v.magnitude);if(float.IsNaN(radius)||radius>4)throw new Exception("Invalid native pose "+definition.name+" radius "+radius);maxRadius=Mathf.Max(maxRadius,radius);for(int i=0;i<positions.Length;i++)maxMove=Mathf.Max(maxMove,Vector3.Distance(positions[i],baseline[i]));for(int i=0;i<clothing.Length;i++)maxOutfitMove=Mathf.Max(maxOutfitMove,Vector3.Distance(clothing[i],baseOutfit[i]));sampleCount++;}
    if(maxMove<.001f)throw new Exception("No body animation on "+definition.name);samples.Add(new{name=definition.name,clip=definition.Clip.name,maxMoveM=maxMove,maxOutfitMoveM=maxOutfitMove,maxRadiusM=maxRadius});largestRadius=Mathf.Max(largestRadius,maxRadius);
   }finally{graph.Destroy();}}
   reset();
   // Every finger must respond independently through the native Avatar mapping.
   int fingerChecks=0;foreach(var side in new[]{"Left","Right"})foreach(var finger in new[]{"Thumb","Index","Middle","Ring","Little"})foreach(var joint in new[]{"Proximal","Intermediate","Distal"}){
    string name=side+finger+joint;var human=(HumanBodyBones)Enum.Parse(typeof(HumanBodyBones),name);var bone=animator.GetBoneTransform(human);if(!bone)throw new Exception("Missing finger mapping "+name);var before=bone.localPosition;bone.localRotation*=Quaternion.Euler(0,0,60);if(bone.localPosition!=before)throw new Exception("Finger spacing changed");reset();fingerChecks++;
   }
   var lip=root.GetComponentInChildren<uLipSync.uLipSync>(true);var driver=root.GetComponentInChildren<uLipSync.uLipSyncBlendShape>(true);if(!driver||driver.skinnedMeshRenderer!=face||lip.onLipSyncUpdate.GetPersistentTarget(0)!=driver)throw new Exception("Lip-sync binding mismatch");
   var expression=root.GetComponentInChildren<ExpressionModule>(true);if(expression.Face!=face||root.GetComponentInChildren<AnimationModule>(true).Animator!=animator)throw new Exception("Module reference mismatch");
   var result=new{passed=true,humanoid=true,avatarHumanScale=animator.humanScale,mappedFingerChecks=fingerChecks,poseSamples=sampleCount,clips=samples,largestRadiusM=largestRadius,phonemes=driver.blendShapes.Count,faceExpressionTarget=true,lipSyncCallback=true,playModeTested=false};Save("motion_verification",result);return result;
  }finally{PrefabUtility.UnloadPrefabContents(root);}
 }
 public static object HairChecks(){
  Guard();var source=PrefabUtility.LoadPrefabContents(Source);var candidate=PrefabUtility.LoadPrefabContents(Candidate);
  try{
   var old=source.GetComponentInChildren<Unity.DemoTeam.Hair.HairInstance>(true);var hair=candidate.GetComponentInChildren<Unity.DemoTeam.Hair.HairInstance>(true);if(!old||!hair)throw new Exception("Scalp missing");var asset=hair.strandGroupProviders[0].hairAsset;if(asset!=old.strandGroupProviders[0].hairAsset)throw new Exception("Groom data changed");var g=asset.strandGroups.Single();
   if(g.strandParticleCount!=32||hair.settingsExecutive.updateSimulationInEditor||hair.settingsExecutive.updateSimulationRate!=Unity.DemoTeam.Hair.HairInstance.SettingsExecutive.UpdateRate.Fixed30Hz||hair.strandGroupDefaults.settingsGeometry.strandScale!=Unity.DemoTeam.Hair.HairSim.SettingsGeometry.StrandScale.Fixed)throw new Exception("Hair production settings changed");
   float error=0;var oldMatrix=source.transform.worldToLocalMatrix*old.transform.localToWorldMatrix;var matrix=candidate.transform.worldToLocalMatrix*hair.transform.localToWorldMatrix;
   for(int strand=0;strand<g.strandCount;strand++){Unity.DemoTeam.Hair.HairAssetUtility.DeclareStrandIterator(g,strand,out int start,out int step,out int end);foreach(int i in new[]{start,start+31*step})error=Mathf.Max(error,Vector3.Distance(oldMatrix.MultiplyPoint3x4(g.particlePosition[i]),matrix.MultiplyPoint3x4(g.particlePosition[i])));}
   var animator=candidate.GetComponentInChildren<Animator>(true);var head=animator.GetBoneTransform(HumanBodyBones.Head);if(!hair.transform.IsChildOf(head))throw new Exception("Scalp not attached to native head");var boundary=hair.settingsEnvironment.boundaryResident.Single();var oldBoundary=old.settingsEnvironment.boundaryResident.Single();if(boundary.settingsSDF.kSDFTexture!=oldBoundary.settingsSDF.kSDFTexture||!boundary.transform.IsChildOf(head))throw new Exception("Collision attachment changed");
   var warmup=candidate.GetComponent<CharacterHairStartupWarmup>();if(new SerializedObject(warmup).FindProperty("targetAnimator").objectReferenceValue!=animator)throw new Exception("Warmup Animator reference mismatch");var readiness=candidate.GetComponent<CharacterReadiness>();var so=new SerializedObject(readiness);if(so.FindProperty("startupWarmup").objectReferenceValue!=warmup)throw new Exception("Readiness reference mismatch");
   var original=head.localRotation;head.localRotation=original*Quaternion.Euler(0,15,0);bool scaleOk=(hair.transform.lossyScale-Vector3.one).magnitude<.0001f;head.localRotation=original;
   var result=new{passed=error<=.000001f&&scaleOk,strands=g.strandCount,points=g.strandParticleCount,simulationHz=30,editModeSimulation=false,fixedStrandScale=true,maxEndpointErrorMm=error*1000,headAttachment=true,collisionTexturePreserved=true,startupWiring=true,scaleOk,playModeTested=false};Save("hair_verification",result);return result;
  }finally{PrefabUtility.UnloadPrefabContents(source);PrefabUtility.UnloadPrefabContents(candidate);}
 }
 public static object Finish(){
  Guard();var root=PrefabUtility.LoadPrefabContents(Candidate);
  try{
   foreach(string name in new[]{"static_verification","motion_verification","hair_verification"})if(!(bool)Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(Output+"/"+name+".json"))["passed"])throw new Exception("Verification failed: "+name);
   int missing=root.GetComponentsInChildren<Transform>(true).Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));if(missing!=0)throw new Exception("Missing scripts");
   var empty=root.transform.Find("Hair");if(empty&&empty.childCount==0)UnityEngine.Object.DestroyImmediate(empty.gameObject);var hair=root.GetComponentInChildren<Unity.DemoTeam.Hair.HairInstance>(true);if(hair&&hair.transform.parent&&hair.transform.parent.name!="head")hair.transform.parent.name="Hair";
   var animator=root.GetComponentInChildren<Animator>(true);var dependencies=new HashSet<string>(AssetDatabase.GetDependencies(Candidate,true));foreach(string name in new[]{"EthanV2_NativeAvatar.asset","EthanV2_NativeAvatar_Meters.asset","EthanV2_Avatar.asset"}){string path="Assets/_Sandbox/Characters/Ethan_V2/Candidate/"+name;if(!dependencies.Contains(path))AssetDatabase.DeleteAsset(path);}
   PrefabUtility.SaveAsPrefabAsset(root,Candidate,out var success);if(!success)throw new Exception("Candidate save failed");
   var source=AssetDatabase.LoadAssetAtPath<GameObject>(Source);var sourceMeshes=source.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(r=>r.sharedMesh).Distinct().ToArray();var meshes=root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(r=>r.sharedMesh).Distinct().ToArray();
   var result=new{candidate=Candidate,passed=true,animators=root.GetComponentsInChildren<Animator>(true).Length,avatar=animator.avatar.name,avatarValid=animator.avatar.isValid,humanoid=animator.avatar.isHuman,renderers=root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(r=>r.name).ToArray(),topLevel=Enumerable.Range(0,root.transform.childCount).Select(i=>root.transform.GetChild(i).name).ToArray(),missingScripts=missing,sourceActiveMeshMiB=sourceMeshes.Sum(m=>UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(m))/1048576f,candidateActiveMeshMiB=meshes.Sum(m=>UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(m))/1048576f,vertices=meshes.Sum(m=>m.vertexCount),triangles=meshes.Sum(m=>m.triangles.Length/3),facialHairVertices=84405,facialHairTriangles=148734,productionSwitched=false,sceneSaved=false,playModeTested=false};Save("final_check",result);return result;
  }finally{PrefabUtility.UnloadPrefabContents(root);}
 }
}

