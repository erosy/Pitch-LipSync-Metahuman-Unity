using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Unity.DemoTeam.Hair;
using Newtonsoft.Json;
using UnityEngine.Formats.Alembic.Importer;

public static class OptimizeJiwooScalp
{
    const string Prefab="Assets/_Sandbox/Characters/Jiwoo/Jiwoo_Normalized.prefab";
    const string Original="Assets/_Sandbox/Characters/Jiwoo/Groom/Rigged/Jiwoo_Scalp.asset";
    const string Variant="Assets/_Sandbox/Characters/Jiwoo New/Groom/Jiwoo_Scalp_32.asset";
    const string Output="Data/JiwooRigged/HairOptimization32";
    static void Guard(){if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Edit Mode required; user controls Play Mode");Directory.CreateDirectory(Output);}
    static string Hash(string p){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(p))).Replace("-","");}
    static void Save(string name,object data)=>File.WriteAllText(Output+"/"+name+".json",JsonConvert.SerializeObject(data,Formatting.Indented));
    static void Backup(string path){string dst=Output+"/Before/"+path;Directory.CreateDirectory(Path.GetDirectoryName(dst));if(!File.Exists(dst))File.Copy(path,dst);}
    static Dictionary<string,string> UnrelatedBlocks(string path)
    {
        var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);var hair=asset.GetComponentInChildren<HairInstance>(true);
        var ids=new HashSet<long>();
        foreach(var t in hair.GetComponentsInChildren<Transform>(true)){
            foreach(var o in t.GetComponents<Component>().Cast<UnityEngine.Object>().Concat(new UnityEngine.Object[]{t.gameObject})){
                if(!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(o,out string guid,out long id))throw new Exception("Cannot identify prefab object");ids.Add(id);
            }
        }
        return Regex.Split(File.ReadAllText(path),@"(?m)(?=^--- !u!)").Where(s=>s.StartsWith("--- !u!")).Where(s=>!ids.Contains(long.Parse(Regex.Match(s,@"&(-?\d+)").Groups[1].Value))).ToDictionary(s=>s.Split('\n')[0].Trim(),s=>s);
    }
    public static object BuildVariant()
    {
        Guard();if(AssetDatabase.LoadAssetAtPath<HairAsset>(Variant))throw new Exception("Variant already exists; use Verify");
        foreach(var p in new[]{Prefab,Prefab+".meta",Original,Original+".meta"})Backup(p);
        Save("unrelated_prefab_blocks",UnrelatedBlocks(Prefab));
        var protectedPaths=new[]{Prefab+".meta",Original,Original+".meta","Assets/_Sandbox/Characters/Jiwoo/Groom/Rigged/Jiwoo_Scalp_HeadLocal.abc","Assets/_Sandbox/Characters/Jiwoo New/Jiwoo_EthanSetup.fbx","Assets/_Sandbox/Characters/Jiwoo New/Jiwoo_EthanSetup.fbx.meta","Assets/_Sandbox/Characters/Jiwoo New/Groom/Collision/Jiwoo_HeadSDF.asset","Assets/_Sandbox/Characters/Ethan/Ethan.prefab","Assets/_Sandbox/Characters/Jiwoo/Materials/Jiwoo_StrandHair.mat"};
        Save("protected_hashes",protectedPaths.ToDictionary(p=>p,Hash));
        var original=AssetDatabase.LoadAssetAtPath<HairAsset>(Original);var next=ScriptableObject.CreateInstance<HairAsset>();
        next.settingsBasic=original.settingsBasic;next.settingsAlembic=original.settingsAlembic;next.settingsLODClusters=original.settingsLODClusters;
        next.settingsAlembic.settingsResolve.resampleCurves=false;next.settingsAlembic.settingsResolve.resampleResolution=32;next.strandGroupsAutoBuild=false;
        Directory.CreateDirectory(Path.GetDirectoryName(Variant));AssetDatabase.Refresh();AssetDatabase.CreateAsset(next,Variant);
        return BuildEndpointVariant();
    }
    static object CheckGeometry()
    {
        var old=AssetDatabase.LoadAssetAtPath<HairAsset>(Original).strandGroups.Single();var g=AssetDatabase.LoadAssetAtPath<HairAsset>(Variant).strandGroups.Single();
        if(g.strandCount!=old.strandCount||g.strandParticleCount!=32)throw new Exception("Unexpected groom topology");
        float rootError=0,tipError=0,maxLengthChange=0;
        for(int i=0;i<g.strandCount;i++){
            HairAssetUtility.DeclareStrandIterator(old,i,out int a,out int sa,out int ea);HairAssetUtility.DeclareStrandIterator(g,i,out int b,out int sb,out int eb);
            rootError=Mathf.Max(rootError,Vector3.Distance(old.particlePosition[a],g.particlePosition[b]));tipError=Mathf.Max(tipError,Vector3.Distance(old.particlePosition[ea-sa],g.particlePosition[eb-sb]));
            float ol=0,nl=0;for(int k=a+sa;k<ea;k+=sa)ol+=Vector3.Distance(old.particlePosition[k],old.particlePosition[k-sa]);
            for(int k=b+sb;k<eb;k+=sb){var p=g.particlePosition[k];if(float.IsNaN(p.x)||float.IsInfinity(p.x)||float.IsNaN(p.y)||float.IsInfinity(p.y)||float.IsNaN(p.z)||float.IsInfinity(p.z))throw new Exception("Invalid particle");nl+=Vector3.Distance(p,g.particlePosition[k-sb]);}
            maxLengthChange=Mathf.Max(maxLengthChange,Mathf.Abs(ol-nl));
        }
        if(rootError>1e-6f||tipError>1e-6f||Mathf.Abs(g.strandParamsMax.y-old.strandParamsMax.y)>1e-8f)throw new Exception("Root/tip/diameter changed");
        var result=new{passed=true,g.strandCount,g.strandParticleCount,particles=g.particlePosition.Length,rootError,tipError,maxLengthChange,diameter=g.strandParamsMax.y,oldParticles=old.particlePosition.Length,original=Original,variant=Variant};Save("groom_geometry",result);return result;
    }
    public static object BuildEndpointVariant()
    {
        Guard();const string abc="Assets/_Sandbox/Characters/Jiwoo New/Groom/Jiwoo_Scalp_32_HeadLocal.abc";
        if(!File.Exists(Output+"/alembic_readback.json"))throw new Exception("Independent readback required");
        AssetDatabase.ImportAsset(abc,ImportAssetOptions.ForceSynchronousImport);var importer=AssetImporter.GetAtPath(abc);var so=new SerializedObject(importer);
        so.FindProperty("streamSettings.scaleFactor").floatValue=1;
        foreach(var f in new[]{"importCurves","swapHandedness","importXform"})so.FindProperty("streamSettings."+f).boolValue=true;
        foreach(var f in new[]{"createCurveRenderers","importMeshes","importCameras","importPoints"})so.FindProperty("streamSettings."+f).boolValue=false;
        so.ApplyModifiedPropertiesWithoutUndo();importer.SaveAndReimport();
        var next=AssetDatabase.LoadAssetAtPath<HairAsset>(Variant);if(!next)throw new Exception("Run backed-up initial variant setup first");
        next.settingsAlembic.alembicAsset=AssetDatabase.LoadAssetAtPath<AlembicStreamPlayer>(abc);next.settingsAlembic.settingsResolve.resampleCurves=false;
        HairAssetBuilder.BuildHairAsset(next,HairAssetBuilder.BuildFlags.DisableProgress);EditorUtility.SetDirty(next);AssetDatabase.SaveAssetIfDirty(next);return CheckGeometry();
    }
    public static object Apply()
    {
        Guard();CheckGeometry();var r=PrefabUtility.LoadPrefabContents(Prefab);
        try{
            var hair=r.GetComponentInChildren<HairInstance>(true);if(AssetDatabase.GetAssetPath(hair.strandGroupProviders.Single().hairAsset)!=Original)throw new Exception("Unexpected currently installed groom");
            hair.strandGroupProviders=new[]{new HairInstance.GroupProvider{hairAsset=AssetDatabase.LoadAssetAtPath<HairAsset>(Variant)}};
            hair.settingsExecutive.updateSimulationRate=HairInstance.SettingsExecutive.UpdateRate.Fixed30Hz;hair.settingsExecutive.updateSimulationInEditor=false;
            HairInstanceBuilder.BuildHairInstance(hair,hair.strandGroupProviders);PrefabUtility.SaveAsPrefabAsset(r,Prefab);
        }finally{PrefabUtility.UnloadPrefabContents(r);}
        return Verify();
    }
    public static object Verify()
    {
        Guard();CheckGeometry();var before=JsonConvert.DeserializeObject<Dictionary<string,string>>(File.ReadAllText(Output+"/unrelated_prefab_blocks.json"));var after=UnrelatedBlocks(Prefab);
        if(before.Count!=after.Count||before.Any(p=>!after.ContainsKey(p.Key)||after[p.Key]!=p.Value))throw new Exception("Unrelated prefab blocks changed");
        foreach(var p in JsonConvert.DeserializeObject<Dictionary<string,string>>(File.ReadAllText(Output+"/protected_hashes.json")))if(Hash(p.Key)!=p.Value)throw new Exception("Protected asset changed: "+p.Key);
        var r=PrefabUtility.LoadPrefabContents(Prefab);
        try{
            var h=r.GetComponentInChildren<HairInstance>(true);if(h.settingsExecutive.updateSimulationRate!=HairInstance.SettingsExecutive.UpdateRate.Fixed30Hz||h.settingsExecutive.updateSimulationInEditor)throw new Exception("Wrong scheduling");
            if(AssetDatabase.GetAssetPath(h.strandGroupProviders.Single().hairAsset)!=Variant||AssetDatabase.GetAssetPath(h.strandGroupInstances.Single().groupAssetReference.hairAsset)!=Variant)throw new Exception("Saved provider/group mismatch");
            if(h.strandGroupInstances.Single().sceneObjects.rootMeshFilter.sharedMesh!=h.strandGroupProviders[0].hairAsset.strandGroups[0].meshAssetRoots)throw new Exception("Roots mesh reference mismatch");
            if(h.transform.parent.name!="DEF-spine.006"||h.transform.localPosition!=Vector3.zero||h.transform.localRotation!=Quaternion.identity||h.transform.localScale!=Vector3.one)throw new Exception("Head attachment changed");
            var errors=r.GetComponent<Sandbox.CharacterSystem.Character>().ValidateConfiguration();int missing=r.GetComponentsInChildren<Transform>(true).Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));if(errors.Count>0||missing>0)throw new Exception("Character validation failed");
            var scenes=UnityEngine.Object.FindObjectsByType<HairInstance>(FindObjectsInactive.Include).Where(x=>x.name=="Jiwoo_ScalpHair"&&x.gameObject.scene.isLoaded&&x.gameObject.scene!=r.scene).Select(x=>new{x.gameObject.scene.path,x.gameObject.scene.isDirty,groom=AssetDatabase.GetAssetPath(x.strandGroupProviders.Single().hairAsset),rate=x.settingsExecutive.updateSimulationRate.ToString(),editModeSimulation=x.settingsExecutive.updateSimulationInEditor}).ToArray();
            var result=new{passed=true,preservedUnrelatedBlocks=before.Count,variant=Variant,particles=3215936,simulationHz=30,missingScripts=missing,configurationErrors=errors,headBoundary=h.settingsEnvironment.boundaryResident[0].settings.type.ToString(),editModeSimulation=false,scenes,runtimeTested=false};Save("saved_prefab_verification",result);return result;
        }finally{PrefabUtility.UnloadPrefabContents(r);}
    }
}
