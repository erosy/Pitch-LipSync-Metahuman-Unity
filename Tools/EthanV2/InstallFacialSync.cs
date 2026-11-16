using System;using System.Linq;using UnityEditor;using UnityEngine;
public static class InstallV2FacialSync {
 public static object Run(){
 if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Edit Mode required");const string path="Assets/_Sandbox/Characters/Ethan_V2/Ethan_V2.prefab";var root=PrefabUtility.LoadPrefabContents(path);
 try{var all=root.GetComponentsInChildren<SkinnedMeshRenderer>(true);var head=all.Single(r=>r.name=="BAKED_head_lod0_mesh");var targets=all.Where(r=>r!=head&&r.name!="BAKED_body_lod0_mesh"&&r.name!="BAKED_Outfits_LOD0"&&r.sharedMesh.blendShapeCount==73).ToArray();var sync=root.GetComponent<CharacterFacialMeshSync>()??root.AddComponent<CharacterFacialMeshSync>();sync.Configure(head,targets);sync.Apply();PrefabUtility.SaveAsPrefabAsset(root,path);return new{secondaryMeshes=targets.Length};}finally{PrefabUtility.UnloadPrefabContents(root);}}
}
