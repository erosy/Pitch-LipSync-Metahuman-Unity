var h = UnityEngine.Object.FindObjectsByType<Unity.DemoTeam.Hair.HairInstance>(UnityEngine.FindObjectsInactive.Exclude).First();
var group = h.strandGroupProviders[0].hairAsset.strandGroups[0];
var b = h.settingsEnvironment.boundaryResident[0];
Unity.DemoTeam.Hair.HairBoundary.RuntimeData boundary = default;
var boundaryValid = Unity.DemoTeam.Hair.HairBoundary.TryGetData(b, ref boundary);
var data = new UnityEngine.Vector3[h.solverData[0].buffers._ParticlePosition.count];
h.solverData[0].buffers._ParticlePosition.GetData(data);
var nonFinite = 0; var samples = 0; var deep = 0; float worst = 0;
var inverse = boundary.xform.matrix.inverse;
var texture = b.settingsSDF.kSDFTexture as UnityEngine.Texture3D;
for (int i = group.strandCount; i < data.Length; i += 97)
{
    var v = data[i];
    if (!float.IsFinite(v.x) || !float.IsFinite(v.y) || !float.IsFinite(v.z)) { nonFinite++; continue; }
    var uvw = inverse.MultiplyPoint3x4(v);
    if (uvw.x < 0 || uvw.y < 0 || uvw.z < 0 || uvw.x > 1 || uvw.y > 1 || uvw.z > 1) continue;
    var distance = texture.GetPixelBilinear(uvw.x, uvw.y, uvw.z).r;
    samples++; if (distance < -0.002f) deep++; worst = UnityEngine.Mathf.Min(worst, distance);
}
var roots = 0; float rootError = 0;
var matrix = h.strandGroupInstances[0].sceneObjects.rootMeshFilter.transform.localToWorldMatrix;
for (int i = 0; i < group.strandCount; i += 113)
{
    rootError = UnityEngine.Mathf.Max(rootError, UnityEngine.Vector3.Distance(data[i], matrix.MultiplyPoint3x4(group.particlePosition[i]))); roots++;
}
var result = new { playing = UnityEditor.EditorApplication.isPlaying, elapsed = h.execState.elapsedTime, boundaryValid, sdfSamples = samples, samplesDeeperThan2mm = deep, worstSdfMm = worst * 1000, nonFinite, testedRoots = roots, maxRootTrackingErrorMm = rootError * 1000, strands = group.strandCount, particles = group.strandParticleCount };
System.IO.File.WriteAllText("C:/PROJECTS/Metahuman Unity Lip Sync Workaround/Temp/fitted_hair_validation.json", Newtonsoft.Json.JsonConvert.SerializeObject(result));
return result;
