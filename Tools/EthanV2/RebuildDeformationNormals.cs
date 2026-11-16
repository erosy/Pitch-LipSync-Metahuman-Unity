using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json;
public static class RebuildEthanDeformationNormals {
 public static object ExportShading(){
  var mesh=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/_Sandbox/Characters/Ethan_V2/Meshes/EthanV2_Head_NormalReview.asset");var p=new Vector3[mesh.vertexCount];var n=new Vector3[mesh.vertexCount];var t=new Vector3[mesh.vertexCount];
  using(var w=new BinaryWriter(File.Create("Data/EthanV2/NormalRepair/corrected_shading.bin")))for(int s=0;s<mesh.blendShapeCount;s++){mesh.GetBlendShapeFrameVertices(s,0,p,n,t);for(int i=0;i<n.Length;i++){w.Write(n[i].x);w.Write(n[i].y);w.Write(n[i].z);w.Write(t[i].x);w.Write(t[i].y);w.Write(t[i].z);}}
  return new{exported=true};
 }
 public static object ImportReview(){if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Edit Mode required");const string path="Assets/_Sandbox/Characters/Ethan_V2/Meshes/EthanV2_Head_NormalReview.asset";File.Copy("Data/EthanV2/NormalRepair/corrected_head.asset",path,true);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);return new{reviewImported=true};}
 public static object Draft(){
  if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Edit Mode required");
  const string path="Assets/_Sandbox/Characters/Ethan_V2/Meshes/EthanV2_Head_FacialHairOptimized.asset";
  var original=AssetDatabase.LoadAssetAtPath<Mesh>(path);var result=UnityEngine.Object.Instantiate(original);var work=UnityEngine.Object.Instantiate(original);
  try{
   var basis=original.vertices;var authoredNormals=original.normals;var authoredTangents=original.tangents;
   work.ClearBlendShapes();work.RecalculateNormals();var neutralNormals=work.normals;
   result.ClearBlendShapes();var dp=new Vector3[original.vertexCount];var dn=new Vector3[original.vertexCount];var dt=new Vector3[original.vertexCount];var positions=new Vector3[original.vertexCount];
   for(int s=0;s<original.blendShapeCount;s++)for(int f=0;f<original.GetBlendShapeFrameCount(s);f++){
    original.GetBlendShapeFrameVertices(s,f,dp,null,null);for(int i=0;i<dp.Length;i++)positions[i]=basis[i]+dp[i];work.vertices=positions;work.RecalculateNormals();var posedNormals=work.normals;
    for(int i=0;i<dp.Length;i++){
     var rotation=Quaternion.FromToRotation(neutralNormals[i],posedNormals[i]);
     dn[i]=rotation*authoredNormals[i]-authoredNormals[i];
     Vector3 tangent=authoredTangents[i];dt[i]=rotation*tangent-tangent;
     if(!float.IsFinite(dn[i].x)||!float.IsFinite(dt[i].x))throw new Exception("Nonfinite shading");
    }
    result.AddBlendShapeFrame(original.GetBlendShapeName(s),original.GetBlendShapeFrameWeight(s,f),dp,dn,dt);
   }
   result.name=original.name;const string draft="Assets/_Sandbox/Characters/Ethan_V2/Meshes/EthanV2_Head_NormalReview.asset";if(AssetDatabase.LoadAssetAtPath<Mesh>(draft))throw new Exception("Draft already exists");AssetDatabase.CreateAsset(result,draft);result=null;
   return new{draft,productionWritten=false,vertices=original.vertexCount,shapes=original.blendShapeCount};
  }finally{if(result)UnityEngine.Object.DestroyImmediate(result);UnityEngine.Object.DestroyImmediate(work);}
 }
}

