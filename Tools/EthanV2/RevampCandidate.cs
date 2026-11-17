using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Newtonsoft.Json;
using Unity.Collections;
using Sandbox.CharacterSystem;

public static class RevampEthanV2Candidate {
 public const string Source="Assets/_Sandbox/Characters/Ethan_V2/Ethan_V2.prefab";
 public const string Folder="Assets/_Sandbox/Characters/Ethan_V2/Candidate";
 public const string Report="Data/EthanV2/Revamp";
 static void Guard(){if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Edit Mode required; user controls Play Mode");Directory.CreateDirectory(Report);}
 static int Depth(Transform t){int n=0;while(t.parent){n++;t=t.parent;}return n;}
 static void Ref(UnityEngine.Object obj,string property,UnityEngine.Object value){var so=new SerializedObject(obj);var p=so.FindProperty(property);if(p==null)throw new Exception("Missing property "+property);p.objectReferenceValue=value;so.ApplyModifiedPropertiesWithoutUndo();}
 static void Save(string name,object data)=>File.WriteAllText(Report+"/"+name+".json",JsonConvert.SerializeObject(data,Formatting.Indented));
 static Transform Top(Transform t,Transform root){while(t.parent!=root)t=t.parent;return t;}
 static Transform CopyBone(Transform original,Transform parent,string name=null){var t=new GameObject(name??original.name).transform;t.SetPositionAndRotation(original.position,original.rotation);t.localScale=original.lossyScale;t.SetParent(parent,true);return t;}

 public static object Build(){
  Guard();const string path=Folder+"/EthanV2_Candidate.prefab";if(AssetDatabase.LoadAssetAtPath<GameObject>(path))throw new Exception("Candidate exists; preserve it before rebuilding");var avatar=AssetDatabase.LoadAssetAtPath<Avatar>(Folder+"/EthanV2_NativeAvatar.asset");if(!avatar||!avatar.isHuman||!avatar.isValid)throw new Exception("Native avatar gate required");
  var scene=EditorSceneManager.NewPreviewScene();var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Source),scene);
  try{
   PrefabUtility.UnpackPrefabInstance(root,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
   root.SetActive(false);
   foreach(var t in root.GetComponentsInChildren<Transform>(true).Where(t=>t!=root.transform&&PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)).OrderBy(Depth).ToArray())if(t&&PrefabUtility.IsPartOfPrefabInstance(t.gameObject))PrefabUtility.UnpackPrefabInstance(t.gameObject,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
   var skins=root.GetComponentsInChildren<SkinnedMeshRenderer>(true);var head=skins.Single(r=>r.name=="BAKED_head_lod0_mesh");var body=skins.Single(r=>r.name=="BAKED_body_lod0_mesh");var outfit=skins.Single(r=>r.name=="BAKED_Outfits_LOD0");
   var bodyRig=Top(body.bones[0],root.transform);var headRig=Top(head.bones[0],root.transform);var rigify=root.transform.Find("BAKED_rig");var driver=root.transform.Find("AnimationDriver");
   var bodyBones=bodyRig.GetComponentsInChildren<Transform>(true).ToDictionary(t=>t.name);var remap=new Dictionary<UnityEngine.Object,UnityEngine.Object>();var boneMap=bodyRig.GetComponentsInChildren<Transform>(true).ToDictionary(t=>t,t=>t);
   // Consolidate facial deformation bones under the native body. Shared torso bones
   // use recomputed per-renderer bind poses, not assumed matching rest positions.
   boneMap[headRig]=bodyRig;remap[headRig]=bodyRig;remap[headRig.gameObject]=bodyRig.gameObject;
   var headNeeded=new HashSet<Transform>();foreach(var b in skins.SelectMany(r=>r.bones).Where(b=>b&&Top(b,root.transform)==headRig))for(var t=b;t&&t!=headRig;t=t.parent)headNeeded.Add(t);
   foreach(var t in headNeeded.OrderBy(Depth)){
    Transform mapped;if(bodyBones.TryGetValue(t.name,out mapped))boneMap[t]=mapped;
    else boneMap[t]=CopyBone(t,boneMap[t.parent]);
    remap[t]=boneMap[t];remap[t.gameObject]=boneMap[t].gameObject;
   }
   var bridge=root.GetComponent<CharacterSkeletonPoseBridge>();
   var nativeNames=HumanNames();var oldImporter=(ModelImporter)AssetImporter.GetAtPath("Assets/_Sandbox/Characters/Ethan/Ethan_unity.fbx");var oldRoles=oldImporter.humanDescription.human.ToDictionary(h=>h.boneName,h=>h.humanName.Replace(" ",""));
   var clothingMaps=new List<object>();var weightedOutfit=new HashSet<int>();using(var ws=outfit.sharedMesh.GetAllBoneWeights())foreach(var w in ws)if(w.weight>0)weightedOutfit.Add(w.boneIndex);
   foreach(int index in weightedOutfit.OrderBy(i=>i)){
    var old=outfit.bones[index];if(boneMap.ContainsKey(old))continue;if(Top(old,root.transform)!=rigify)throw new Exception("Unexpected clothing skeleton");
    var binding=bridge.Bindings.Single(b=>b.target==old);var source=binding.source;string role=null;Transform parent=null;
    if(source.name=="Neck2")parent=bodyBones["neck_02"];
    for(var t=source;!parent&&t&&t!=driver;t=t.parent)if(oldRoles.TryGetValue(t.name,out role)&&nativeNames.TryGetValue(role,out var native)&&bodyBones.TryGetValue(native,out parent))break;
    if(!parent)throw new Exception("No anatomical source mapping for "+old.name);
    var helper=CopyBone(old,parent,"OutfitBind_"+old.name.Replace('.','_'));boneMap[old]=helper;remap[old]=helper;remap[old.gameObject]=helper.gameObject;clothingMaps.Add(new{old=old.name,source=source.name,parent=parent.name,helper=helper.name});
   }
   var faceParts=new[]{head,body}.Concat(skins.Where(r=>r!=head&&r!=body&&r!=outfit).OrderBy(r=>r.name)).ToArray();
   var faceGo=new GameObject("Face");faceGo.transform.SetParent(root.transform,false);faceGo.transform.SetPositionAndRotation(head.transform.position,head.transform.rotation);faceGo.transform.localScale=head.transform.lossyScale;faceGo.layer=head.gameObject.layer;var face=faceGo.AddComponent<SkinnedMeshRenderer>();EditorUtility.CopySerialized(head,face);
   var mesh=MergeFace(faceParts,face.transform,boneMap,out var faceBones,out var faceMaterials);
   AssetDatabase.DeleteAsset(Folder+"/EthanV2_Face.asset");AssetDatabase.CreateAsset(mesh,Folder+"/EthanV2_Face.asset");face.sharedMesh=mesh;face.bones=faceBones;face.sharedMaterials=faceMaterials;face.rootBone=bodyRig;face.localBounds=head.localBounds;face.updateWhenOffscreen=true;
   foreach(var r in faceParts){remap[r]=face;remap[r.gameObject]=faceGo;remap[r.transform]=face.transform;}
   // Hair remains attached to head skin submesh 0: source head indices stay first.
   remap[head.sharedMesh]=mesh;
   var clothing=UnityEngine.Object.Instantiate(outfit.sharedMesh);clothing.name="EthanV2_Outfit";Rebind(clothing,outfit,boneMap,out var outfitBones);AssetDatabase.DeleteAsset(Folder+"/EthanV2_Outfit.asset");AssetDatabase.CreateAsset(clothing,Folder+"/EthanV2_Outfit.asset");outfit.sharedMesh=clothing;outfit.bones=outfitBones;outfit.rootBone=bodyRig;outfit.name="Outfit";
   var oldAnimators=root.GetComponentsInChildren<Animator>(true);var animator=bodyRig.gameObject.AddComponent<Animator>();animator.avatar=avatar;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
   foreach(var old in oldAnimators)remap[old]=animator;
   // Copy user components off mesh objects which will be removed. Never rewrite
   // Unity's native m_GameObject/m_Script/prefab identity references.
   foreach(var r in faceParts)foreach(var c in r.GetComponents<MonoBehaviour>().Where(c=>c)){
    var copy=faceGo.AddComponent(c.GetType());EditorUtility.CopySerialized(c,copy);remap[c]=copy;
   }
   foreach(var component in root.GetComponentsInChildren<Component>(true).Where(c=>c&&!(c is Transform)&&!(c is Renderer)&&!(c is CharacterSkeletonPoseBridge)&&!(c is CharacterFacialMeshSync))){var so=new SerializedObject(component);var p=so.GetIterator();while(p.Next(true))if(!p.propertyPath.StartsWith("m_")&&p.propertyType==SerializedPropertyType.ObjectReference&&p.objectReferenceValue&&remap.TryGetValue(p.objectReferenceValue,out var replacement))p.objectReferenceValue=replacement;so.ApplyModifiedPropertiesWithoutUndo();}
   // Preserve non-bone children (voice, collisions, groom attachment objects).
   foreach(var pair in boneMap.Where(p=>p.Key!=p.Value).OrderBy(p=>Depth(p.Key)).ToArray())foreach(var child in Enumerable.Range(0,pair.Key.childCount).Select(i=>pair.Key.GetChild(i)).Where(t=>!boneMap.ContainsKey(t)).ToArray())if(!faceParts.Any(r=>r.transform==child))child.SetParent(pair.Value,true);
   foreach(var c in root.GetComponents<CharacterFacialMeshSync>())UnityEngine.Object.DestroyImmediate(c);UnityEngine.Object.DestroyImmediate(bridge);foreach(var a in oldAnimators)if(a)UnityEngine.Object.DestroyImmediate(a);
   foreach(var r in faceParts)if(r)UnityEngine.Object.DestroyImmediate(r.gameObject);
   foreach(var t in new[]{headRig,rigify,driver})if(t)UnityEngine.Object.DestroyImmediate(t.gameObject);
   bodyRig.name="Armature";root.name="EthanV2_Candidate";
   Ref(root.GetComponentInChildren<AnimationModule>(true),"animator",animator);Ref(root.GetComponent<CharacterHairStartupWarmup>(),"targetAnimator",animator);
   var bounds=new Bounds();bool first=true;foreach(var r in root.GetComponentsInChildren<SkinnedMeshRenderer>(true)){foreach(var p in r.sharedMesh.vertices){var v=r.transform.TransformPoint(p);if(first){bounds=new Bounds(v,Vector3.zero);first=false;}else bounds.Encapsulate(v);}}face.localBounds=new Bounds(face.transform.InverseTransformPoint(bounds.center),face.transform.InverseTransformVector(bounds.size));
   root.SetActive(true);var errors=root.GetComponent<Character>().ValidateConfiguration();errors.AddRange(root.GetComponent<CharacterFlowController>().ValidateConfiguration());if(errors.Count>0)throw new Exception(string.Join("; ",errors));
   var saved=PrefabUtility.SaveAsPrefabAsset(root,path,out var success);if(!success||!saved)throw new Exception("Candidate prefab save failed");Save("build",new{path,avatar=avatar.name,faceVertices=mesh.vertexCount,faceTriangles=mesh.triangles.Length/3,shapes=mesh.blendShapeCount,materialSlots=faceMaterials.Length,outfitVertices=clothing.vertexCount,clothingMaps,faceBones=faceBones.Length,oneArmature=true,productionChanged=false,sceneSaved=false});return new{path,mesh.vertexCount,shapes=mesh.blendShapeCount};
  }finally{if(root)UnityEngine.Object.DestroyImmediate(root);EditorSceneManager.ClosePreviewScene(scene);}
 }

 static void Rebind(Mesh mesh,SkinnedMeshRenderer source,Dictionary<Transform,Transform> map,out Transform[] bones){
  using(var ws=source.sharedMesh.GetAllBoneWeights())using(var cs=source.sharedMesh.GetBonesPerVertex()){
   var ids=ws.ToArray().Where(w=>w.weight>0).Select(w=>w.boneIndex).Distinct().OrderBy(i=>i).ToArray();var indices=ids.Select((v,i)=>(v,i)).ToDictionary(p=>p.v,p=>p.i);var mappedBones=ids.Select(i=>map[source.bones[i]]).ToArray();bones=mappedBones;mesh.bindposes=ids.Select((i,j)=>mappedBones[j].worldToLocalMatrix*source.bones[i].localToWorldMatrix*source.sharedMesh.bindposes[i]).ToArray();
   var weights=ws.ToArray();for(int i=0;i<weights.Length;i++)if(weights[i].weight>0)weights[i].boneIndex=indices[weights[i].boneIndex];else weights[i].boneIndex=0;
   using(var c=new NativeArray<byte>(cs.ToArray(),Allocator.Temp))using(var w=new NativeArray<BoneWeight1>(weights,Allocator.Temp))mesh.SetBoneWeights(c,w);
  }
 }

 static Mesh MergeFace(SkinnedMeshRenderer[] sources,Transform target,Dictionary<Transform,Transform> map,out Transform[] bones,out Material[] materials){
  var result=new Mesh{name="EthanV2_Face",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};var positions=new List<Vector3>();var normals=new List<Vector3>();var tangents=new List<Vector4>();var uv=new List<Vector4>[8];for(int i=0;i<8;i++)uv[i]=new List<Vector4>();var colors=new List<Color32>();bool anyColors=sources.Any(r=>r.sharedMesh.colors32.Length>0);var triangles=new List<int[]>();var mats=new List<Material>();var bs=new List<Transform>();var binds=new List<Matrix4x4>();var counts=new List<byte>();var weights=new List<BoneWeight1>();var offsets=new List<int>();var matrices=new List<Matrix4x4>();
  for(int section=0;section<sources.Length;section++){
   var r=sources[section];var m=r.sharedMesh;int at=positions.Count;offsets.Add(at);var x=section==0?Matrix4x4.identity:target.worldToLocalMatrix*r.transform.localToWorldMatrix;matrices.Add(x);var nx=x.inverse.transpose;
   positions.AddRange(m.vertices.Select(v=>x.MultiplyPoint3x4(v)));normals.AddRange(m.normals.Select(v=>nx.MultiplyVector(v).normalized));tangents.AddRange(m.tangents.Select(v=>{var t=x.MultiplyVector(v).normalized;return new Vector4(t.x,t.y,t.z,v.w);}));
   if(section==0){positions.Clear();positions.AddRange(m.vertices);normals.Clear();normals.AddRange(m.normals);tangents.Clear();tangents.AddRange(m.tangents);}
   for(int channel=0;channel<8;channel++){var values=new List<Vector4>();m.GetUVs(channel,values);uv[channel].AddRange(values.Count==0?Enumerable.Repeat(Vector4.zero,m.vertexCount):values);}
   if(anyColors)colors.AddRange(m.colors32.Length==0?Enumerable.Repeat(new Color32(255,255,255,255),m.vertexCount):m.colors32);
   for(int sub=0;sub<m.subMeshCount;sub++){triangles.Add(m.GetTriangles(sub).Select(i=>i+at).ToArray());mats.Add(r.sharedMaterials[sub]);}
   using(var ws=m.GetAllBoneWeights())using(var cs=m.GetBonesPerVertex()){
    var used=ws.ToArray().Where(w=>w.weight>0).Select(w=>w.boneIndex).Distinct().OrderBy(i=>i).ToArray();var local=new Dictionary<int,int>();foreach(int i in used){if(!map.TryGetValue(r.bones[i],out var bone))throw new Exception("Unmapped weighted face bone "+r.bones[i].name);local[i]=bs.Count;bs.Add(bone);binds.Add(bone.worldToLocalMatrix*r.bones[i].localToWorldMatrix*m.bindposes[i]*x.inverse);}
    counts.AddRange(cs.ToArray());foreach(var w in ws){var v=w;v.boneIndex=v.weight>0?local[v.boneIndex]:0;weights.Add(v);}
   }
  }
  result.vertices=positions.ToArray();result.normals=normals.ToArray();result.tangents=tangents.ToArray();for(int c=0;c<8;c++)if(uv[c].Any(v=>v!=Vector4.zero))result.SetUVs(c,uv[c]);if(anyColors)result.colors32=colors.ToArray();result.bindposes=binds.ToArray();using(var c=new NativeArray<byte>(counts.ToArray(),Allocator.Temp))using(var w=new NativeArray<BoneWeight1>(weights.ToArray(),Allocator.Temp))result.SetBoneWeights(c,w);result.subMeshCount=triangles.Count;for(int i=0;i<triangles.Count;i++)result.SetTriangles(triangles[i],i);
  for(int s=0;s<sources[0].sharedMesh.blendShapeCount;s++){
   string name=sources[0].sharedMesh.GetBlendShapeName(s);var p=new Vector3[positions.Count];var n=new Vector3[p.Length];var t=new Vector3[p.Length];
   for(int section=0;section<sources.Length;section++){if(section==1)continue;var m=sources[section].sharedMesh;int shape=m.GetBlendShapeIndex(name);if(shape<0)throw new Exception("Missing expression "+name+" on "+m.name);var dp=new Vector3[m.vertexCount];var dn=new Vector3[dp.Length];var dt=new Vector3[dp.Length];m.GetBlendShapeFrameVertices(shape,0,dp,dn,dt);var x=matrices[section];var nx=x.inverse.transpose;for(int i=0;i<dp.Length;i++){int j=offsets[section]+i;p[j]=section==0?dp[i]:x.MultiplyVector(dp[i]);n[j]=section==0?dn[i]:nx.MultiplyVector(dn[i]);t[j]=section==0?dt[i]:x.MultiplyVector(dt[i]);}}
   result.AddBlendShapeFrame(name,sources[0].sharedMesh.GetBlendShapeFrameWeight(s,0),p,n,t);
  }
  result.RecalculateBounds();bones=bs.ToArray();materials=mats.ToArray();Save("sections",sources.Select((r,i)=>new{name=r.name,source=AssetDatabase.GetAssetPath(r.sharedMesh),offset=offsets[i],vertices=r.sharedMesh.vertexCount,matrix=matrices[i].ToString("G9")}).ToArray());return result;
 }
 public static Dictionary<string,string> HumanNames(){
  var map=new Dictionary<string,string>{{"Hips","pelvis"},{"Spine","spine_01"},{"Chest","spine_03"},{"UpperChest","spine_05"},{"Neck","neck_01"},{"Head","head"}};
  foreach(var side in new[]{("Left","l"),("Right","r")}){
   foreach(var p in new[]{("Shoulder","clavicle"),("UpperArm","upperarm"),("LowerArm","lowerarm"),("Hand","hand"),("UpperLeg","thigh"),("LowerLeg","calf"),("Foot","foot"),("Toes","ball")})map[side.Item1+p.Item1]=p.Item2+"_"+side.Item2;
   foreach(var finger in new[]{("Thumb","thumb"),("Index","index"),("Middle","middle"),("Ring","ring"),("Little","pinky")})for(int i=1;i<=3;i++)map[side.Item1+finger.Item1+new[]{"Proximal","Intermediate","Distal"}[i-1]]=finger.Item2+"_0"+i+"_"+side.Item2;
  }return map;
 }
 public static object RebuildMeterRig(){
  Guard();const string path=Folder+"/EthanV2_Candidate.prefab";var source=PrefabUtility.LoadPrefabContents(Source);var root=PrefabUtility.LoadPrefabContents(path);GameObject avatarRig=null;
  try{
   var rig=root.transform.Find("Armature");var animator=rig.GetComponent<Animator>();root.SetActive(false);animator.enabled=false;animator.avatar=null;
   var originalSkins=source.GetComponentsInChildren<SkinnedMeshRenderer>(true);var originalBody=originalSkins.Single(r=>r.name=="BAKED_body_lod0_mesh");var originalHead=originalSkins.Single(r=>r.name=="BAKED_head_lod0_mesh");var bodyRig=Top(originalBody.bones[0],source.transform);var headRig=Top(originalHead.bones[0],source.transform);var rigify=source.transform.Find("BAKED_rig");
   var body=bodyRig.GetComponentsInChildren<Transform>(true).ToDictionary(t=>t.name);var head=headRig.GetComponentsInChildren<Transform>(true).GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.First());var outfitMap=Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(Report+"/build.json"))["clothingMaps"].ToDictionary(p=>(string)p["helper"],p=>(string)p["old"]);
   var native=new Dictionary<Transform,Transform>();var all=rig.GetComponentsInChildren<Transform>(true).OrderBy(Depth).ToArray();var boneSet=new HashSet<Transform>();foreach(var b in root.GetComponentsInChildren<SkinnedMeshRenderer>(true).SelectMany(r=>r.bones))for(var t=b;t&&t!=root.transform;t=t.parent)boneSet.Add(t);
   rig.localRotation=Quaternion.identity;rig.localScale=Vector3.one;
   foreach(var t in all.Where(t=>t!=rig)){
    Transform old=null;if(outfitMap.TryGetValue(t.name,out var oldName))old=rigify.GetComponentsInChildren<Transform>(true).Single(b=>b.name==oldName);else if(!body.TryGetValue(t.name,out old))head.TryGetValue(t.name,out old);
    if(boneSet.Contains(t)){if(!old)throw new Exception("Missing source rest transform "+t.name);native[t]=old;t.SetPositionAndRotation(old.position,old.rotation);t.localScale=Vector3.one;}
    else {Transform originalParent=null;Transform attachment=null;if(head.TryGetValue(t.parent.name,out originalParent))attachment=originalParent.Find(t.name);if(!attachment&&body.TryGetValue(t.parent.name,out originalParent))attachment=originalParent.Find(t.name);if(attachment){t.SetPositionAndRotation(attachment.position,attachment.rotation);var s=t.parent.lossyScale;var desired=attachment.lossyScale;t.localScale=new Vector3(desired.x/s.x,desired.y/s.y,desired.z/s.z);}}
   }
   // Avatar construction runs on a disposable clone with no existing Animator.
   // This prevents the old Avatar from restoring its centimetre reference pose.
   avatarRig=UnityEngine.Object.Instantiate(rig.gameObject);avatarRig.name="Armature";foreach(var a in avatarRig.GetComponentsInChildren<Animator>(true))UnityEngine.Object.DestroyImmediate(a);
   var avatarBones=avatarRig.GetComponentsInChildren<Transform>(true);var names=HumanNames();var supported=new HashSet<string>(HumanTrait.BoneName.Select(n=>n.Replace(" ","")));var desc=new HumanDescription{human=names.Where(p=>supported.Contains(p.Key)).Select(p=>new HumanBone{humanName=HumanTrait.BoneName.Single(n=>n.Replace(" ","")==p.Key),boneName=p.Value,limit=new HumanLimit{useDefaultValues=true}}).ToArray(),skeleton=avatarBones.Select(t=>new SkeletonBone{name=t.name,position=t.localPosition,rotation=t.localRotation,scale=t.localScale}).ToArray(),upperArmTwist=.5f,lowerArmTwist=.5f,upperLegTwist=.5f,lowerLegTwist=.5f,armStretch=0,legStretch=0};
   var avatar=AvatarBuilder.BuildHumanAvatar(avatarRig,desc);if(!avatar.isValid||!avatar.isHuman)throw new Exception("Metre Avatar invalid");avatar.name="EthanV2_Avatar";const string avatarPath=Folder+"/EthanV2_Avatar.asset";AssetDatabase.DeleteAsset(avatarPath);AssetDatabase.CreateAsset(avatar,avatarPath);animator.avatar=avatar;animator.enabled=true;
   // Restore the authored pose after assigning the Avatar; save that pose explicitly.
   foreach(var pair in native.OrderBy(p=>Depth(p.Key))){pair.Key.SetPositionAndRotation(pair.Value.position,pair.Value.rotation);pair.Key.localScale=Vector3.one;}
   var face=root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r=>r.name=="Face");var parts=new[]{originalHead,originalBody}.Concat(originalSkins.Where(r=>r!=originalHead&&r!=originalBody&&r.name!="BAKED_Outfits_LOD0").OrderBy(r=>r.name));var binds=new List<Matrix4x4>();int slot=0;foreach(var r in parts){var x=face.transform.worldToLocalMatrix*r.transform.localToWorldMatrix;using(var weights=r.sharedMesh.GetAllBoneWeights())foreach(int i in weights.ToArray().Where(w=>w.weight>0).Select(w=>w.boneIndex).Distinct().OrderBy(i=>i))binds.Add(face.bones[slot++].worldToLocalMatrix*r.bones[i].localToWorldMatrix*r.sharedMesh.bindposes[i]*x.inverse);}face.sharedMesh.bindposes=binds.ToArray();EditorUtility.SetDirty(face.sharedMesh);AssetDatabase.SaveAssetIfDirty(face.sharedMesh);
   var outfit=root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r=>r.name=="Outfit");var oldOutfit=originalSkins.Single(r=>r.name=="BAKED_Outfits_LOD0");var ob=new List<Matrix4x4>();slot=0;using(var weights=oldOutfit.sharedMesh.GetAllBoneWeights())foreach(int i in weights.ToArray().Where(w=>w.weight>0).Select(w=>w.boneIndex).Distinct().OrderBy(i=>i))ob.Add(outfit.bones[slot++].worldToLocalMatrix*oldOutfit.bones[i].localToWorldMatrix*oldOutfit.sharedMesh.bindposes[i]);outfit.sharedMesh.bindposes=ob.ToArray();EditorUtility.SetDirty(outfit.sharedMesh);AssetDatabase.SaveAssetIfDirty(outfit.sharedMesh);
   root.SetActive(true);foreach(var pair in native.OrderBy(p=>Depth(p.Key))){pair.Key.SetPositionAndRotation(pair.Value.position,pair.Value.rotation);pair.Key.localScale=Vector3.one;}PrefabUtility.SaveAsPrefabAsset(root,path,out var success);if(!success)throw new Exception("Candidate save failed");var result=new{normalized=true,avatarValid=avatar.isValid,humanoid=avatar.isHuman,nativeBones=native.Count,scale=rig.localScale.ToString(),productionChanged=false};Save("unit_normalization",result);return result;
  }finally{if(avatarRig)UnityEngine.Object.DestroyImmediate(avatarRig);PrefabUtility.UnloadPrefabContents(source);PrefabUtility.UnloadPrefabContents(root);}
 }
 public static object CalibrateAvatar(){
  Guard();const string path=Folder+"/EthanV2_Candidate.prefab";var root=PrefabUtility.LoadPrefabContents(path);GameObject clone=null;
  try{
   var rig=root.transform.Find("Armature");var animator=rig.GetComponent<Animator>();root.SetActive(false);var rest=rig.GetComponentsInChildren<Transform>(true).ToDictionary(t=>t,t=>(t.localPosition,t.localRotation,t.localScale));clone=UnityEngine.Object.Instantiate(rig.gameObject);clone.name="Armature";UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(clone,root.scene);foreach(var a in clone.GetComponentsInChildren<Animator>(true))UnityEngine.Object.DestroyImmediate(a);
   var all=clone.GetComponentsInChildren<Transform>(true);var byName=all.ToDictionary(t=>t.name);var calibration=new List<object>();
   foreach(string side in new[]{"l","r"}){var upper=byName["upperarm_"+side];var lower=byName["lowerarm_"+side];var hand=byName["hand_"+side];var direction=Vector3.right*Mathf.Sign(upper.position.x-byName["pelvis"].position.x);upper.rotation=Quaternion.FromToRotation(lower.position-upper.position,direction)*upper.rotation;lower.rotation=Quaternion.FromToRotation(hand.position-lower.position,direction)*lower.rotation;calibration.Add(new{side,upperDirection=(lower.position-upper.position).normalized.ToString(),lowerDirection=(hand.position-lower.position).normalized.ToString()});}
   var names=HumanNames();var supported=new HashSet<string>(HumanTrait.BoneName.Select(n=>n.Replace(" ","")));var desc=new HumanDescription{human=names.Where(p=>supported.Contains(p.Key)).Select(p=>new HumanBone{humanName=HumanTrait.BoneName.Single(n=>n.Replace(" ","")==p.Key),boneName=p.Value,limit=new HumanLimit{useDefaultValues=true}}).ToArray(),skeleton=all.Select(t=>new SkeletonBone{name=t.name,position=t.localPosition,rotation=t.localRotation,scale=t.localScale}).ToArray(),upperArmTwist=.5f,lowerArmTwist=.5f,upperLegTwist=.5f,lowerLegTwist=.5f,armStretch=0,legStretch=0};
   var avatar=AvatarBuilder.BuildHumanAvatar(clone,desc);if(!avatar.isValid||!avatar.isHuman)throw new Exception("Calibrated Avatar invalid");avatar.name="EthanV2_Avatar_TPose";const string avatarPath=Folder+"/EthanV2_Avatar_TPose.asset";AssetDatabase.DeleteAsset(avatarPath);AssetDatabase.CreateAsset(avatar,avatarPath);animator.enabled=false;animator.avatar=avatar;animator.enabled=true;root.SetActive(true);foreach(var p in rest){p.Key.localPosition=p.Value.Item1;p.Key.localRotation=p.Value.Item2;p.Key.localScale=p.Value.Item3;}PrefabUtility.SaveAsPrefabAsset(root,path,out var success);if(!success)throw new Exception("Candidate save failed");var result=new{avatarValid=true,humanoid=true,calibration,meshDataChanged=false};Save("avatar_calibration",result);return result;
  }finally{if(clone)UnityEngine.Object.DestroyImmediate(clone);PrefabUtility.UnloadPrefabContents(root);}
 }
 public static object RefineClothingHelpers(){Guard();const string path=Folder+"/EthanV2_Candidate.prefab";var root=PrefabUtility.LoadPrefabContents(path);try{var rig=root.transform.Find("Armature");var head=rig.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="head");var changes=new List<object>();foreach(var t in rig.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("OutfitBind_DEF-jaw")||t.name.StartsWith("OutfitBind_DEF-ear"))){string previous=t.parent.name;t.SetParent(head,true);changes.Add(new{t.name,previous,parent=head.name});}PrefabUtility.SaveAsPrefabAsset(root,path,out var success);if(!success)throw new Exception("Candidate save failed");Save("clothing_helpers",new{changes,neutralWorldTransformsPreserved=true,weightsChanged=false});return new{helpers=changes.Count};}finally{PrefabUtility.UnloadPrefabContents(root);}}
 public static object ProbeAvatar(){
  Guard();var prefab=PrefabUtility.LoadPrefabContents(Source);GameObject rig=null;Avatar avatar=null;
  try{
   var body=prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r=>r.name=="BAKED_body_lod0_mesh");var sourceRig=body.bones[0];while(sourceRig.parent!=prefab.transform)sourceRig=sourceRig.parent;
   rig=UnityEngine.Object.Instantiate(sourceRig.gameObject);rig.name="Armature";rig.hideFlags=HideFlags.HideAndDontSave;
   // Keep native axes/unit scale and actual parent chains; no Rigify hierarchy is used.
   var all=rig.GetComponentsInChildren<Transform>(true);var names=HumanNames();var missing=names.Where(p=>!all.Any(t=>t.name==p.Value)).ToArray();if(missing.Length>0)throw new Exception("Missing native human bones: "+JsonConvert.SerializeObject(missing));
   var supported=new HashSet<string>(HumanTrait.BoneName.Select(n=>n.Replace(" ","")));
   var human=names.Where(p=>supported.Contains(p.Key)).Select(p=>new HumanBone{humanName=HumanTrait.BoneName.Single(n=>n.Replace(" ","")==p.Key),boneName=p.Value,limit=new HumanLimit{useDefaultValues=true}}).ToArray();
   var desc=new HumanDescription{human=human,skeleton=all.Select(t=>new SkeletonBone{name=t.name,position=t.localPosition,rotation=t.localRotation,scale=t.localScale}).ToArray(),upperArmTwist=.5f,lowerArmTwist=.5f,upperLegTwist=.5f,lowerLegTwist=.5f,armStretch=.05f,legStretch=.05f};
   var messages=new List<string>();Application.LogCallback log=(m,s,t)=>messages.Add(m);Application.logMessageReceived+=log;try{avatar=AvatarBuilder.BuildHumanAvatar(rig,desc);}finally{Application.logMessageReceived-=log;}
   var result=new{valid=avatar&&avatar.isValid,human=avatar&&avatar.isHuman,mapped=human.Length,bones=all.Length,messages};File.WriteAllText(Report+"/avatar_probe.json",JsonConvert.SerializeObject(result,Formatting.Indented));
   if(!avatar||!avatar.isValid||!avatar.isHuman)throw new Exception("Native body Humanoid gate failed: "+JsonConvert.SerializeObject(result));
   if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/_Sandbox/Characters/Ethan_V2","Candidate");
   const string path=Folder+"/EthanV2_NativeAvatar.asset";if(AssetDatabase.LoadAssetAtPath<Avatar>(path))throw new Exception("Candidate Avatar already exists");avatar.name="EthanV2_NativeAvatar";avatar.hideFlags=HideFlags.None;AssetDatabase.CreateAsset(avatar,path);avatar=null;return result;
  }finally{if(avatar)UnityEngine.Object.DestroyImmediate(avatar);if(rig)UnityEngine.Object.DestroyImmediate(rig);PrefabUtility.UnloadPrefabContents(prefab);}
 }
}





