using System;using System.IO;using System.Linq;using System.Collections.Generic;using UnityEngine;using UnityEditor;using Sandbox.CharacterSystem;using Newtonsoft.Json;
public static class WireLioraCandidate {
 const string Folder="Assets/_Sandbox/Characters/Liora/Candidate/";const string Path=Folder+"Liora_Candidate.prefab";
 static void Ref(UnityEngine.Object o,string field,UnityEngine.Object value){var s=new SerializedObject(o);var p=s.FindProperty(field);if(p==null)throw new Exception(o.GetType()+" missing "+field);p.objectReferenceValue=value;s.ApplyModifiedPropertiesWithoutUndo();}
 static int Depth(Transform t){int n=0;while(t.parent){n++;t=t.parent;}return n;}
 public static object Run(){
 if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Edit Mode required");
 foreach(var name in new[]{"Eyebrows","Eyelashes"}){string target=Folder+"Liora_"+name+".asset";File.Copy("Data/Liora/Integration/"+name+"_preserved.asset",target,true);AssetDatabase.ImportAsset(target,ImportAssetOptions.ForceUpdate);}
 var template=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Sandbox/Characters/Ethan_V2/Candidate/EthanV2_Candidate.prefab");var root=PrefabUtility.LoadPrefabContents(Path);
 try{
 if(root.GetComponent<Character>())throw new Exception("Candidate already wired");root.SetActive(false);
 var animator=root.GetComponentInChildren<Animator>(true);var head=animator.transform.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="head");var face=root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r=>r.name=="BAKED_head_lod0_mesh");var sourceFace=template.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r=>r.name=="Face");
 var remap=new Dictionary<UnityEngine.Object,UnityEngine.Object>{{template,root},{template.transform,root.transform},{template.GetComponentInChildren<Animator>(true),animator},{sourceFace,face},{sourceFace.gameObject,face.gameObject},{sourceFace.transform,face.transform},{template.GetComponentInChildren<Unity.DemoTeam.Hair.HairInstance>(true),root.GetComponentInChildren<Unity.DemoTeam.Hair.HairInstance>(true)}};
 var copied=new List<Component>();
 Action<GameObject,GameObject,Func<Component,bool>> components=(src,dst,accept)=>{foreach(var c in src.GetComponents<Component>().Where(c=>c&&accept(c))){var clone=dst.AddComponent(c.GetType());EditorUtility.CopySerialized(c,clone);remap[c]=clone;copied.Add(clone);}};
 components(template,root,c=>c is Character||c is CharacterSpeechPlayer||c is CharacterFlowController||c is CharacterHairStartupWarmup||c is CharacterReadiness);
 var modules=template.transform.Find("Modules");foreach(var old in modules.GetComponentsInChildren<Transform>(true).OrderBy(Depth)){
 var next=new GameObject(old.name).transform;next.SetParent(old==modules?root.transform:(Transform)remap[old.parent],false);next.localPosition=old.localPosition;next.localRotation=old.localRotation;next.localScale=old.localScale;remap[old]=next;remap[old.gameObject]=next.gameObject;components(old.gameObject,next.gameObject,c=>!(c is Transform));}
 var originalVoice=template.GetComponentInChildren<uLipSync.uLipSync>(true).gameObject;var voice=new GameObject("Voice");voice.transform.SetParent(head,false);remap[originalVoice]=voice;remap[originalVoice.transform]=voice.transform;components(originalVoice,voice,c=>!(c is Transform));
 components(sourceFace.gameObject,face.gameObject,c=>c is uLipSync.uLipSyncBlendShape);
 var gaze=new GameObject("SpeechGazeFrame").transform;gaze.SetParent(head,false);gaze.rotation=Quaternion.identity;gaze.localPosition=Vector3.zero;gaze.localScale=Vector3.one;
 var originalExpression=template.GetComponentInChildren<ExpressionModule>(true);var oldGaze=new SerializedObject(originalExpression).FindProperty("speechGaze.head").objectReferenceValue;if(oldGaze)remap[oldGaze]=gaze;
 var errors=new List<string>();foreach(var c in copied){var s=new SerializedObject(c);var p=s.GetIterator();while(p.Next(true)){
 if(p.propertyPath.StartsWith("m_")||p.propertyType!=SerializedPropertyType.ObjectReference||!p.objectReferenceValue)continue;var old=p.objectReferenceValue;
 if(remap.TryGetValue(old,out var value))p.objectReferenceValue=value;
 else {Transform owner=(old as Component)?.transform??(old as GameObject)?.transform;if(owner&&(owner==template.transform||owner.IsChildOf(template.transform)))errors.Add(c.GetType().Name+"."+p.propertyPath+" -> "+owner.name);}
 }s.ApplyModifiedPropertiesWithoutUndo();}if(errors.Count>0)throw new Exception("Unmapped template references: "+string.Join(";",errors));
 var driver=root.GetComponentInChildren<uLipSync.uLipSyncBlendShape>(true);var sourceDriver=template.GetComponentInChildren<uLipSync.uLipSyncBlendShape>(true);
 for(int j=0;j<driver.blendShapes.Count;j++){string name=sourceFace.sharedMesh.GetBlendShapeName(sourceDriver.blendShapes[j].index);int index=face.sharedMesh.GetBlendShapeIndex(name);if(index<0)throw new Exception("Missing phoneme shape "+name);driver.blendShapes[j].index=index;}
 Ref(driver,"skinnedMeshRenderer",face);var lip=voice.GetComponent<uLipSync.uLipSync>();var audio=voice.GetComponent<AudioSource>();audio.playOnAwake=false;
 var profile=AssetDatabase.LoadAssetAtPath<uLipSync.Profile>("Assets/_Sandbox/LipSync/Profile_Liora_ID.asset");if(!profile)throw new Exception("Liora voice profile missing");lip.profile=profile;
 var speech=root.GetComponent<CharacterSpeechPlayer>();Ref(speech,"audioSource",audio);Ref(speech,"lipSync",lip);Ref(speech,"profile",profile);Ref(speech,"clip",AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Sandbox/Audio/liora.mp3"));
 var expression=root.GetComponentInChildren<ExpressionModule>(true);Ref(expression,"face",face);Ref(expression,"lipSync",lip);Ref(expression,"blendShapeDriver",driver);Ref(expression,"blendShapeDebugger",null);
 var es=new SerializedObject(expression);es.FindProperty("speechGaze.head").objectReferenceValue=gaze;es.FindProperty("speechGaze.target").objectReferenceValue=null;es.FindProperty("speechGaze.assistHead").boolValue=false;es.FindProperty("speechGaze.eyeOnlyDownwardGaze").boolValue=false;es.FindProperty("previewDefinition").objectReferenceValue=null;es.ApplyModifiedPropertiesWithoutUndo();
 var animation=root.GetComponentInChildren<AnimationModule>(true);Ref(animation,"animator",animator);Ref(animation,"previewDefinition",null);
 var warmup=root.GetComponent<CharacterHairStartupWarmup>();Ref(warmup,"targetAnimator",animator);var ws=new SerializedObject(warmup);var hs=ws.FindProperty("strandHair");hs.arraySize=1;hs.GetArrayElementAtIndex(0).objectReferenceValue=root.GetComponentInChildren<Unity.DemoTeam.Hair.HairInstance>(true);ws.ApplyModifiedPropertiesWithoutUndo();
 var ready=root.GetComponent<CharacterReadiness>();Ref(ready,"startupWarmup",warmup);Ref(ready,"hairFallback",null);
 var sync=root.AddComponent<CharacterFacialMeshSync>();var targets=root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r=>r!=face&&r.sharedMesh.blendShapeCount>0).ToArray();sync.Configure(face,targets);sync.Apply();
 root.SetActive(true);errors=root.GetComponent<Character>().ValidateConfiguration();errors.AddRange(root.GetComponent<CharacterFlowController>().ValidateConfiguration());if(errors.Count>0)throw new Exception(string.Join(";",errors));
 PrefabUtility.SaveAsPrefabAsset(root,Path,out var success);if(!success)throw new Exception("Candidate save failed");AssetDatabase.SaveAssets();
 var report=new{candidate=Path,template=AssetDatabase.GetAssetPath(template),configurationErrors=errors,oneAnimator=true,modules=new[]{animation.GetType().Name,expression.GetType().Name},phonemes=driver.blendShapes.Select(b=>new{b.phoneme,shape=face.sharedMesh.GetBlendShapeName(b.index)}),profile=AssetDatabase.GetAssetPath(profile),clip="Assets/_Sandbox/Audio/liora.mp3",gaze="Calibrated helper, eye-only, main-camera fallback; template idle-gaze preference retained",secondaryFaceMeshes=targets.Length,sceneSaved=false,selectionControllerChanged=false};File.WriteAllText("Data/Liora/Integration/wiring.json",JsonConvert.SerializeObject(report,Formatting.Indented));return report;
 }finally{PrefabUtility.UnloadPrefabContents(root);}
 }
}
