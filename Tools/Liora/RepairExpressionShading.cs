using System;using System.IO;using System.Linq;using System.Collections.Generic;using UnityEngine;using UnityEditor;using Newtonsoft.Json;using Newtonsoft.Json.Linq;
public static class RepairLioraExpressionShading {
 const string Target="Assets/_Sandbox/Characters/Liora/Candidate/Liora_Face.asset";const string Output="Data/Liora/ShadingRepair/";
 static JToken Sections()=>JObject.Parse(File.ReadAllText("Data/Liora/HierarchyRepair/sections.json"))["sections"];
 static JToken[] Scope()=>Sections().Where(s=>((string)s["name"]).StartsWith("BAKED_head_lod0_mesh")).ToArray();
 static void Guard(){if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Edit Mode required");}
 static void Write(BinaryWriter w,Vector3 p){w.Write(p.x);w.Write(p.y);w.Write(p.z);}
 public static object Prepare(){
 Guard();if(File.Exists(Output+"before_face.asset"))throw new Exception("Backup exists; refuse overwrite");
 var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(Target);var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Sandbox/Characters/Liora/Candidate/Liora_Candidate.prefab");if(prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r=>r.name=="Face").sharedMesh!=mesh)throw new Exception("Target mesh is no longer current");
 var sections=Scope();if(mesh.vertexCount!=Sections().Sum(s=>(int)s["vertices"])||mesh.blendShapeCount!=73||sections.Length!=3)throw new Exception("Unexpected current topology/shape contract");
 File.Copy(Target,Output+"before_face.asset");File.Copy(Target+".meta",Output+"before_face.asset.meta");
 var basis=mesh.vertices;var normals=mesh.normals;var tangents=mesh.tangents;var delta=new Vector3[mesh.vertexCount];var oldNormal=new Vector3[delta.Length];var oldTangent=new Vector3[delta.Length];var work=new List<Mesh>();var geometricNeutral=new List<Vector3[]>();var positions=new List<Vector3[]>();var ranges=new List<object>();
 try{
 foreach(var section in sections){int start=(int)section["offset"],count=(int)section["vertices"];var tri=Enumerable.Range((int)section["firstSubmesh"],(int)section["submeshes"]).SelectMany(mesh.GetTriangles).Select(i=>i-start).ToArray();if(tri.Any(i=>i<0||i>=count))throw new Exception("Section topology mismatch");var m=new Mesh{vertices=basis.Skip(start).Take(count).ToArray(),triangles=tri};m.RecalculateNormals();work.Add(m);geometricNeutral.Add(m.normals);positions.Add(new Vector3[count]);ranges.Add(new{name=(string)section["name"],start,count,triangles=tri.Length/3});}
 int degenerateSamples=0;using(var writer=new BinaryWriter(File.Create(Output+"corrected_shading.bin"))){
 writer.Write(mesh.blendShapeCount);writer.Write(sections.Sum(s=>(int)s["vertices"]));
 for(int s=0;s<mesh.blendShapeCount;s++){
 if(mesh.GetBlendShapeFrameCount(s)!=1)throw new Exception("Multi-frame shape requires explicit handling");mesh.GetBlendShapeFrameVertices(s,0,delta,oldNormal,oldTangent);
 for(int section=0;section<sections.Length;section++){int start=(int)sections[section]["offset"],count=(int)sections[section]["vertices"];for(int j=0;j<count;j++)positions[section][j]=basis[start+j]+delta[start+j];work[section].vertices=positions[section];work[section].RecalculateNormals();var posed=work[section].normals;
 for(int j=0;j<count;j++){int i=start+j;Vector3 dn,dt;if(geometricNeutral[section][j].sqrMagnitude<1e-12f||posed[j].sqrMagnitude<1e-12f){dn=oldNormal[i];dt=oldTangent[i];degenerateSamples++;}else{var rotation=Quaternion.FromToRotation(geometricNeutral[section][j],posed[j]);dn=rotation*normals[i]-normals[i];dt=rotation*(Vector3)tangents[i]-(Vector3)tangents[i];}
 if(!float.IsFinite(dn.x)||!float.IsFinite(dn.y)||!float.IsFinite(dn.z)||!float.IsFinite(dt.x)||!float.IsFinite(dt.y)||!float.IsFinite(dt.z))throw new Exception("Nonfinite shading");Write(writer,dn);Write(writer,dt);}
 }}
 }
 var result=new{target=Target,mesh.vertexCount,shapes=mesh.blendShapeCount,ranges,degenerateSamples,neutralShadingPreserved=true,positionsPreserved=true,method="Rotate authored neutral shading by the change in geometric normals on each section's own topology"};File.WriteAllText(Output+"preparation.json",JsonConvert.SerializeObject(result,Formatting.Indented));return result;
 }finally{foreach(var m in work)UnityEngine.Object.DestroyImmediate(m);}
 }
 public static object ExportBefore(){
 Guard();var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(Target);var scope=Scope();var p=new Vector3[mesh.vertexCount];var n=new Vector3[p.Length];var t=new Vector3[p.Length];using(var writer=new BinaryWriter(File.Create(Output+"original_scoped_shading.bin"))){writer.Write(mesh.blendShapeCount);writer.Write(scope.Sum(s=>(int)s["vertices"]));for(int s=0;s<mesh.blendShapeCount;s++){mesh.GetBlendShapeFrameVertices(s,0,p,n,t);foreach(var section in scope)for(int j=(int)section["offset"];j<(int)section["offset"]+(int)section["vertices"];j++){Write(writer,n[j]);Write(writer,t[j]);}}}return new{exportedOriginalShading=true};
 }
 static object Compare(Mesh before,Mesh after){
 if(before.vertexCount!=after.vertexCount||before.subMeshCount!=after.subMeshCount||!before.vertices.SequenceEqual(after.vertices)||!before.normals.SequenceEqual(after.normals)||!before.tangents.SequenceEqual(after.tangents)||!before.bindposes.SequenceEqual(after.bindposes)||!before.colors32.SequenceEqual(after.colors32)||before.blendShapeCount!=after.blendShapeCount)throw new Exception("Base attributes changed");
 for(int c=0;c<8;c++){var a=new List<Vector4>();var b=new List<Vector4>();before.GetUVs(c,a);after.GetUVs(c,b);if(!a.SequenceEqual(b))throw new Exception("UV channel changed");}
 for(int sub=0;sub<before.subMeshCount;sub++)if(!before.GetIndices(sub).SequenceEqual(after.GetIndices(sub)))throw new Exception("Topology changed");
 using(var a=before.GetBonesPerVertex())using(var b=after.GetBonesPerVertex())if(!a.ToArray().SequenceEqual(b.ToArray()))throw new Exception("Skin counts changed");using(var a=before.GetAllBoneWeights())using(var b=after.GetAllBoneWeights())if(!a.ToArray().SequenceEqual(b.ToArray()))throw new Exception("Skin weights changed");
 var scoped=new bool[before.vertexCount];foreach(var section in Scope())for(int j=(int)section["offset"];j<(int)section["offset"]+(int)section["vertices"];j++)scoped[j]=true;
 var p=new Vector3[before.vertexCount];var n=new Vector3[p.Length];var t=new Vector3[p.Length];var pp=new Vector3[p.Length];var nn=new Vector3[p.Length];var tt=new Vector3[p.Length];int changed=0;
 for(int s=0;s<before.blendShapeCount;s++){if(before.GetBlendShapeName(s)!=after.GetBlendShapeName(s)||before.GetBlendShapeFrameCount(s)!=after.GetBlendShapeFrameCount(s)||before.GetBlendShapeFrameWeight(s,0)!=after.GetBlendShapeFrameWeight(s,0))throw new Exception("Shape definitions changed");before.GetBlendShapeFrameVertices(s,0,p,n,t);after.GetBlendShapeFrameVertices(s,0,pp,nn,tt);if(!p.SequenceEqual(pp))throw new Exception("Expression positions changed "+s);for(int j=0;j<p.Length;j++)if(!n[j].Equals(nn[j])||!t[j].Equals(tt[j])){if(!scoped[j])throw new Exception("Shading outside head/lip scope changed");changed++;}}
 if(changed==0)throw new Exception("No shading repair applied");
 return new{passed=true,all73PositionArraysBitIdentical=true,neutralShadingBitIdentical=true,allNonHeadAndLipShadingBitIdentical=true,topologyUVSkinBindPosesExact=true,shapeDefinitionsExact=true,changedScopedShadingRecords=changed,after.vertexCount,triangles=after.triangles.Length/3,shapes=after.blendShapeCount,memoryMiB=UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(after)/1048576f};
 }
 public static object Install(){
 Guard();var original=AssetDatabase.LoadAssetAtPath<Mesh>(Target);var before=UnityEngine.Object.Instantiate(original);
 try{File.Copy(Output+"corrected_face.asset",Target,true);AssetDatabase.ImportAsset(Target,ImportAssetOptions.ForceUpdate);var result=Compare(before,AssetDatabase.LoadAssetAtPath<Mesh>(Target));File.WriteAllText(Output+"verification.json",JsonConvert.SerializeObject(result,Formatting.Indented));}
 catch{File.Copy(Output+"before_face.asset",Target,true);AssetDatabase.ImportAsset(Target,ImportAssetOptions.ForceUpdate);throw;}
 finally{UnityEngine.Object.DestroyImmediate(before);}
 var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(Target);var live=UnityEngine.Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Include).Where(r=>r.sharedMesh==mesh&&r.gameObject.scene.IsValid()).Select(r=>new{r.name,root=r.transform.root.name,scene=r.gameObject.scene.path,active=r.gameObject.activeInHierarchy}).ToArray();
 var report=new{installed=true,target=Target,live,assetGuidPreserved=File.ReadAllText(Target+".meta")==File.ReadAllText(Output+"before_face.asset.meta"),materialsAndPrefabNotEdited=true,sceneSaved=false};File.WriteAllText(Output+"installation.json",JsonConvert.SerializeObject(report,Formatting.Indented));return report;
 }
}
