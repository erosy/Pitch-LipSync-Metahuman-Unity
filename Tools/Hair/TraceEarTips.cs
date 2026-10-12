var h=UnityEngine.Object.FindObjectsByType<Unity.DemoTeam.Hair.HairInstance>(UnityEngine.FindObjectsInactive.Exclude).First();
var g=h.strandGroupProviders[0].hairAsset.strandGroups[0];var camera=UnityEngine.Camera.main;
var d=new UnityEngine.Vector3[h.solverData[0].buffers._ParticlePosition.count];h.solverData[0].buffers._ParticlePosition.GetData(d);
var result=new System.Collections.Generic.List<object>();
for(int i=0;i<g.strandCount;i++){
    int tip=i+31*g.strandCount;var p=camera.WorldToScreenPoint(d[tip]);
    var y=camera.pixelHeight-p.y;
    if(p.x<1015||p.x>1080||y<370||y>430)continue;
    var points=new float[32][];
    for(int j=0;j<32;j++){var v=camera.WorldToScreenPoint(d[i+j*g.strandCount]);points[j]=new[]{v.x,camera.pixelHeight-v.y};}
    result.Add(new{strand=i,x=p.x,y,points});
}
System.IO.File.WriteAllText("Docs/HairRendering/EarCorrection/TipTrace.json",Newtonsoft.Json.JsonConvert.SerializeObject(result));
return new{tips=result.Count};
