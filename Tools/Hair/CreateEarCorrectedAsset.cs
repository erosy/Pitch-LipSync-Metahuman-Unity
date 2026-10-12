if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) throw new System.Exception("Stop Play Mode first.");
const string folder="Assets/_Sandbox/Groom Hair Asset/Fitted/";
UnityEditor.AssetDatabase.ImportAsset(folder+"Ethan_EarClearance.bytes",UnityEditor.ImportAssetOptions.ForceSynchronousImport);
var original=UnityEditor.AssetDatabase.LoadAssetAtPath<Unity.DemoTeam.Hair.HairAsset>(folder+"Ethan_BlenderFitted.asset");
var provider=UnityEditor.AssetDatabase.LoadAssetAtPath<BlenderFittedHairData>(folder+"Ethan_EarClearanceCurves.asset");
if(provider==null) { provider=UnityEngine.ScriptableObject.CreateInstance<BlenderFittedHairData>();UnityEditor.AssetDatabase.CreateAsset(provider,folder+"Ethan_EarClearanceCurves.asset"); }
provider.curveFile=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.TextAsset>(folder+"Ethan_EarClearance.bytes");
UnityEditor.EditorUtility.SetDirty(provider);
var asset=UnityEditor.AssetDatabase.LoadAssetAtPath<Unity.DemoTeam.Hair.HairAsset>(folder+"Ethan_EarClearance.asset");
if(asset==null) { asset=UnityEngine.ScriptableObject.CreateInstance<Unity.DemoTeam.Hair.HairAsset>();UnityEditor.AssetDatabase.CreateAsset(asset,folder+"Ethan_EarClearance.asset"); }
asset.settingsBasic=original.settingsBasic;
asset.settingsCustom=original.settingsCustom;asset.settingsCustom.dataProvider=provider;
asset.strandGroupsAutoBuild=false;
Unity.DemoTeam.Hair.HairAssetBuilder.BuildHairAsset(asset,Unity.DemoTeam.Hair.HairAssetBuilder.BuildFlags.DisableProgress);
UnityEditor.AssetDatabase.SaveAssets();
return new { asset=UnityEditor.AssetDatabase.GetAssetPath(asset),strands=asset.strandGroups[0].strandCount,points=asset.strandGroups[0].strandParticleCount,original=UnityEditor.AssetDatabase.GetAssetPath(original) };
