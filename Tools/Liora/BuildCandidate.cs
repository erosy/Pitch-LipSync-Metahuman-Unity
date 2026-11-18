using System;using System.IO;using System.Linq;using System.Collections.Generic;using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;using Newtonsoft.Json;
public static class BuildLioraCandidate {
 const string Folder="Assets/_Sandbox/Characters/Liora/Candidate";
 const string Source="Assets/_Sandbox/Characters/Liora/Liora_73_Baked.fbx";
 const string Output=Folder+"/Liora_Candidate.prefab";
 static int Depth(Transform t){int n=0;while(t.parent){n++;t=t.parent;}return n;}
 static Transform Copy(Transform src,Transform parent,string name){var t=new GameObject(name).transform;t.SetParent(parent,false);t.SetPositionAndRotation(src.position,src.rotation);t.localScale=Vector3.one;return t;}
 static Dictionary<string,string> Human(){var m=new Dictionary<string,string>{{"Hips","pelvis"},{"Spine","spine_01"},{"Chest","spine_03"},{"UpperChest","spine_05"},{"Neck","neck_01"},{"Head","head"}};foreach(var s in new[]{("Left","l"),("Right","r")}){foreach(var p in new[]{("Shoulder","clavicle"),("UpperArm","upperarm"),("LowerArm","lowerarm"),("Hand","hand"),("UpperLeg","thigh"),("LowerLeg","calf"),("Foot","foot"),("Toes","ball")})m[s.Item1+p.Item1]=p.Item2+"_"+s.Item2;foreach(var f in new[]{("Thumb","thumb"),("Index","index"),("Middle","middle"),("Ring","ring"),("Little","pinky")})for(int j=1;j<=3;j++)m[s.Item1+f.Item1+new[]{"Proximal","Intermediate","Distal"}[j-1]]=f.Item2+"_0"+j+"_"+s.Item2;}return m;}
 static string ClothingAnchor(string n){
 n=n.Replace("Rigify_DEF-","");string side=n.Contains(".L")?"l":n.Contains(".R")?"r":null;
 if(n.StartsWith("jaw")||n.StartsWith("ear"))return "head";
 if(n.StartsWith("shoulder"))return "clavicle_"+side;
 if(n.StartsWith("upper_arm"))return "upperarm_"+side;
 if(n.StartsWith("forearm"))return "lowerarm_"+side;
 if(n.StartsWith("hand"))return "hand_"+side;
 if(n.StartsWith("breast"))return "spine_05";
 if(n.StartsWith("pelvis"))return "pelvis";
 var spine=new Dictionary<string,string>{{"spine","pelvis"},{"spine.001","spine_01"},{"spine.002","spine_02"},{"spine.003","spine_03"},{"spine.004","spine_05"},{"spine.005","neck_01"},{"spine.006","head"}};
 if(spine.TryGetValue(n,out var result))return result;throw new Exception("Unmapped weighted clothing bone "+n);
 }
 public static Vector3[] Skin(SkinnedMeshRenderer r,Transform root){var mesh=r.sharedMesh;var p=mesh.vertices;var result=new Vector3[p.Length];var matrices=r.bones.Select((b,j)=>root.worldToLocalMatrix*b.localToWorldMatrix*mesh.bindposes[j]).ToArray();using(var counts=mesh.GetBonesPerVertex())using(var weights=mesh.GetAllBoneWeights()){int at=0;for(int j=0;j<p.Length;j++)for(int k=0;k<counts[j];k++){var w=weights[at++];result[j]+=matrices[w.boneIndex].MultiplyPoint3x4(p[j])*w.weight;}}return result;}
 public static object Run(){
 if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Edit Mode required");
 if(File.Exists(Output))throw new Exception("Candidate exists; preserve before rebuilding");
 if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/_Sandbox/Characters/Liora","Candidate");
 Directory.CreateDirectory("Data/Liora/Integration/Preserved");foreach(var p in new[]{Source,Source+".meta"})File.Copy(p,"Data/Liora/Integration/Preserved/"+System.IO.Path.GetFileName(p),false);
 var scene=EditorSceneManager.NewPreviewScene();GameObject root=null;GameObject calibration=null;
 try{
 root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Source),scene);PrefabUtility.UnpackPrefabInstance(root,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);root.SetActive(false);root.name="Liora_Candidate";
 var skins=root.GetComponentsInChildren<SkinnedMeshRenderer>(true);var originals=skins.ToDictionary(r=>r,r=>Skin(r,root.transform));
 var oldBody=root.transform.Find("BAKED_LioraMinji_NewMetaHumanCharacter_body_rig");var oldHead=root.transform.Find("BAKED_LioraMinji_NewMetaHumanCharacter_head_rig");var oldRigify=root.transform.Find("BAKED_rig");
 if(!oldBody||!oldHead||!oldRigify)throw new Exception("Expected baked skeletons missing");
 var rig=new GameObject("Armature").transform;rig.SetParent(root.transform,false);var map=new Dictionary<Transform,Transform>{{oldBody,rig},{oldHead,rig},{oldRigify,rig}};var native=new Dictionary<string,Transform>();
 foreach(var t in oldBody.GetComponentsInChildren<Transform>(true).Where(t=>t!=oldBody).OrderBy(Depth)){var copy=Copy(t,map[t.parent],t.name);map[t]=copy;native[t.name]=copy;}
 foreach(var t in oldHead.GetComponentsInChildren<Transform>(true).Where(t=>t!=oldHead).OrderBy(Depth)){
 var name=t.name.StartsWith("Face_")?t.name.Substring(5):t.name;
 if(native.TryGetValue(name,out var bodyBone))map[t]=bodyBone;else map[t]=Copy(t,map[t.parent],t.name);
 }
 var clothing=new List<object>();var outfit=skins.Single(r=>r.name=="BAKED_Outfits");
 using(var weights=outfit.sharedMesh.GetAllBoneWeights())foreach(var idx in weights.ToArray().Where(w=>w.weight>0).Select(w=>w.boneIndex).Distinct()){
 var old=outfit.bones[idx];if(map.ContainsKey(old))continue;var anchor=ClothingAnchor(old.name);map[old]=Copy(old,native[anchor],"OutfitBind_"+old.name.Replace('.','_'));clothing.Add(new{source=old.name,anchor,helper=map[old].name});
 }
 var checks=new List<object>();
 foreach(var r in skins){var original=r.sharedMesh;var copy=UnityEngine.Object.Instantiate(original);copy.name="Liora_"+r.name.Replace("BAKED_","");var bones=r.bones;var binds=original.bindposes;var newBones=new Transform[bones.Length];var newBinds=new Matrix4x4[bones.Length];
 for(int j=0;j<bones.Length;j++){if(!bones[j])throw new Exception("Missing source skin bone");newBones[j]=map.TryGetValue(bones[j],out var mapped)?mapped:rig;newBinds[j]=newBones[j].worldToLocalMatrix*bones[j].localToWorldMatrix*binds[j];}
 copy.bindposes=newBinds;AssetDatabase.CreateAsset(copy,Folder+"/"+copy.name+".asset");r.sharedMesh=copy;r.bones=newBones;r.rootBone=native["pelvis"];r.updateWhenOffscreen=true;
 var now=Skin(r,root.transform);float error=now.Select((p,j)=>Vector3.Distance(p,originals[r][j])).Max();if(error>2e-6f)throw new Exception("Rebind error "+r.name+" "+error);
 checks.Add(new{r.name,copy.vertexCount,shapes=copy.blendShapeCount,maxNeutralErrorM=error});
 }
 foreach(var animator in root.GetComponentsInChildren<Animator>(true))UnityEngine.Object.DestroyImmediate(animator);
 foreach(var old in new[]{oldBody,oldHead,oldRigify})UnityEngine.Object.DestroyImmediate(old.gameObject);
 // Build a meter-based Avatar on a disposable calibrated T-pose skeleton.
 calibration=UnityEngine.Object.Instantiate(rig.gameObject);calibration.name="Armature";UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(calibration,scene);
 var all=calibration.GetComponentsInChildren<Transform>(true);var byName=all.ToDictionary(t=>t.name);
 foreach(var side in new[]{"l","r"}){var upper=byName["upperarm_"+side];var lower=byName["lowerarm_"+side];var hand=byName["hand_"+side];var direction=Vector3.right*Mathf.Sign(upper.position.x-byName["pelvis"].position.x);upper.rotation=Quaternion.FromToRotation(lower.position-upper.position,direction)*upper.rotation;lower.rotation=Quaternion.FromToRotation(hand.position-lower.position,direction)*lower.rotation;}
 var desc=new HumanDescription{human=Human().Select(p=>new HumanBone{humanName=HumanTrait.BoneName.Single(n=>n.Replace(" ","")==p.Key),boneName=p.Value,limit=new HumanLimit{useDefaultValues=true}}).ToArray(),skeleton=all.Select(t=>new SkeletonBone{name=t.name,position=t.localPosition,rotation=t.localRotation,scale=t.localScale}).ToArray(),upperArmTwist=.5f,lowerArmTwist=.5f,upperLegTwist=.5f,lowerLegTwist=.5f,armStretch=0,legStretch=0};
 var avatar=AvatarBuilder.BuildHumanAvatar(calibration,desc);if(!avatar.isValid||!avatar.isHuman)throw new Exception("Invalid native Avatar");avatar.name="Liora_Humanoid";AssetDatabase.CreateAsset(avatar,Folder+"/Liora_Humanoid.asset");
 var a=rig.gameObject.AddComponent<Animator>();a.avatar=avatar;a.applyRootMotion=false;a.cullingMode=AnimatorCullingMode.AlwaysAnimate;
 root.SetActive(true);PrefabUtility.SaveAsPrefabAsset(root,Output,out var success);if(!success)throw new Exception("Prefab save failed");AssetDatabase.SaveAssets();
 var report=new{candidate=Output,oneDeformSkeleton=true,oneAnimator=true,calibratedAvatar=true,neutralGeometryPreserved=true,checks,clothingHelpers=clothing,sceneSaved=false,playModeTested=false};File.WriteAllText("Data/Liora/Integration/skeleton_build.json",JsonConvert.SerializeObject(report,Formatting.Indented));return new{candidate=Output,skins=skins.Length,nativeBones=all.Length,avatarValid=true};
 }finally{if(calibration)UnityEngine.Object.DestroyImmediate(calibration);if(root)UnityEngine.Object.DestroyImmediate(root);EditorSceneManager.ClosePreviewScene(scene);}
 }
}
