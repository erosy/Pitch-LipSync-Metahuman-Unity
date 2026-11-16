using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json;
public static class InstallCanonicalEthanFace {
 static string ShapeHash(Mesh mesh){using(var stream=new MemoryStream()){using(var w=new BinaryWriter(stream,System.Text.Encoding.UTF8,true)){var p=new Vector3[mesh.vertexCount];var n=new Vector3[p.Length];var t=new Vector3[p.Length];for(int s=0;s<mesh.blendShapeCount;s++){w.Write(mesh.GetBlendShapeName(s));w.Write(mesh.GetBlendShapeFrameWeight(s,0));mesh.GetBlendShapeFrameVertices(s,0,p,n,t);for(int i=0;i<p.Length;i++)foreach(var v in new[]{p[i],n[i],t[i]}){w.Write(v.x);w.Write(v.y);w.Write(v.z);}}}using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(stream.ToArray()));}}
 public static object Run(){
  if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Edit Mode required");const string path="Assets/_Sandbox/Characters/Ethan_V2/Meshes/EthanV2_Head_FacialHairOptimized.asset";var before=AssetDatabase.LoadAssetAtPath<Mesh>(path);var hash=ShapeHash(before);var vertices=before.vertices;var triangles=before.triangles;var normals=before.normals;var tangents=before.tangents;var uv=before.uv;var bind=before.bindposes;
  File.Copy(path,"Data/EthanV2/SmileRepair/before_canonical.asset",true);File.Copy("Data/EthanV2/SmileRepair/canonical_head.asset",path,true);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
  var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(ShapeHash(mesh)!=hash||!mesh.vertices.SequenceEqual(vertices)||!mesh.triangles.SequenceEqual(triangles)||!mesh.normals.SequenceEqual(normals)||!mesh.tangents.SequenceEqual(tangents)||!mesh.uv.SequenceEqual(uv)||!mesh.bindposes.SequenceEqual(bind))throw new Exception("Canonicalization changed mesh values");
  var result=new{passed=true,all73ShapePositionNormalTangentArraysExact=true,baseAttributesExact=true,sourceReimported=true,mesh.vertexCount,triangles=triangles.Length/3,playing=false,sceneSaved=false};File.WriteAllText("Data/EthanV2/SmileRepair/installation.json",JsonConvert.SerializeObject(result,Formatting.Indented));return result;
 }
}
