using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Unity.DemoTeam.Hair;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public static class RepairJiwooScalpBoundary
{
    const string Prefab="Assets/_Sandbox/Characters/Jiwoo/Jiwoo_Normalized.prefab";
    const string Output="Data/JiwooRigged/ScalpBoundaryRepair";
    const string TexturePath="Assets/_Sandbox/Characters/Jiwoo New/Groom/Collision/Jiwoo_HeadSDF.asset";
    static void Guard(){if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("User controls Play Mode; Edit Mode required");Directory.CreateDirectory(Output);}
    static float[] V(Vector3 p)=>new[]{p.x,p.y,p.z};
    static Vector3 Vec(JToken p)=>new Vector3((float)p[0],(float)p[1],(float)p[2]);
    static Transform Head(GameObject r)=>r.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="DEF-spine.006");
    static void Save(string name,object data)=>File.WriteAllText(Output+"/"+name+".json",JsonConvert.SerializeObject(data,Formatting.Indented));
    public static object Audit()
    {
        Guard();Directory.CreateDirectory(Output+"/Before");
        foreach(var p in new[]{Prefab,Prefab+".meta"}){var dst=Output+"/Before/"+Path.GetFileName(p);if(!File.Exists(dst))File.Copy(p,dst);}
        var r=PrefabUtility.LoadPrefabContents(Prefab);
        try{
            var h=Head(r);var face=r.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(s=>s.name=="Face");var mesh=face.sharedMesh;
            int sub=Array.FindIndex(face.sharedMaterials,m=>m&&m.name=="Jiwoo_Head");if(sub<0)throw new Exception("Head material section missing");
            using(var w=new BinaryWriter(File.Create(Output+"/head_vertices.bin")))foreach(var p in mesh.vertices){var v=h.InverseTransformPoint(face.transform.TransformPoint(p));w.Write(v.x);w.Write(v.y);w.Write(v.z);}
            using(var w=new BinaryWriter(File.Create(Output+"/head_triangles.bin")))foreach(var i in mesh.GetTriangles(sub))w.Write(i);
            var hair=r.GetComponentInChildren<HairInstance>(true);var g=hair.strandGroupProviders.Single().hairAsset.strandGroups.Single();
            using(var w=new BinaryWriter(File.Create(Output+"/roots.bin")))for(int i=0;i<g.strandCount;i++){HairAssetUtility.DeclareStrandIterator(g,i,out int b,out int stride,out int end);var p=g.particlePosition[b];w.Write(p.x);w.Write(p.y);w.Write(p.z);}
            var result=new{headSubmesh=sub,triangles=mesh.GetTriangles(sub).Length/3,headPosition=V(r.transform.InverseTransformPoint(h.position)),boundary=EditorJsonUtility.ToJson(hair.settingsEnvironment.boundaryResident[0]),hair=EditorJsonUtility.ToJson(hair)};
            Save("before",result);return new{result.headSubmesh,result.triangles};
        }finally{PrefabUtility.UnloadPrefabContents(r);}
    }
    public static object Apply()
    {
        Guard();var report=JObject.Parse(File.ReadAllText(Output+"/sdf_verification.json"));if(!(bool)report["passed"])throw new Exception("SDF verification required");
        Directory.CreateDirectory(Path.GetDirectoryName(TexturePath));AssetDatabase.Refresh();
        var config=JObject.Parse(File.ReadAllText(Output+"/sdf_config.json"));int n=(int)config["resolution"];
        var texture=AssetDatabase.LoadAssetAtPath<Texture3D>(TexturePath);
        if(texture)throw new Exception("Collision asset exists; do not replace silently");
        texture=new Texture3D(n,n,n,TextureFormat.RFloat,false){name="Jiwoo_HeadSDF",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
        var bytes=File.ReadAllBytes(Output+"/head_sdf.f32");var values=new float[bytes.Length/4];Buffer.BlockCopy(bytes,0,values,0,bytes.Length);
        texture.SetPixelData(values,0);texture.Apply(false,false);AssetDatabase.CreateAsset(texture,TexturePath);AssetDatabase.SaveAssetIfDirty(texture);
        var r=PrefabUtility.LoadPrefabContents(Prefab);
        try{
            var hair=r.GetComponentInChildren<HairInstance>(true);var boundary=hair.settingsEnvironment.boundaryResident.Single(b=>b.name=="Jiwoo_HairBoundary_Head");
            boundary.transform.SetParent(Head(r),false);boundary.transform.localPosition=Vec(config["center"]);boundary.transform.localRotation=Quaternion.identity;boundary.transform.localScale=Vector3.one;
            boundary.settings.type=HairBoundary.Settings.Type.DiscreteSDF;boundary.settings.mode=HairBoundary.Settings.Mode.Standalone;
            boundary.settingsSDF.kSDFTexture=texture;boundary.settingsSDF.kSDFWorldSize=Vec(config["size"]);
            PrefabUtility.SaveAsPrefabAsset(r,Prefab);
        }finally{PrefabUtility.UnloadPrefabContents(r);}
        return Verify();
    }
    public static object Verify()
    {
        Guard();var r=PrefabUtility.LoadPrefabContents(Prefab);
        try{
            var hair=r.GetComponentInChildren<HairInstance>(true);var boundary=hair.settingsEnvironment.boundaryResident.Single(b=>b.name=="Jiwoo_HairBoundary_Head");
            var data=new HairBoundary.RuntimeData();if(!HairBoundary.TryGetData(boundary,ref data)||data.type!=HairBoundary.RuntimeData.Type.SDF)throw new Exception("Boundary not resolved as SDF");
            if(boundary.transform.parent!=Head(r)||boundary.settingsSDF.kSDFTexture!=AssetDatabase.LoadAssetAtPath<Texture3D>(TexturePath))throw new Exception("Head attachment/texture mismatch");
            if(hair.settingsExecutive.updateSimulationInEditor||hair.settingsEnvironment.boundaryResident.Length!=4)throw new Exception("Scheduling/boundary count changed");
            float maxSampleError=0;
            var probes=JArray.Parse(File.ReadAllText(Output+"/unity_probes.json"));
            var uvList=new List<Vector3>();var head=Head(r);var neutral=head.localRotation;
            var poses=new[]{Quaternion.identity,Quaternion.Euler(0,45,0),Quaternion.Euler(25,-35,15)};
            foreach(var pose in poses){head.localRotation=neutral*pose;HairBoundary.TryGetData(boundary,ref data);uvList.AddRange(probes.Select(p=>data.xform.matrix.inverse.MultiplyPoint3x4(head.TransformPoint(Vec(p["point"])))));}
            head.localRotation=neutral;HairBoundary.TryGetData(boundary,ref data);var uv=uvList.ToArray();
            // Verify the same hardware trilinear SampleLevel path used by HairSim.
            // Texture3D.GetPixelBilinear has different CPU texel-center semantics.
            const string shaderPath="Assets/_Sandbox/Characters/Jiwoo New/Groom/Collision/Jiwoo_HeadSDFProbe.compute";
            try{
                File.Copy("Tools/Hair/VerifyJiwooHeadSdf.compute",shaderPath,true);AssetDatabase.ImportAsset(shaderPath,ImportAssetOptions.ForceSynchronousImport);
                var shader=AssetDatabase.LoadAssetAtPath<ComputeShader>(shaderPath);var kernel=shader.FindKernel("SampleSdf");
                using(var input=new ComputeBuffer(uv.Length,12))using(var output=new ComputeBuffer(uv.Length,4)){
                    input.SetData(uv);shader.SetBuffer(kernel,"_UV",input);shader.SetBuffer(kernel,"_Distance",output);shader.SetTexture(kernel,"_HeadSdf",boundary.settingsSDF.kSDFTexture);shader.SetInt("_Count",uv.Length);shader.Dispatch(kernel,(uv.Length+63)/64,1,1);
                    var actual=new float[uv.Length];output.GetData(actual);
                    for(int i=0;i<actual.Length;i++)maxSampleError=Mathf.Max(maxSampleError,Mathf.Abs(actual[i]*data.shape.data.tA-(float)probes[i%probes.Count]["expected"]));
                }
            }finally{AssetDatabase.DeleteAsset(shaderPath);}
            if(maxSampleError>.0001f)throw new Exception("Unity SDF frame/sampling error: "+maxSampleError);
            // Compare the complete serialized prefab to its pre-repair version except
            // the head boundary transform and component, the only authorized edits.
            var before=File.ReadAllText(Output+"/Before/Jiwoo_Normalized.prefab");var after=File.ReadAllText(Prefab);
            Func<string,Dictionary<string,string>> blocks=text=>System.Text.RegularExpressions.Regex.Split(text,@"(?m)(?=^--- !u!)").Where(s=>s.StartsWith("--- !u!")).ToDictionary(s=>s.Split('\n')[0].Trim(),s=>s);
            var a=blocks(before);var b=blocks(after);var changed=a.Keys.Union(b.Keys).Where(k=>!a.ContainsKey(k)||!b.ContainsKey(k)||a[k]!=b[k]).ToArray();
            if(changed.Length!=2||changed.Any(k=>!k.StartsWith("--- !u!4 ")&&!k.StartsWith("--- !u!114 ")))throw new Exception("Unexpected prefab block changes: "+string.Join(",",changed));
            var errors=r.GetComponent<Sandbox.CharacterSystem.Character>().ValidateConfiguration();if(errors.Count>0)throw new Exception(string.Join(";",errors));
            var result=new{passed=true,changedBlocks=changed,texture=TexturePath,textureResolution=((Texture3D)boundary.settingsSDF.kSDFTexture).width,center=V(boundary.transform.localPosition),size=V(boundary.settingsSDF.kSDFWorldSize),maxSampleError,poseChecks=poses.Length,gpuSamples=uv.Length,boundaryCount=4,missingScripts=r.GetComponentsInChildren<Transform>(true).Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)),configurationErrors=errors,editModeSimulation=false,runtimeTested=false};
            Save("saved_prefab_verification",result);return result;
        }finally{PrefabUtility.UnloadPrefabContents(r);}
    }
}
