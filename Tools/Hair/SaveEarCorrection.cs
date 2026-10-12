if(UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)throw new System.Exception("Stop Play Mode first.");
const string prefab="Assets/_Sandbox/Characters/Ethan/Ethan.prefab";
const string backup="Assets/_Sandbox/Characters/Ethan/Ethan_BeforeEarCorrection.prefab";
var asset=UnityEditor.AssetDatabase.LoadAssetAtPath<Unity.DemoTeam.Hair.HairAsset>("Assets/_Sandbox/Groom Hair Asset/Fitted/Ethan_EarClearance.asset");
if(asset==null||asset.strandGroups[0].strandParticleCount!=32)throw new System.Exception("Corrected asset is not ready.");
if(UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(backup)==null&&!UnityEditor.AssetDatabase.CopyAsset(prefab,backup))throw new System.Exception("Could not preserve the prefab backup.");
var root=UnityEditor.PrefabUtility.LoadPrefabContents(prefab);
try {
    foreach(var hair in root.GetComponentsInChildren<Unity.DemoTeam.Hair.HairInstance>(true)) {
        hair.strandGroupProviders=new[]{new Unity.DemoTeam.Hair.HairInstance.GroupProvider{hairAsset=asset}};
        hair.ResetSimulationState();
        typeof(Unity.DemoTeam.Hair.HairInstance).GetMethod("OnValidate",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(hair,null);
        typeof(Unity.DemoTeam.Hair.HairInstance).GetMethod("UpdateStrandGroupInstances",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(hair,null);
        UnityEditor.EditorUtility.SetDirty(hair);
    }
    if(UnityEditor.PrefabUtility.SaveAsPrefabAsset(root,prefab)==null)throw new System.Exception("Could not save the corrected prefab.");
}finally{UnityEditor.PrefabUtility.UnloadPrefabContents(root);}
foreach(var hair in UnityEngine.Object.FindObjectsByType<Unity.DemoTeam.Hair.HairInstance>(UnityEngine.FindObjectsInactive.Exclude)) {
    if(UnityEditor.PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(hair.gameObject)!=prefab)continue;
    UnityEditor.Undo.RecordObject(hair,"Correct Ethan ear strands");
    hair.strandGroupProviders=new[]{new Unity.DemoTeam.Hair.HairInstance.GroupProvider{hairAsset=asset}};
    hair.ResetSimulationState();
    typeof(Unity.DemoTeam.Hair.HairInstance).GetMethod("OnValidate",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(hair,null);
    typeof(Unity.DemoTeam.Hair.HairInstance).GetMethod("UpdateStrandGroupInstances",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(hair,null);
    UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(hair);
    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(hair.gameObject.scene);
}
UnityEditor.AssetDatabase.SaveAssets();
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
return new{asset=UnityEditor.AssetDatabase.GetAssetPath(asset),prefab,backup};
