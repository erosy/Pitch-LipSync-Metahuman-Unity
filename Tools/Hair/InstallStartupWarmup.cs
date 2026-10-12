if(UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)throw new System.Exception("Stop Play Mode first.");
const string path="Assets/_Sandbox/Characters/Ethan/Ethan.prefab";
var root=UnityEditor.PrefabUtility.LoadPrefabContents(path);
try{
    if(root.GetComponent<EthanHairStartupWarmup>()==null)root.AddComponent<EthanHairStartupWarmup>();
    if(UnityEditor.PrefabUtility.SaveAsPrefabAsset(root,path)==null)throw new System.Exception("Could not save Ethan startup component.");
}finally{UnityEditor.PrefabUtility.UnloadPrefabContents(root);}
var h=UnityEngine.Object.FindObjectsByType<Unity.DemoTeam.Hair.HairInstance>(UnityEngine.FindObjectsInactive.Exclude).First();
var ethan=h.GetComponentInParent<UnityEngine.Animator>().gameObject;
if(ethan.GetComponent<EthanHairStartupWarmup>()==null)UnityEditor.Undo.AddComponent<EthanHairStartupWarmup>(ethan);
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(ethan.scene);
UnityEditor.AssetDatabase.SaveAssets();
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(ethan.scene);
return new{attachedTo=ethan.name,asset=UnityEditor.AssetDatabase.GetAssetPath(h.strandGroupProviders[0].hairAsset)};
