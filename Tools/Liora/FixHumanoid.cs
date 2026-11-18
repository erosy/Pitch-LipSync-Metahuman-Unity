using System;using System.IO;using System.Linq;using System.Collections.Generic;using UnityEngine;using UnityEditor;using Newtonsoft.Json;
public static class LioraHumanoidFix {
 const string Path="Assets/_Sandbox/Characters/Liora/Liora_73_Baked.fbx";
 static Dictionary<string,string> Map(){
 var m=new Dictionary<string,string>{{"Hips","pelvis"},{"Spine","spine_01"},{"Chest","spine_03"},{"UpperChest","spine_05"},{"Neck","neck_01"},{"Head","head"}};
 foreach(var s in new[]{("Left","l"),("Right","r")}){
 foreach(var p in new[]{("Shoulder","clavicle"),("UpperArm","upperarm"),("LowerArm","lowerarm"),("Hand","hand"),("UpperLeg","thigh"),("LowerLeg","calf"),("Foot","foot"),("Toes","ball")})m[s.Item1+p.Item1]=p.Item2+"_"+s.Item2;
 foreach(var f in new[]{("Thumb","thumb"),("Index","index"),("Middle","middle"),("Ring","ring"),("Little","pinky")})for(int j=1;j<=3;j++)m[s.Item1+f.Item1+new[]{"Proximal","Intermediate","Distal"}[j-1]]=f.Item2+"_0"+j+"_"+s.Item2;
 }return m;}
 public static object Run(){
 if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Edit Mode required");
 string backup="Data/Liora/Native/BeforeHumanoidFix";Directory.CreateDirectory(backup);
 foreach(var ext in new[]{"",".meta"}){var dst=backup+"/Liora_73_Baked.fbx"+ext;if(File.Exists(dst))throw new Exception("Backup exists; refuse overwrite");File.Copy(Path+ext,dst);}
 File.Copy("Data/Liora/Native/Liora_73_Clean.fbx",Path,true);
 var imp=(ModelImporter)AssetImporter.GetAtPath(Path);imp.animationType=ModelImporterAnimationType.Generic;imp.importAnimation=false;imp.importCameras=false;imp.importLights=false;imp.SaveAndReimport();
 var model=AssetDatabase.LoadAssetAtPath<GameObject>(Path);var all=model.GetComponentsInChildren<Transform>(true);var names=Map();
 foreach(var p in names)if(all.Count(t=>t.name==p.Value)!=1)throw new Exception("Missing/ambiguous "+p.Key+" : "+p.Value);
 var desc=imp.humanDescription;desc.human=names.Select(p=>new HumanBone{humanName=HumanTrait.BoneName.Single(n=>n.Replace(" ","")==p.Key),boneName=p.Value,limit=new HumanLimit{useDefaultValues=true}}).ToArray();
 desc.skeleton=all.Select(t=>new SkeletonBone{name=t.name,position=t.localPosition,rotation=t.localRotation,scale=t.localScale}).ToArray();
 imp.humanDescription=desc;imp.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;imp.animationType=ModelImporterAnimationType.Human;imp.SaveAndReimport();
 var avatar=AssetDatabase.LoadAllAssetsAtPath(Path).OfType<Avatar>().SingleOrDefault();
 var result=new{path=Path,backup,valid=avatar&&avatar.isValid,human=avatar&&avatar.isHuman,mappedBones=names.Count,transforms=all.Length};
 File.WriteAllText("Data/Liora/Native/humanoid_fix_report.json",JsonConvert.SerializeObject(result,Formatting.Indented));
 if(!result.valid||!result.human)throw new Exception("Humanoid validation failed; inspect report");return result;
 }
}
