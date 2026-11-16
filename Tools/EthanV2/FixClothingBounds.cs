using System;using System.Linq;using UnityEditor;using UnityEngine;
public static class FixClothingBounds {
 public static object Run(){if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Edit Mode required");const string path="Assets/_Sandbox/Characters/Ethan_V2/Ethan_V2.prefab";var root=PrefabUtility.LoadPrefabContents(path);try{var cloth=root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r=>r.name=="BAKED_Outfits_LOD0");cloth.rootBone=root.transform;cloth.localBounds=new Bounds(new Vector3(0,1.1f,0),new Vector3(2.4f,2.4f,2.4f));PrefabUtility.SaveAsPrefabAsset(root,path);AssetDatabase.SaveAssets();return new{root=cloth.rootBone.name,bounds=cloth.localBounds.ToString()};}finally{PrefabUtility.UnloadPrefabContents(root);}}
}
