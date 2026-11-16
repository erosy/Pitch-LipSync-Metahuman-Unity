using System;using System.IO;using System.Linq;using System.Collections.Generic;using UnityEngine;using UnityEditor;using UnityEngine.SceneManagement;using UnityEditor.Events;using UnityEngine.UI;using Sandbox.CharacterSystem;using Newtonsoft.Json;
public static class TransferV2CharacterSetup {
 const string PathV2="Assets/_Sandbox/Characters/Ethan_V2/Ethan_V2.prefab";const string Old="Assets/_Sandbox/Characters/Ethan/Ethan.prefab";const string Out="Data/EthanV2/CharacterSetup/";
 static void Ref(UnityEngine.Object o,string n,UnityEngine.Object v){var s=new SerializedObject(o);s.FindProperty(n).objectReferenceValue=v;s.ApplyModifiedPropertiesWithoutUndo();}
 static int Depth(Transform t){int d=0;while(t.parent){d++;t=t.parent;}return d;}
 static string Alias(string n){
 var basic=new Dictionary<string,string>{{"root","Root"},{"pelvis","Hips"},{"spine_01","Spine"},{"spine_02","Chest"},{"spine_03","Chest"},{"spine_04","UpperChest"},{"spine_05","UpperChest"},{"neck_01","Neck"},{"neck_02","Neck2"},{"head","Head"}};if(basic.TryGetValue(n,out var a))return a;
 string side=n.EndsWith("_l")?"Left":n.EndsWith("_r")?"Right":null;if(side==null)return null;
 string stem=n.Substring(0,n.Length-2);var limbs=new Dictionary<string,string>{{"clavicle","Shoulder"},{"upperarm","UpperArm"},{"lowerarm","LowerArm"},{"hand","Hand"},{"thigh","UpperLeg"},{"calf","LowerLeg"},{"foot","Foot"},{"ball","Toes"}};
 foreach(var p in limbs)if(stem==p.Key)return side+p.Value;else if(stem.StartsWith(p.Key+"_twist"))return side+p.Value+"Twist";
 foreach(var finger in new[]{"thumb","index","middle","ring","pinky"})if(stem.StartsWith(finger+"_")){string f=finger=="pinky"?"Pinky":char.ToUpper(finger[0])+finger.Substring(1);string digit=stem.Substring(finger.Length+1).Split('_')[0];return side+f+(digit=="01"?"Proximal":digit=="02"?"Intermediate":digit=="03"?"Distal":"Metacarpal");}return null;}
 public static object Run(){
 if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Edit Mode required");Directory.CreateDirectory(Out+"Before");foreach(var p in new[]{PathV2,PathV2+".meta"})if(!File.Exists(Out+"Before/"+System.IO.Path.GetFileName(p)))File.Copy(p,Out+"Before/"+System.IO.Path.GetFileName(p));
 var old=AssetDatabase.LoadAssetAtPath<GameObject>(Old);var root=PrefabUtility.LoadPrefabContents(PathV2);
 try{
 if(root.GetComponent<Character>())throw new Exception("V2 character is already configured");root.SetActive(false);
 var map=new Dictionary<UnityEngine.Object,UnityEngine.Object>{{old,root},{old.transform,root.transform}};var copied=new List<Component>();
 var driver=new GameObject("AnimationDriver");driver.transform.SetParent(root.transform,false);
 var needed=new HashSet<Transform>();foreach(var b in old.GetComponentsInChildren<SkinnedMeshRenderer>(true).SelectMany(r=>r.bones).Where(t=>t))for(var t=b;t&&t!=old.transform;t=t.parent)needed.Add(t);
 foreach(var t in needed.OrderBy(Depth)){var p=t.parent==old.transform?driver.transform:(Transform)map[t.parent];var n=new GameObject(t.name).transform;n.SetParent(p,false);n.localPosition=t.localPosition;n.localRotation=t.localRotation;n.localScale=t.localScale;map[t]=n;map[t.gameObject]=n.gameObject;}
 var oldAnimator=old.GetComponent<Animator>();var animator=driver.AddComponent<Animator>();EditorUtility.CopySerialized(oldAnimator,animator);animator.runtimeAnimatorController=null;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;map[oldAnimator]=animator;animator.Rebind();
 var generic=root.GetComponent<Animator>();if(generic)generic.enabled=false;
 var face=root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r=>r.name=="BAKED_head_lod0_mesh");var oldFace=old.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r=>r.name=="Face");map[oldFace]=face;map[oldFace.gameObject]=face.gameObject;map[oldFace.transform]=face.transform;
 var head=face.bones.Single(t=>t.name=="head");
 Action<GameObject,GameObject> copyComponents=(src,dst)=>{foreach(var c in src.GetComponents<Component>().Where(c=>c&&!(c is Transform)&&!(c is Renderer)&&!(c is MeshFilter))){var clone=dst.AddComponent(c.GetType());EditorUtility.CopySerialized(c,clone);map[c]=clone;copied.Add(clone);}};
 foreach(var c in old.GetComponents<Component>().Where(c=>c is Character||c is CharacterSpeechPlayer||c is CharacterFlowController)){var n=root.AddComponent(c.GetType());EditorUtility.CopySerialized(c,n);map[c]=n;copied.Add(n);}
 var oldModules=old.transform.Find("Modules");foreach(var t in oldModules.GetComponentsInChildren<Transform>(true).OrderBy(Depth)){var n=new GameObject(t.name).transform;n.SetParent(t==oldModules?root.transform:(Transform)map[t.parent],false);n.localPosition=t.localPosition;n.localRotation=t.localRotation;n.localScale=t.localScale;map[t]=n;map[t.gameObject]=n.gameObject;copyComponents(t.gameObject,n.gameObject);}
 var oldVoice=old.GetComponentInChildren<uLipSync.uLipSync>(true).gameObject;var voice=new GameObject("Voice");voice.transform.SetParent(head,false);map[oldVoice]=voice;map[oldVoice.transform]=voice.transform;copyComponents(oldVoice,voice);
 copyComponents(oldFace.gameObject,face.gameObject); // Only its lip-sync component, not the renderer.
 var warmup=root.GetComponent<CharacterHairStartupWarmup>();var readiness=root.GetComponent<CharacterReadiness>();
 if(old.GetComponent<CharacterReadiness>())map[old.GetComponent<CharacterReadiness>()]=readiness;if(old.GetComponent<CharacterHairStartupWarmup>())map[old.GetComponent<CharacterHairStartupWarmup>()]=warmup;
 foreach(var c in copied){var so=new SerializedObject(c);var it=so.GetIterator();while(it.Next(true)){if(it.propertyType!=SerializedPropertyType.ObjectReference||it.propertyPath=="m_GameObject"||it.propertyPath=="m_Script")continue;var value=it.objectReferenceValue;if(value&&map.TryGetValue(value,out var replacement))it.objectReferenceValue=replacement;}so.ApplyModifiedPropertiesWithoutUndo();}
 var lip=root.GetComponentInChildren<uLipSync.uLipSync>(true);var blend=root.GetComponentInChildren<uLipSync.uLipSyncBlendShape>(true);blend.skinnedMeshRenderer=face;
 var oldBlend=old.GetComponentInChildren<uLipSync.uLipSyncBlendShape>(true);for(int j=0;j<blend.blendShapes.Count;j++){var name=oldFace.sharedMesh.GetBlendShapeName(oldBlend.blendShapes[j].index);int index=face.sharedMesh.GetBlendShapeIndex(name);if(index<0)throw new Exception("Missing viseme "+name);blend.blendShapes[j].index=index;}
 var animation=root.GetComponentInChildren<AnimationModule>(true);Ref(animation,"animator",animator);
 var expression=root.GetComponentInChildren<ExpressionModule>(true);Ref(expression,"face",face);Ref(expression,"lipSync",lip);Ref(expression,"blendShapeDriver",blend);Ref(expression,"speechGaze.head",head);
 Ref(warmup,"targetAnimator",animator);var flow=root.GetComponent<CharacterFlowController>();Ref(flow,"readinessProvider",readiness);
 // Map driver bone identities onto the existing, unmodified separate skin rigs.
 var byName=needed.ToDictionary(t=>t.name,t=>(Transform)map[t]);var sourceBones=old.GetComponentsInChildren<SkinnedMeshRenderer>(true).SelectMany(r=>r.bones).Where(t=>t).Distinct().Select(t=>(Transform)map[t]).ToArray();
 var oldImporter=(ModelImporter)AssetImporter.GetAtPath("Assets/_Sandbox/Characters/Ethan/Ethan_unity.fbx");var humans=oldImporter.humanDescription.human.Where(h=>byName.ContainsKey(h.boneName)).ToDictionary(h=>h.humanName.Replace(" ",""),h=>byName[h.boneName]);
 var jiwoo=(ModelImporter)AssetImporter.GetAtPath("Assets/_Sandbox/Characters/Jiwoo New/Jiwoo_EthanSetup.fbx");var rigify=jiwoo.humanDescription.human.Where(h=>humans.ContainsKey(h.humanName.Replace(" ",""))).ToDictionary(h=>h.boneName,h=>humans[h.humanName.Replace(" ","")]);
 var pairs=new List<CharacterSkeletonPoseBridge.Binding>();foreach(var target in root.GetComponentsInChildren<SkinnedMeshRenderer>(true).SelectMany(r=>r.bones).Where(t=>t).Distinct().OrderBy(Depth)){
  if(target.name.StartsWith("FACIAL_"))continue;
  Transform source=null;if(!rigify.TryGetValue(target.name,out source)){var alias=Alias(target.name);if(alias!=null)byName.TryGetValue(alias,out source);}
  if(!source)source=sourceBones.OrderBy(t=>(root.transform.InverseTransformPoint(t.position)-root.transform.InverseTransformPoint(target.position)).sqrMagnitude).First();
  pairs.Add(new CharacterSkeletonPoseBridge.Binding{source=source,target=target});
 }
 var bridge=root.AddComponent<CharacterSkeletonPoseBridge>();bridge.Configure(root.transform,pairs.ToArray());
 // Native fingers keep their authored spacing; skin correctives follow their
 // own parent joint because the old driver has no equivalent corrective bones.
 foreach(var p in pairs)if((p.target.name.EndsWith("_l")||p.target.name.EndsWith("_r"))&&new[]{"thumb_","index_","middle_","ring_","pinky_"}.Any(f=>p.target.name.StartsWith(f)))
  p.mode=p.target.name.Split('_').Length==3?CharacterSkeletonPoseBridge.TransferMode.RotationOnly:CharacterSkeletonPoseBridge.TransferMode.FollowParent;
 root.SetActive(true);var character=root.GetComponent<Character>();var errors=character.ValidateConfiguration();errors.AddRange(flow.ValidateConfiguration());if(errors.Count>0)throw new Exception(string.Join("; ",errors));
 PrefabUtility.SaveAsPrefabAsset(root,PathV2);AssetDatabase.SaveAssets();File.WriteAllText(Out+"prefab_transfer.json",JsonConvert.SerializeObject(new{avatar=animator.avatar.name,human=animator.avatar.isHuman,bindings=pairs.Count,visemes=blend.blendShapes.Select(b=>new{b.phoneme,shape=face.sharedMesh.GetBlendShapeName(b.index)}).ToArray(),errors},Formatting.Indented));
 }finally{PrefabUtility.UnloadPrefabContents(root);}
 return WireScene();
 }
 public static object WireScene(){
 if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Edit Mode required");
 var roots=Enumerable.Range(0,SceneManager.sceneCount).SelectMany(i=>SceneManager.GetSceneAt(i).GetRootGameObjects()).ToArray();var v2=roots.Single(r=>PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(r)==PathV2);
 var old=roots.FirstOrDefault(r=>r.name=="Ethan");if(old){Undo.RecordObject(old,"Hide original Ethan");old.SetActive(false);}
 var selector=roots.SelectMany(r=>r.GetComponentsInChildren<CharacterSelectionController>(true)).Single();Undo.RecordObject(selector,"Select Ethan V2 for Boy");Ref(selector,"boy",v2.GetComponent<CharacterFlowController>());
 var girlProp=new SerializedObject(selector).FindProperty("girl");if(!girlProp.objectReferenceValue){var girl=roots.Where(r=>r!=v2&&r!=old).Select(r=>r.GetComponent<CharacterFlowController>()).FirstOrDefault(c=>c);if(girl)Ref(selector,"girl",girl);}
 var boyButton=roots.SelectMany(r=>r.GetComponentsInChildren<Button>(true)).Single(b=>b.name=="Boy");Undo.RecordObject(boyButton,"Use Ethan V2 Boy selection");
 for(int j=boyButton.onClick.GetPersistentEventCount()-1;j>=0;j--){var name=boyButton.onClick.GetPersistentMethodName(j);if(name=="SelectBoy"||name=="PlayBoy"||name=="PlayResponse")UnityEventTools.RemovePersistentListener(boyButton.onClick,j);}
 UnityEventTools.AddPersistentListener(boyButton.onClick,selector.SelectBoy);EditorUtility.SetDirty(boyButton);EditorUtility.SetDirty(selector);
 foreach(var demo in roots.SelectMany(r=>r.GetComponentsInChildren<VoiceLipSyncDemo>(true))){Undo.RecordObject(demo,"Use Ethan V2 voice");Ref(demo,"boy.character",v2);Ref(demo,"boy.audioSource",v2.GetComponentInChildren<AudioSource>(true));Ref(demo,"boy.lipSync",v2.GetComponentInChildren<uLipSync.uLipSync>(true));}
 v2.SetActive(true);PrefabUtility.RecordPrefabInstancePropertyModifications(v2);var report=new{boy=v2.name,oldEthanHidden=old&&!old.activeSelf,buttonTarget=boyButton.onClick.GetPersistentTarget(boyButton.onClick.GetPersistentEventCount()-1).name,buttonMethod="SelectBoy",sceneSaved=false,playing=EditorApplication.isPlaying};File.WriteAllText(Out+"scene_transfer.json",JsonConvert.SerializeObject(report,Formatting.Indented));return report;
 }
}
