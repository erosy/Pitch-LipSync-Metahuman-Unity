using System;using System.IO;using System.Linq;using UnityEngine;using UnityEditor;using Newtonsoft.Json;
public static class LioraRigAudit {
 public static object Run(){
 var p="Assets/_Sandbox/Characters/Liora/Liora_73_Baked.fbx";
 var model=AssetDatabase.LoadAssetAtPath<GameObject>(p);var imp=(ModelImporter)AssetImporter.GetAtPath(p);
 var result=new{playing=EditorApplication.isPlaying,importer=imp.humanDescription.human.Select(h=>new{h.humanName,h.boneName}),
 transforms=model.GetComponentsInChildren<Transform>(true).Select(t=>new{t.name,path=AnimationUtility.CalculateTransformPath(t,model.transform),position=new[]{t.position.x,t.position.y,t.position.z},scale=new[]{t.lossyScale.x,t.lossyScale.y,t.lossyScale.z}}),
 meshes=model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(s=>new{s.name,mesh=s.sharedMesh.name,vertices=s.sharedMesh.vertexCount,shapes=s.sharedMesh.blendShapeCount,root=s.rootBone?s.rootBone.name:null,bones=s.bones.Select(b=>b?AnimationUtility.CalculateTransformPath(b,model.transform):null)}),avatars=AssetDatabase.LoadAllAssetsAtPath(p).OfType<Avatar>().Select(a=>new{a.name,a.isValid,a.isHuman})};
 File.WriteAllText("Data/Liora/Native/unity_rig_audit.json",JsonConvert.SerializeObject(result,Formatting.Indented));
 return new{result.playing,transforms=result.transforms.Count(),meshes=result.meshes.Select(s=>new{s.name,s.vertices,s.shapes,s.root}),result.avatars};
 }
}

