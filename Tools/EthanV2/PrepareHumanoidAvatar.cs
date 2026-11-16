using System;using System.IO;using System.Linq;using UnityEngine;using UnityEditor;using Newtonsoft.Json;
public static class PrepareV2Humanoid {
 static string Parent(string n){
 if(n=="Hips")return null;if(n=="Spine")return "Hips";if(n=="Chest")return "Spine";if(n=="UpperChest")return "Chest";if(n=="Neck")return "UpperChest";if(n=="Head")return "Neck";
 string side=n.StartsWith("Left")?"Left":"Right";var part=n.Substring(side.Length);
 if(part=="Shoulder")return "UpperChest";if(part=="UpperArm")return side+"Shoulder";if(part=="LowerArm")return side+"UpperArm";if(part=="Hand")return side+"LowerArm";
 if(part=="UpperLeg")return "Hips";if(part=="LowerLeg")return side+"UpperLeg";if(part=="Foot")return side+"LowerLeg";if(part=="Toes")return side+"Foot";
 if(part.EndsWith("Proximal"))return side+"Hand";if(part.EndsWith("Intermediate"))return n.Replace("Intermediate","Proximal");if(part.EndsWith("Distal"))return n.Replace("Distal","Intermediate");return null;}
 public static object Run(){
 if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Edit Mode required");
 var r=PrefabUtility.LoadPrefabContents("Assets/_Sandbox/Characters/Ethan_V2/Ethan_V2.prefab");
 try{
 var rig=r.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="BAKED_rig");
 var importer=(ModelImporter)AssetImporter.GetAtPath("Assets/_Sandbox/Characters/Jiwoo New/Jiwoo_EthanSetup.fbx");var desc=importer.humanDescription;
 var all=rig.GetComponentsInChildren<Transform>(true);var missing=desc.human.Where(h=>!all.Any(t=>t.name==h.boneName)).Select(h=>h.boneName).ToArray();if(missing.Length>0)throw new Exception(string.Join(",",missing));
 var mapped=desc.human.ToDictionary(h=>h.humanName.Replace(" ",""),h=>all.Single(t=>t.name==h.boneName));
 foreach(var pair in mapped){var p=Parent(pair.Key);var parent=p!=null&&mapped.ContainsKey(p)?mapped[p]:rig;if(!pair.Value.IsChildOf(parent))pair.Value.SetParent(parent,true);}
 all=rig.GetComponentsInChildren<Transform>(true);
 File.WriteAllText("Data/EthanV2/CharacterSetup/hierarchy_debug.json",JsonConvert.SerializeObject(new{humans=mapped.Select(p=>new{p.Key,name=p.Value.name,parent=p.Value.parent.name}),fields=typeof(SkeletonBone).GetFields().Select(f=>f.Name)},Formatting.Indented)); desc.skeleton=all.Select(t=>new SkeletonBone{name=t.name,position=t.localPosition,rotation=t.localRotation,scale=t.localScale}).ToArray();
 var logs=new System.Collections.Generic.List<string>();Application.LogCallback callback=(message,stack,type)=>logs.Add(message);Application.logMessageReceived+=callback;
 Avatar avatar;try{avatar=AvatarBuilder.BuildHumanAvatar(rig.gameObject,desc);}finally{Application.logMessageReceived-=callback;}avatar.name="EthanV2_Humanoid";File.WriteAllText("Data/EthanV2/CharacterSetup/avatar_messages.json",JsonConvert.SerializeObject(new{logs,duplicates=all.GroupBy(t=>t.name).Where(g=>g.Count()>1).Select(g=>g.Key).ToArray()},Formatting.Indented));
 var report=new{valid=avatar.isValid,human=avatar.isHuman,rig=rig.name,mappedBones=desc.human.Length,skeletonBones=all.Length,rigScale=rig.localScale.x,skins=r.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(s=>new{s.name,boneRoots=s.bones.Where(b=>b).GroupBy(b=>AnimationUtility.CalculateTransformPath(b,r.transform).Split('/')[0]).Select(g=>new{root=g.Key,count=g.Count()}).ToArray()}).ToArray()};
 File.WriteAllText("Data/EthanV2/CharacterSetup/avatar_audit.json",JsonConvert.SerializeObject(report,Formatting.Indented));
 if(!avatar.isValid||!avatar.isHuman)throw new Exception("Humanoid avatar invalid");const string path="Assets/_Sandbox/Characters/Ethan_V2/EthanV2_Humanoid.asset";if(AssetDatabase.LoadAssetAtPath<Avatar>(path))throw new Exception("Avatar exists");AssetDatabase.CreateAsset(avatar,path);var animator=rig.gameObject.GetComponent<Animator>()??rig.gameObject.AddComponent<Animator>();animator.avatar=avatar;animator.applyRootMotion=false;PrefabUtility.SaveAsPrefabAsset(r,"Assets/_Sandbox/Characters/Ethan_V2/Ethan_V2.prefab");AssetDatabase.SaveAssets();return report;
 }finally{PrefabUtility.UnloadPrefabContents(r);}}
}


