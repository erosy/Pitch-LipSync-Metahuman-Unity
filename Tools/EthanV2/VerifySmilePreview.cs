using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Sandbox.CharacterSystem;
public static class VerifyEthanSmilePreview {
 static ExpressionModule Module()=>UnityEngine.Object.FindObjectsByType<ExpressionModule>(FindObjectsInactive.Exclude).Single(m=>m.Face&&m.Face.name=="BAKED_head_lod0_mesh");
 static object Set(string name){if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Edit Mode required");var m=Module();if(!m.IsPreviewing)throw new Exception("Start supported Joy preview first");var r=m.Face;for(int i=0;i<r.sharedMesh.blendShapeCount;i++){string n=r.sharedMesh.GetBlendShapeName(i);if(n.StartsWith("viseme_")||n.StartsWith("eyeBlink"))r.SetBlendShapeWeight(i,0);}if(name=="blink"){r.SetBlendShapeWeight(r.sharedMesh.GetBlendShapeIndex("eyeBlinkLeft"),100);r.SetBlendShapeWeight(r.sharedMesh.GetBlendShapeIndex("eyeBlinkRight"),100);}else r.SetBlendShapeWeight(r.sharedMesh.GetBlendShapeIndex(name),100);r.transform.root.GetComponent<CharacterFacialMeshSync>().Apply();EditorApplication.QueuePlayerLoopUpdate();SceneView.RepaintAll();return new{pose=name,joyWeight=r.GetBlendShapeWeight(r.sharedMesh.GetBlendShapeIndex("emo_joy"))};}
 public static object AA()=>Set("viseme_aa");
 public static object E()=>Set("viseme_E");
 public static object CH()=>Set("viseme_CH");
 public static object Blink()=>Set("blink");
 public static object HoldBlink(){if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Edit Mode required");var m=Module();if(m.IsPreviewing)throw new Exception("Existing preview must be restored first");var definition=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<ExpressionDefinition>("Assets/_Sandbox/Data/Expression/Joy.asset"));definition.name="Ethan temporary blink verification";var targets=(System.Collections.Generic.List<BlendShapeWeight>)definition.Targets;targets.Add(new BlendShapeWeight("eyeBlinkLeft",100));targets.Add(new BlendShapeWeight("eyeBlinkRight",100));var so=new SerializedObject(definition);so.FindProperty("fadeDuration").floatValue=0;so.ApplyModifiedPropertiesWithoutUndo();return new{started=m.StartPreview(definition)};}
 public static object Stop(){if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Edit Mode required");var m=Module();var r=m.Face;for(int i=0;i<r.sharedMesh.blendShapeCount;i++){string n=r.sharedMesh.GetBlendShapeName(i);if(n.StartsWith("viseme_")||n.StartsWith("eyeBlink"))r.SetBlendShapeWeight(i,0);}m.StopPreview();r.transform.root.GetComponent<CharacterFacialMeshSync>().Apply();EditorApplication.QueuePlayerLoopUpdate();return new{previewStopped=!m.IsPreviewing,remaining=Enumerable.Range(0,r.sharedMesh.blendShapeCount).Where(i=>r.GetBlendShapeWeight(i)!=0).Select(i=>r.sharedMesh.GetBlendShapeName(i)).ToArray(),playing=EditorApplication.isPlaying};}
}
