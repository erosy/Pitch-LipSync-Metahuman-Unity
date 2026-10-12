var h = UnityEngine.Object.FindObjectsByType<Unity.DemoTeam.Hair.HairInstance>(UnityEngine.FindObjectsInactive.Exclude).First();
var g = h.strandGroupProviders[0].hairAsset.strandGroups[0];
var data = new UnityEngine.Vector3[h.solverData[0].buffers._ParticlePosition.count];
h.solverData[0].buffers._ParticlePosition.GetData(data);
var matrix = h.strandGroupInstances[0].sceneObjects.rootMeshFilter.transform.localToWorldMatrix;
var inverse = matrix.inverse;
var b = h.settingsEnvironment.boundaryResident[0];
Unity.DemoTeam.Hair.HairBoundary.RuntimeData boundary = default;
Unity.DemoTeam.Hair.HairBoundary.TryGetData(b, ref boundary);
var sdfInverse = boundary.xform.matrix.inverse;
var texture = b.settingsSDF.kSDFTexture as UnityEngine.Texture3D;
var candidates = new System.Collections.Generic.List<object>();
for(int strand=0;strand<g.strandCount;strand++) {
    int begin=g.particleMemoryLayout==Unity.DemoTeam.Hair.HairAsset.MemoryLayout.Interleaved?strand:strand*g.strandParticleCount;
    int stride=g.particleMemoryLayout==Unity.DemoTeam.Hair.HairAsset.MemoryLayout.Interleaved?g.strandCount:1;
    var tip=g.particlePosition[begin+(g.strandParticleCount-1)*stride];
    if(System.Math.Abs(tip.x)<.065f || tip.y>.11f || tip.y<.005f) continue;
    var rest=new UnityEngine.Vector3[g.strandParticleCount]; var live=new UnityEngine.Vector3[g.strandParticleCount];
    float maximum=0,minRest=1,minLive=1;
    for(int j=0;j<g.strandParticleCount;j++) {
        int k=begin+j*stride; rest[j]=g.particlePosition[k];live[j]=inverse.MultiplyPoint3x4(data[k]);
        maximum=UnityEngine.Mathf.Max(maximum,UnityEngine.Vector3.Distance(rest[j],live[j]));
        var uv=sdfInverse.MultiplyPoint3x4(matrix.MultiplyPoint3x4(rest[j]));
        minRest=UnityEngine.Mathf.Min(minRest,texture.GetPixelBilinear(uv.x,uv.y,uv.z).r);
        uv=sdfInverse.MultiplyPoint3x4(data[k]);
        minLive=UnityEngine.Mathf.Min(minLive,texture.GetPixelBilinear(uv.x,uv.y,uv.z).r);
    }
    candidates.Add(new {strand,maximumMm=maximum*1000,minRestSdfMm=minRest*1000,minLiveSdfMm=minLive*1000,rest=rest.Select(v=>new[]{v.x,v.y,v.z}).ToArray(),live=live.Select(v=>new[]{v.x,v.y,v.z}).ToArray()});
}
System.IO.Directory.CreateDirectory("Docs/HairRendering/EarDiagnosis");
System.IO.File.WriteAllText("Docs/HairRendering/EarDiagnosis/Strands.json",Newtonsoft.Json.JsonConvert.SerializeObject(candidates));
return new { elapsed=h.execState.elapsedTime,candidates=candidates.Count };
