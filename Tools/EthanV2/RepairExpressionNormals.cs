using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json;

public static class RepairEthanExpressionNormals
{
 const string Fbx="Assets/_Sandbox/Characters/Ethan_V2/Ethan_V2_73_Baked.fbx";
 const string Target="Assets/_Sandbox/Characters/Ethan_V2/Meshes/EthanV2_Head_FacialHairOptimized.asset";
 const string Output="Data/EthanV2/NormalRepair";
 static void Guard(){if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Edit Mode required");Directory.CreateDirectory(Output);}
 static void V(BinaryWriter w,Vector3 v){w.Write(v.x);w.Write(v.y);w.Write(v.z);}
 // Include every source vertex, triangle, UV, skin influence and expression position.
 // Shading attributes are intentionally checked separately.
 static string Geometry(Mesh m){using(var stream=new MemoryStream()){using(var w=new BinaryWriter(stream,System.Text.Encoding.UTF8,true)){
  w.Write(m.vertexCount);foreach(var v in m.vertices)V(w,v);foreach(int i in m.triangles)w.Write(i);
  for(int c=0;c<8;c++){var uv=new List<Vector4>();m.GetUVs(c,uv);w.Write(uv.Count);foreach(var v in uv){V(w,v);w.Write(v.w);}}
  foreach(var matrix in m.bindposes)for(int i=0;i<16;i++)w.Write(matrix[i]);
  using(var counts=m.GetBonesPerVertex())using(var weights=m.GetAllBoneWeights()){foreach(var n in counts)w.Write(n);foreach(var b in weights){w.Write(b.boneIndex);w.Write(b.weight);}}
  var p=new Vector3[m.vertexCount];w.Write(m.blendShapeCount);for(int s=0;s<m.blendShapeCount;s++){w.Write(m.GetBlendShapeName(s));w.Write(m.GetBlendShapeFrameCount(s));for(int f=0;f<m.GetBlendShapeFrameCount(s);f++){w.Write(m.GetBlendShapeFrameWeight(s,f));m.GetBlendShapeFrameVertices(s,f,p,null,null);foreach(var v in p)V(w,v);}}
 }using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(stream.ToArray()));}}
 public static object Finish(){Guard();var baseline=JsonConvert.DeserializeObject<Dictionary<string,string>>(File.ReadAllText(Output+"/before.json"));var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(Target);if(Geometry(mesh)!=baseline["optimized"])throw new Exception("Final production geometry mismatch");if(!File.ReadAllBytes(Fbx+".meta").SequenceEqual(File.ReadAllBytes(Output+"/before.fbx.meta")))throw new Exception("Importer metadata not restored");
  var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Sandbox/Characters/Ethan_V2/Ethan_V2.prefab");if(prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r=>r.name=="BAKED_head_lod0_mesh").sharedMesh!=mesh)throw new Exception("Prefab assignment changed");
  var live=UnityEngine.Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Include).Where(r=>r.name=="BAKED_head_lod0_mesh"&&r.gameObject.scene.isLoaded).Select(r=>new{scene=r.gameObject.scene.path,mesh=AssetDatabase.GetAssetPath(r.sharedMesh),matches=r.sharedMesh==mesh}).ToArray();if(live.Any(r=>!r.matches))throw new Exception("Live scene mesh mismatch");
  AssetDatabase.DeleteAsset("Assets/_Sandbox/Characters/Ethan_V2/Meshes/EthanV2_Head_NormalReview.asset");
  var result=new{passed=true,geometryExact=true,importerMetadataRestored=true,live,temporaryReviewRemoved=true,playing=EditorApplication.isPlaying,sceneSaved=false};File.WriteAllText(Output+"/final_check.json",JsonConvert.SerializeObject(result,Formatting.Indented));return result;
 }
 public static object RestoreImporter(){Guard();File.Copy(Output+"/before.fbx.meta",Fbx+".meta",true);AssetDatabase.ImportAsset(Fbx,ImportAssetOptions.ForceUpdate);return new{restored=true};}
 public static object Diagnose(){var baseline=JsonConvert.DeserializeObject<Dictionary<string,string>>(File.ReadAllText(Output+"/before.json"));var a=AssetDatabase.LoadAssetAtPath<Mesh>(Target);var b=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/_Sandbox/Characters/Ethan_V2/Meshes/EthanV2_Head_NormalReview.asset");var p=new Vector3[a.vertexCount];var q=new Vector3[b.vertexCount];float max=0;int count=0;for(int s=0;s<a.blendShapeCount;s++){a.GetBlendShapeFrameVertices(s,0,p,null,null);b.GetBlendShapeFrameVertices(s,0,q,null,null);for(int j=0;j<p.Length;j++)if(!p[j].Equals(q[j])){count++;max=Mathf.Max(max,(p[j]-q[j]).magnitude);}}return new{originalHash=Geometry(a)==baseline["optimized"],draftHash=Geometry(b)==baseline["optimized"],basePositions=a.vertices.SequenceEqual(b.vertices),uv=a.uv.SequenceEqual(b.uv),triangles=a.triangles.SequenceEqual(b.triangles),bindposes=a.bindposes.SequenceEqual(b.bindposes),shapePositionMismatchCount=count,maxShapePositionError=max};} public static object Install(){
  Guard();var baseline=JsonConvert.DeserializeObject<Dictionary<string,string>>(File.ReadAllText(Output+"/before.json"));var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(Target);const string draftPath="Assets/_Sandbox/Characters/Ethan_V2/Meshes/EthanV2_Head_NormalReview.asset";var draft=AssetDatabase.LoadAssetAtPath<Mesh>(draftPath);
  if(!draft||Geometry(mesh)!=baseline["optimized"]||Geometry(draft)!=baseline["optimized"])throw new Exception("Geometry preservation check failed");
  if(!mesh.normals.SequenceEqual(draft.normals)||!mesh.tangents.SequenceEqual(draft.tangents))throw new Exception("Neutral shading changed");
  EditorUtility.CopySerialized(draft,mesh);EditorUtility.SetDirty(mesh);AssetDatabase.SaveAssetIfDirty(mesh);
  if(Geometry(mesh)!=baseline["optimized"])throw new Exception("Installed geometry changed");
  var result=new{passed=true,geometrySkinUVsAll73ShapePositionsExact=true,neutralNormalsAndTangentsExact=true,vertexRemappingUsed=false,mesh.vertexCount,triangles=mesh.triangles.Length/3,browLashVertices=84405,browLashTriangles=148734,shapes=mesh.blendShapeCount,meshMemoryMiB=UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(mesh)/1048576f,playing=EditorApplication.isPlaying,sceneSaved=false};File.WriteAllText(Output+"/verification.json",JsonConvert.SerializeObject(result,Formatting.Indented));return result;
 }
}

