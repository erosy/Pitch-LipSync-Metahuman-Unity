using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json;

// Runs through unity command run_script; never enters Play Mode or edits the scene.
public static class RepairCandidateLipNormals {
 const string Target="Assets/_Sandbox/Characters/Ethan_V2/Candidate/EthanV2_Face.asset";
 const string Output="Data/EthanV2/LipNormalRepair";
 const int Start=139924, Count=4252;
 static void Guard(){if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Edit Mode required");}
 static void Save(string name,object value)=>File.WriteAllText(Output+"/"+name+".json",JsonConvert.SerializeObject(value,Formatting.Indented));
 public static object Prepare(){
  Guard();Directory.CreateDirectory(Output);if(File.Exists(Output+"/before_face.asset"))throw new Exception("Backup already exists; do not overwrite");
  var m=AssetDatabase.LoadAssetAtPath<Mesh>(Target);if(m.vertexCount!=149611||m.blendShapeCount!=73)throw new Exception("Unexpected candidate topology");
  File.Copy(Target,Output+"/before_face.asset");File.Copy(Target+".meta",Output+"/before_face.asset.meta");
  var basis=m.vertices;var normals=m.normals;var tangents=m.tangents;var dp=new Vector3[m.vertexCount];
  var positions=new Vector3[Count];var tris=m.triangles.Where(i=>i>=Start&&i<Start+Count).Select(i=>i-Start).ToArray();
  var work=new Mesh{vertices=basis.Skip(Start).Take(Count).ToArray(),triangles=tris};work.RecalculateNormals();var neutral=work.normals;
  try{using(var w=new BinaryWriter(File.Create(Output+"/shading.bin"))){
   for(int s=0;s<m.blendShapeCount;s++){
    if(m.GetBlendShapeFrameCount(s)!=1)throw new Exception("Unexpected multi-frame shape");
    m.GetBlendShapeFrameVertices(s,0,dp,null,null);for(int j=0;j<Count;j++)positions[j]=basis[Start+j]+dp[Start+j];
    work.vertices=positions;work.RecalculateNormals();var posed=work.normals;
    for(int j=0;j<Count;j++){
     var rotation=Quaternion.FromToRotation(neutral[j],posed[j]);int i=Start+j;
     var dn=rotation*normals[i]-normals[i];Vector3 tangent=tangents[i];var dt=rotation*tangent-tangent;
     foreach(var v in new[]{dn,dt}){if(!float.IsFinite(v.x)||!float.IsFinite(v.y)||!float.IsFinite(v.z))throw new Exception("Invalid shading");w.Write(v.x);w.Write(v.y);w.Write(v.z);}
    }
   }
  }}finally{UnityEngine.Object.DestroyImmediate(work);}
  var result=new{m.vertexCount,triangles=m.triangles.Length/3,shapes=m.blendShapeCount,start=Start,count=Count,lipTriangles=tris.Length/3,neutralShadingPreserved=true};Save("preparation",result);return result;
 }
 static void Compare(Mesh a,Mesh b){
  if(a.vertexCount!=b.vertexCount||a.subMeshCount!=b.subMeshCount||!a.vertices.SequenceEqual(b.vertices)||!a.normals.SequenceEqual(b.normals)||!a.tangents.SequenceEqual(b.tangents)||!a.bindposes.SequenceEqual(b.bindposes)||!a.colors32.SequenceEqual(b.colors32)||a.blendShapeCount!=b.blendShapeCount)throw new Exception("Base mesh changed");
  for(int c=0;c<8;c++){var x=new List<Vector4>();var y=new List<Vector4>();a.GetUVs(c,x);b.GetUVs(c,y);if(!x.SequenceEqual(y))throw new Exception("UV changed");}
  for(int c=0;c<a.subMeshCount;c++)if(!a.GetIndices(c).SequenceEqual(b.GetIndices(c)))throw new Exception("Topology changed");
  using(var x=a.GetBonesPerVertex())using(var y=b.GetBonesPerVertex())if(!x.ToArray().SequenceEqual(y.ToArray()))throw new Exception("Skin counts changed");
  using(var x=a.GetAllBoneWeights())using(var y=b.GetAllBoneWeights())if(!x.ToArray().SequenceEqual(y.ToArray()))throw new Exception("Skin weights changed");
  var p=new Vector3[a.vertexCount];var q=new Vector3[p.Length];var n=new Vector3[p.Length];var nn=new Vector3[p.Length];var t=new Vector3[p.Length];var tt=new Vector3[p.Length];int changed=0;
  for(int s=0;s<a.blendShapeCount;s++){
   if(a.GetBlendShapeName(s)!=b.GetBlendShapeName(s)||a.GetBlendShapeFrameCount(s)!=b.GetBlendShapeFrameCount(s)||a.GetBlendShapeFrameWeight(s,0)!=b.GetBlendShapeFrameWeight(s,0))throw new Exception("Shape definition changed");
   a.GetBlendShapeFrameVertices(s,0,p,n,t);b.GetBlendShapeFrameVertices(s,0,q,nn,tt);
   if(!p.SequenceEqual(q))throw new Exception("Expression positions changed");
   for(int i=0;i<p.Length;i++){bool different=!n[i].Equals(nn[i])||!t[i].Equals(tt[i]);if(different){if(i<Start||i>=Start+Count)throw new Exception("Non-lip shading changed");changed++;}}
  }
  if(changed==0)throw new Exception("No shading correction applied");
  Save("verification",new{passed=true,all73PositionArraysExact=true,allNonLipShadingExact=true,neutralShadingExact=true,topologyUVSkinExact=true,changedLipShadingRecords=changed,a.vertexCount,triangles=a.triangles.Length/3,shapes=a.blendShapeCount,memoryMiB=UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(b)/1048576f,playing=EditorApplication.isPlaying});
 }
 public static object Install(){
  Guard();var before=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<Mesh>(Target));
  try{File.Copy(Output+"/corrected_face.asset",Target,true);AssetDatabase.ImportAsset(Target,ImportAssetOptions.ForceUpdate);Compare(before,AssetDatabase.LoadAssetAtPath<Mesh>(Target));}
  catch{File.Copy(Output+"/before_face.asset",Target,true);AssetDatabase.ImportAsset(Target,ImportAssetOptions.ForceUpdate);throw;}
  finally{UnityEngine.Object.DestroyImmediate(before);}
  var asset=AssetDatabase.LoadAssetAtPath<Mesh>(Target);var live=UnityEngine.Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Include).Where(r=>r.sharedMesh==asset).Select(r=>new{r.name,root=r.transform.root.name,scene=r.gameObject.scene.path}).ToArray();
  Save("installation",new{installed=true,target=Target,live,sceneSaved=false});return new{installed=true,live};
 }
}
