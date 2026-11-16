using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Unity.DemoTeam.Hair;
using UnityEngine.Formats.Alembic.Importer;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// Scalp-only installer. Never invokes the historical character installer.
public static class RestoreJiwooScalp
{
    const string Prefab="Assets/_Sandbox/Characters/Jiwoo/Jiwoo_Normalized.prefab";
    const string Model="Assets/_Sandbox/Characters/Jiwoo New/Jiwoo_EthanSetup.fbx";
    const string Groom="Assets/_Sandbox/Characters/Jiwoo/Groom/Rigged/Jiwoo_Scalp.asset";
    const string Abc="Assets/_Sandbox/Characters/Jiwoo/Groom/Rigged/Jiwoo_Scalp_HeadLocal.abc";
    const string Material="Assets/_Sandbox/Characters/Jiwoo/Materials/Jiwoo_StrandHair.mat";
    const string Output="Data/JiwooRigged/ScalpRestore";
    static string Hash(string p) { using(var s=SHA256.Create())return BitConverter.ToString(s.ComputeHash(File.ReadAllBytes(p))).Replace("-",""); }
    static void Guard(){if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Edit Mode required; user controls Play Mode.");Directory.CreateDirectory(Output);}
    static void Save(string name,object o)=>File.WriteAllText(Output+"/"+name+".json",JsonConvert.SerializeObject(o,Formatting.Indented));
    static void Backup(string p){var dest=Output+"/Before/"+p;Directory.CreateDirectory(Path.GetDirectoryName(dest));if(!File.Exists(dest))File.Copy(p,dest);}
    static Transform Bone(GameObject r,string name)=>r.GetComponentsInChildren<Transform>(true).Single(t=>t.name==name);
    static float[] Vec(Vector3 v)=>new[]{v.x,v.y,v.z};
    static float[] Matrix(Matrix4x4 m)=>Enumerable.Range(0,16).Select(i=>m[i/4,i%4]).ToArray();
    static string Ref(UnityEngine.Object o,GameObject root)
    {
        if(!o)return "null";
        var c=o as Component;var go=o as GameObject;
        var t=c?c.transform:go?go.transform:null;
        if(t&&(t==root.transform||t.IsChildOf(root.transform)))return "local:"+AnimationUtility.CalculateTransformPath(t,root.transform)+":"+o.GetType().FullName;
        if(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(o,out string guid,out long id))return guid+":"+id;
        return o.GetType().FullName+":"+o.name;
    }
    // Normalize transient instance IDs so comparisons survive prefab unload/reload.
    static Dictionary<string,string> Contracts(GameObject root)
    {
        var result=new Dictionary<string,string>();
        foreach(var c in root.GetComponentsInChildren<Component>(true))
        {
            if(!c)throw new Exception("Missing script");
            if(c is HairInstance||c is HairBoundary||c is CharacterHairStartupWarmup||c.transform.GetComponentInParent<HairInstance>())continue;
            string path=AnimationUtility.CalculateTransformPath(c.transform,root.transform)+":"+c.GetType().FullName;
            string json=EditorJsonUtility.ToJson(c);
            if(c is Transform){var t=(Transform)c;json=JsonConvert.SerializeObject(new{position=Vec(t.localPosition),rotation=new[]{t.localRotation.x,t.localRotation.y,t.localRotation.z,t.localRotation.w},scale=Vec(t.localScale),parent=Ref(t.parent,root)});}
            else if(c is CharacterReadiness){var obj=JObject.Parse(json);obj["MonoBehaviour"]?["startupWarmup"]?.Parent.Remove();json=obj.ToString(Formatting.None);}
            json=Regex.Replace(json,"\\\"instanceID\\\":(-?[0-9]+)",m=>"\"reference\":"+JsonConvert.SerializeObject(Ref((UnityEngine.Object)typeof(EditorUtility).GetMethod("InstanceIDToObject").Invoke(null,new object[]{int.Parse(m.Groups[1].Value)}),root)));
            result.Add(path,json);
        }
        return result;
    }
    static void CheckContracts(Dictionary<string,string> before,GameObject root)
    {
        var after=Contracts(root);var errors=before.Where(p=>!after.ContainsKey(p.Key)||after[p.Key]!=p.Value).Select(p=>p.Key).ToArray();
        if(errors.Length>0)throw new Exception("Existing character contracts changed: "+string.Join(", ",errors));
    }
    static void SetRef(UnityEngine.Object o,string field,UnityEngine.Object v){var s=new SerializedObject(o);s.FindProperty(field).objectReferenceValue=v;s.ApplyModifiedPropertiesWithoutUndo();}
    static HairBoundary Capsule(GameObject root,string name,string bone,Vector3 center,float radius,float height,HairBoundary.Axis axis)
    {
        var go=new GameObject(name);go.transform.SetParent(Bone(root,bone),false);
        go.transform.position=root.transform.TransformPoint(center);go.transform.rotation=root.transform.rotation;
        var b=go.AddComponent<HairBoundary>();b.settings.mode=HairBoundary.Settings.Mode.Standalone;b.settings.type=HairBoundary.Settings.Type.Capsule;
        b.settingsCapsule.direction=axis;b.settingsCapsule.radius=radius;b.settingsCapsule.height=height;
        return b;
    }
    public static object Install()
    {
        Guard();
        if(!(bool)JObject.Parse(File.ReadAllText(Output+"/blender_fit.json"))["reuse_passed"]||!(bool)JObject.Parse(File.ReadAllText(Output+"/unity_fit_verification.json"))["passed"])throw new Exception("Fit verification must pass first");
        var root=PrefabUtility.LoadPrefabContents(Prefab);
        try{
            if(root.GetComponentsInChildren<HairInstance>(true).Length!=0)throw new Exception("Scalp already installed; use Verify rather than duplicate");
            var before=Contracts(root);Save("preserved_contracts",before);
            var head=Bone(root,"DEF-spine.006");
            var go=new GameObject("Jiwoo_ScalpHair");go.SetActive(false);go.transform.SetParent(head,false);
            var hair=go.AddComponent<HairInstance>();
            hair.settingsExecutive.updateSimulationInEditor=false;
            hair.settingsExecutive.updateSimulation=true;
            hair.settingsExecutive.updateSimulationRate=HairInstance.SettingsExecutive.UpdateRate.Fixed60Hz;
            hair.settingsExecutive.updateStepsMaxValue=2;
            hair.strandGroupProviders=new[]{new HairInstance.GroupProvider{hairAsset=AssetDatabase.LoadAssetAtPath<HairAsset>(Groom)}};
            hair.strandGroupDefaults.settingsGeometry.strandScale=HairSim.SettingsGeometry.StrandScale.Fixed;
            hair.strandGroupDefaults.settingsGeometry.strandLength=false;hair.strandGroupDefaults.settingsGeometry.strandDiameter=false;
            hair.strandGroupDefaults.settingsRendering.material=true;
            hair.strandGroupDefaults.settingsRendering.materialAsset=AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(Material);
            // Conservative groom-retention baseline; final settling is user Play Mode review.
            var physics=hair.strandGroupDefaults.settingsPhysics;
            physics.solverSubsteps=2;physics.constraintIterations=8;physics.constraintStiffness=1;
            physics.gravity=.2f;physics.dampingLinear=true;physics.dampingLinearFactor=.2f;
            physics.localShapeBiasValue=.7f;physics.globalPosition=true;physics.globalPositionInfluence=.3f;
            physics.globalRotation=true;physics.globalRotationInfluence=.7f;
            physics.globalFade=true;physics.globalFadeOffset=.1f;physics.globalFadeExtent=.2f;
            hair.strandGroupDefaults.settingsPhysics=physics;
            hair.settingsVolumetrics.gridResolution=32;
            hair.settingsEnvironment.boundaryCapture=false;
            hair.settingsEnvironment.defaultSolidMargin=.15f; // centimeters (1.5 mm)
            hair.settingsEnvironment.boundaryResident=new[]{
                Capsule(root,"Jiwoo_HairBoundary_Head","DEF-spine.006",new Vector3(0,1.655f,-.005f),.073f,.19f,HairBoundary.Axis.YAxis),
                Capsule(root,"Jiwoo_HairBoundary_Neck","DEF-spine.005",new Vector3(0,1.50f,.003f),.041f,.135f,HairBoundary.Axis.YAxis),
                Capsule(root,"Jiwoo_HairBoundary_ShoulderL","DEF-shoulder.L",new Vector3(.105f,1.393f,.015f),.052f,.18f,HairBoundary.Axis.XAxis),
                Capsule(root,"Jiwoo_HairBoundary_ShoulderR","DEF-shoulder.R",new Vector3(-.105f,1.393f,.015f),.052f,.18f,HairBoundary.Axis.XAxis)};
            HairInstanceBuilder.BuildHairInstance(hair,hair.strandGroupProviders);go.SetActive(true);
            var warmup=root.AddComponent<CharacterHairStartupWarmup>();SetRef(warmup,"targetAnimator",root.GetComponent<Animator>());
            var data=new SerializedObject(warmup);var list=data.FindProperty("strandHair");list.arraySize=1;list.GetArrayElementAtIndex(0).objectReferenceValue=hair;data.ApplyModifiedPropertiesWithoutUndo();
            SetRef(root.GetComponent<CharacterReadiness>(),"startupWarmup",warmup);
            CheckContracts(before,root);
            PrefabUtility.SaveAsPrefabAsset(root,Prefab);
        }finally{PrefabUtility.UnloadPrefabContents(root);}
        return Verify();
    }
    public static object Verify()
    {
        Guard();var root=PrefabUtility.LoadPrefabContents(Prefab);
        try{
            var before=JsonConvert.DeserializeObject<Dictionary<string,string>>(File.ReadAllText(Output+"/preserved_contracts.json"));CheckContracts(before,root);
            var hair=root.GetComponentsInChildren<HairInstance>(true).Single();var t=hair.transform;
            if(t.parent!=Bone(root,"DEF-spine.006")||t.localPosition!=Vector3.zero||t.localRotation!=Quaternion.identity||t.localScale!=Vector3.one)throw new Exception("Attachment transform incorrect");
            if(hair.settingsExecutive.updateSimulationInEditor||!hair.settingsExecutive.updateSimulation)throw new Exception("Simulation scheduling incorrect");
            if(hair.strandGroupDefaults.settingsGeometry.strandScale!=HairSim.SettingsGeometry.StrandScale.Fixed)throw new Exception("Strand scale must be Fixed");
            if(hair.strandGroupProviders.Single().hairAsset!=AssetDatabase.LoadAssetAtPath<HairAsset>(Groom))throw new Exception("Groom mismatch");
            if(!hair.strandGroupDefaults.settingsRendering.material||hair.strandGroupDefaults.settingsRendering.materialAsset!=AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(Material))throw new Exception("Material mismatch");
            if(hair.strandGroupInstances.Length!=1||hair.strandGroupInstances[0].groupAssetReference.hairAsset!=hair.strandGroupProviders[0].hairAsset)throw new Exception("Generated group mismatch");
            if(hair.settingsEnvironment.boundaryResident.Length!=4||hair.settingsEnvironment.boundaryResident.Any(b=>!b||!b.transform.IsChildOf(root.transform)))throw new Exception("Invalid collision reference");
            var warmup=root.GetComponent<CharacterHairStartupWarmup>();var so=new SerializedObject(warmup);
            if(so.FindProperty("strandHair").arraySize!=1||so.FindProperty("strandHair").GetArrayElementAtIndex(0).objectReferenceValue!=hair||so.FindProperty("targetAnimator").objectReferenceValue!=root.GetComponent<Animator>())throw new Exception("Warmup wiring incorrect");
            if(new SerializedObject(root.GetComponent<CharacterReadiness>()).FindProperty("startupWarmup").objectReferenceValue!=warmup)throw new Exception("Readiness binding incorrect");
            var audit=JObject.Parse(File.ReadAllText(Output+"/unity_audit.json"));foreach(var p in (JObject)audit["hashes"])if(Hash(p.Key)!=(string)p.Value)throw new Exception("Protected asset changed: "+p.Key);
            if(((ModelImporter)AssetImporter.GetAtPath(Model)).importBlendShapeNormals!=ModelImporterNormals.Import)throw new Exception("Authored normals not preserved");
            var errors=new List<string>();root.GetComponent<CharacterReadiness>().ValidateConfiguration(errors);
            if(errors.Count>0)throw new Exception(string.Join(";",errors));
            var result=new{passed=true,preservedComponents=before.Count,missingScripts=0,prefabGuid=AssetDatabase.AssetPathToGUID(Prefab),attachment=AnimationUtility.CalculateTransformPath(t,root.transform),
                localPosition=Vec(t.localPosition),localRotation=t.localRotation.eulerAngles.ToString(),localScale=Vec(t.localScale),
                groom=Groom,material=Material,strands=hair.strandGroupProviders[0].hairAsset.strandGroups[0].strandCount,
                particles=hair.strandGroupProviders[0].hairAsset.strandGroups[0].particlePosition.Length,
                simulationRate=hair.settingsExecutive.updateSimulationRate.ToString(),editModeSimulation=false,
                boundaries=hair.settingsEnvironment.boundaryResident.Select(b=>new{b.name,parent=b.transform.parent.name,position=Vec(root.transform.InverseTransformPoint(b.transform.position)),radius=b.settingsCapsule.radius,height=b.settingsCapsule.height,axis=b.settingsCapsule.direction.ToString()}).ToArray(),
                scenes=Enumerable.Range(0,SceneManager.sceneCount).Select(i=>SceneManager.GetSceneAt(i)).Where(s=>s.isLoaded&&s!=root.scene).Select(s=>new{s.path,s.isDirty}).ToArray(),runtimeTested=false};
            Save("saved_prefab_verification",result);return result;
        }finally{PrefabUtility.UnloadPrefabContents(root);}
    }
    public static object InspectLoadedScene()
    {
        Guard();
        var characters=UnityEngine.Object.FindObjectsByType<Animator>(FindObjectsInactive.Include).Where(a=>a.name=="Jiwoo_Normalized"&&a.gameObject.scene.isLoaded).Select(a=>{
            var hairs=a.GetComponentsInChildren<HairInstance>(true);
            var character=a.GetComponent<Sandbox.CharacterSystem.Character>();
            return new{a.gameObject.scene.path,a.gameObject.scene.isDirty,active=a.gameObject.activeSelf,
                prefab=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(a.gameObject),scalpCount=hairs.Length,
                hairActive=hairs.Select(h=>h.gameObject.activeSelf).ToArray(),editModeSimulation=hairs.Select(h=>h.settingsExecutive.updateSimulationInEditor).ToArray(),
                configurationErrors=character.ValidateConfiguration(),
                missingScripts=a.GetComponentsInChildren<Transform>(true).Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject))};
        }).ToArray();
        Save("loaded_scene_verification",characters);return characters;
    }
    public static object Audit()
    {
        Guard();
        foreach(var p in new[]{Prefab,Prefab+".meta",Groom,Groom+".meta",Abc,Abc+".meta",Material,Material+".meta",Model,Model+".meta"})Backup(p);
        var root=PrefabUtility.LoadPrefabContents(Prefab);
        try{
            var h=Bone(root,"DEF-spine.006");
            var asset=AssetDatabase.LoadAssetAtPath<HairAsset>(Groom);var g=asset.strandGroups.Single();
            using(var w=new BinaryWriter(File.Create(Output+"/unity_groom.bin")))
                for(int i=0;i<g.strandCount;i++){
                    HairAssetUtility.DeclareStrandIterator(g,i,out int begin,out int stride,out int end);
                    for(int j=begin;j<end;j+=stride){var p=g.particlePosition[j];w.Write(p.x);w.Write(p.y);w.Write(p.z);}
                }
            foreach(var r in root.GetComponentsInChildren<SkinnedMeshRenderer>(true)){
                using(var w=new BinaryWriter(File.Create(Output+"/unity_"+r.name+".bin")))
                    foreach(var p in r.sharedMesh.vertices){var v=h.InverseTransformPoint(r.transform.TransformPoint(p));w.Write(v.x);w.Write(v.y);w.Write(v.z);}
                using(var w=new BinaryWriter(File.Create(Output+"/unity_"+r.name+"_triangles.bin")))foreach(var index in r.sharedMesh.triangles)w.Write(index);
            }
            var abc=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<AlembicStreamPlayer>(Abc));
            try{
                var curves=abc.GetComponentsInChildren<AlembicCurves>(true).Single();
                using(var w=new BinaryWriter(File.Create(Output+"/unity_abc.bin")))foreach(var p in curves.Positions){w.Write(p.x);w.Write(p.y);w.Write(p.z);}
            }finally{UnityEngine.Object.DestroyImmediate(abc.gameObject);}
            var result=new{headMatrix=Matrix(root.transform.worldToLocalMatrix*h.localToWorldMatrix),headScale=Vec(h.lossyScale),
                bones=root.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("DEF-")).Select(t=>new{t.name,position=Vec(root.transform.InverseTransformPoint(t.position)),matrix=Matrix(root.transform.worldToLocalMatrix*t.localToWorldMatrix)}).ToArray(),
                g.strandCount,g.strandParticleCount,diameter=g.strandParamsMax.y,maxLength=g.strandParamsMax.x,
                material=EditorJsonUtility.ToJson(AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(Material)),
                scenes=Enumerable.Range(0,SceneManager.sceneCount).Select(i=>SceneManager.GetSceneAt(i)).Select(s=>new{s.path,s.isDirty}).ToArray(),
                hashes=new[]{Prefab+".meta",Model,Model+".meta",Groom,Abc,Material}.ToDictionary(p=>p,Hash)};
            Save("unity_audit",result);return result;
        }finally{PrefabUtility.UnloadPrefabContents(root);}
    }
}
