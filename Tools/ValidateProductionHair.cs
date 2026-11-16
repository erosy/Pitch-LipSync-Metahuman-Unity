using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Unity.DemoTeam.Hair;
using Newtonsoft.Json;

// Read-only Edit Mode validation. Does not modify prefabs/scenes or Play Mode.
public static class ValidateProductionHair
{
    const string Output="Data/ProductionCleanup/2026-11-11";
    public static object Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Edit Mode required; user controls Play Mode");
        var results=new List<object>();
        foreach(var path in new[]{"Assets/_Sandbox/Characters/Jiwoo/Jiwoo_Normalized.prefab","Assets/_Sandbox/Characters/Ethan/Ethan.prefab"}){
            var r=PrefabUtility.LoadPrefabContents(path);
            try{
                int missing=r.GetComponentsInChildren<Transform>(true).Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));if(missing!=0)throw new Exception(path+": missing scripts");
                var errors=r.GetComponent<Sandbox.CharacterSystem.Character>().ValidateConfiguration();if(errors.Count>0)throw new Exception(string.Join(";",errors));
                foreach(var h in r.GetComponentsInChildren<HairInstance>(true)){
                    var groups=h.strandGroupProviders.SelectMany(p=>p.hairAsset.strandGroups).ToArray();
                    if(groups.Any(g=>g.strandParticleCount!=32)||h.settingsExecutive.updateSimulationRate!=HairInstance.SettingsExecutive.UpdateRate.Fixed30Hz)throw new Exception(path+": requires 32 points / 30 Hz");
                    if(h.strandGroupDefaults.settingsGeometry.strandScale!=HairSim.SettingsGeometry.StrandScale.Fixed)throw new Exception("Fixed strand scale required");
                    if(!h.strandGroupDefaults.settingsRendering.materialAsset||h.settingsEnvironment.boundaryResident.Any(b=>!b))throw new Exception("Missing hair material/boundary reference");
                    if(r.name=="Jiwoo_Normalized"&&h.settingsExecutive.updateSimulationInEditor)throw new Exception("Jiwoo Edit Mode simulation unexpectedly enabled");
                    results.Add(new{prefab=path,points=32,simulationHz=30,strands=groups.Sum(g=>g.strandCount),particles=groups.Sum(g=>g.particlePosition.Length),boundaries=h.settingsEnvironment.boundaryResident.Length,missingScripts=missing});
                }
            }finally{PrefabUtility.UnloadPrefabContents(r);}
        }
        var scenes=Enumerable.Range(0,UnityEngine.SceneManagement.SceneManager.sceneCount).Select(i=>UnityEngine.SceneManagement.SceneManager.GetSceneAt(i)).Where(s=>s.isLoaded).Select(s=>new{s.path,s.isDirty}).ToArray();
        var report=new{characters=results,scenes,playing=EditorApplication.isPlaying};Directory.CreateDirectory(Output);File.WriteAllText(Output+"/production_hair_validation.json",JsonConvert.SerializeObject(report,Formatting.Indented));return report;
    }
}
