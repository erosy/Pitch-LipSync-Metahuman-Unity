using System;using System.IO;using System.Linq;using System.Collections.Generic;using UnityEngine;using UnityEditor;using Unity.Collections;using Newtonsoft.Json;
public static class MergeLioraHierarchy {
 const string Folder="Assets/_Sandbox/Characters/Liora/Candidate/";const string Path=Folder+"Liora_Candidate.prefab";const string Data="Data/Liora/HierarchyRepair/";
 static void Write(BinaryWriter w,Vector3 p){w.Write(p.x);w.Write(p.y);w.Write(p.z);}
 static float[] Values(Matrix4x4 x)=>Enumerable.Range(0,16).Select(j=>x[j/4,j%4]).ToArray();
 static Vector3[] Skin(SkinnedMeshRenderer r,Transform root){var m=r.sharedMesh;var p=m.vertices;var result=new Vector3[p.Length];var mat=r.bones.Select((b,j)=>root.worldToLocalMatrix*b.localToWorldMatrix*m.bindposes[j]).ToArray();using(var counts=m.GetBonesPerVertex())using(var weights=m.GetAllBoneWeights()){int at=0;for(int j=0;j<p.Length;j++)for(int k=0;k<counts[j];k++){var w=weights[at++];result[j]+=mat[w.boneIndex].MultiplyPoint3x4(p[j])*w.weight;}}return result;}
 public static object Draft(){
 if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Edit Mode required");if(File.Exists(Folder+"Liora_Face.asset"))throw new Exception("Merged Face already exists");Directory.CreateDirectory(Data+"Before");foreach(var ext in new[]{"",".meta"})File.Copy(Path+ext,Data+"Before/Liora_Candidate.prefab"+ext,false);
 var root=PrefabUtility.LoadPrefabContents(Path);
 try{
 var all=root.GetComponentsInChildren<SkinnedMeshRenderer>(true);var head=all.Single(r=>r.name=="BAKED_head_lod0_mesh");var outfit=all.Single(r=>r.name=="BAKED_Outfits");var sources=new[]{head}.Concat(all.Where(r=>r!=head&&r!=outfit&&r.enabled).OrderBy(r=>r.name)).ToArray();
 var target=new GameObject("Face draft");target.transform.SetParent(root.transform,false);target.transform.SetPositionAndRotation(head.transform.position,head.transform.rotation);target.transform.localScale=head.transform.lossyScale;
 var mesh=new Mesh{name="Liora_Face",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};var positions=new List<Vector3>();var normals=new List<Vector3>();var tangents=new List<Vector4>();var uv=Enumerable.Range(0,8).Select(_=>new List<Vector4>()).ToArray();var colors=new List<Color32>();bool anyColors=sources.Any(r=>r.sharedMesh.colors32.Length>0);
 var triangles=new List<int[]>();var materials=new List<Material>();var bones=new List<Transform>();var bindposes=new List<Matrix4x4>();var counts=new List<byte>();var weights=new List<BoneWeight1>();var offsets=new List<int>();var matrices=new List<Matrix4x4>();var sections=new List<object>();int submesh=0;
 for(int section=0;section<sources.Length;section++){
 var r=sources[section];var m=r.sharedMesh;int offset=positions.Count;offsets.Add(offset);var x=section==0?Matrix4x4.identity:target.transform.worldToLocalMatrix*r.transform.localToWorldMatrix;var nx=x.inverse.transpose;matrices.Add(x);
 positions.AddRange(section==0?m.vertices:m.vertices.Select(v=>x.MultiplyPoint3x4(v)));normals.AddRange(section==0?m.normals:m.normals.Select(v=>nx.MultiplyVector(v).normalized));
 if(m.tangents.Length==0)tangents.AddRange(Enumerable.Repeat(Vector4.zero,m.vertexCount));else tangents.AddRange(section==0?m.tangents:m.tangents.Select(v=>{var t=x.MultiplyVector(v).normalized;return new Vector4(t.x,t.y,t.z,v.w);}));
 for(int channel=0;channel<8;channel++){var values=new List<Vector4>();m.GetUVs(channel,values);uv[channel].AddRange(values.Count>0?values:Enumerable.Repeat(Vector4.zero,m.vertexCount));}
 if(anyColors)colors.AddRange(m.colors32.Length>0?m.colors32:Enumerable.Repeat(new Color32(255,255,255,255),m.vertexCount));
 for(int s=0;s<m.subMeshCount;s++){triangles.Add(m.GetTriangles(s).Select(j=>j+offset).ToArray());materials.Add(r.sharedMaterials[s]);}
 using(var ws=m.GetAllBoneWeights())using(var cs=m.GetBonesPerVertex()){
 var used=ws.ToArray().Where(w=>w.weight>0).Select(w=>w.boneIndex).Distinct().OrderBy(j=>j).ToArray();var indices=new Dictionary<int,int>();foreach(int j in used){indices[j]=bones.Count;bones.Add(r.bones[j]);bindposes.Add(m.bindposes[j]*x.inverse);}
 counts.AddRange(cs.ToArray());foreach(var w in ws){var copy=w;copy.boneIndex=w.weight>0?indices[w.boneIndex]:0;weights.Add(copy);}
 }
 sections.Add(new{name=r.name,source=AssetDatabase.GetAssetPath(m),offset,vertices=m.vertexCount,triangles=m.triangles.Length/3,firstSubmesh=submesh,submeshes=m.subMeshCount,matrix=Values(x),normalMatrix=Values(nx),materials=r.sharedMaterials.Select(AssetDatabase.GetAssetPath).ToArray()});submesh+=m.subMeshCount;
 }
 mesh.vertices=positions.ToArray();mesh.normals=normals.ToArray();mesh.tangents=tangents.ToArray();for(int c=0;c<8;c++)if(uv[c].Any(v=>v.sqrMagnitude>0))mesh.SetUVs(c,uv[c]);if(anyColors)mesh.colors32=colors.ToArray();mesh.bindposes=bindposes.ToArray();using(var cs=new NativeArray<byte>(counts.ToArray(),Allocator.Temp))using(var ws=new NativeArray<BoneWeight1>(weights.ToArray(),Allocator.Temp))mesh.SetBoneWeights(cs,ws);mesh.subMeshCount=triangles.Count;for(int j=0;j<triangles.Count;j++)mesh.SetTriangles(triangles[j],j);
 using(var writer=new BinaryWriter(File.Create(Data+"desired_shapes.bin"))){writer.Write(positions.Count);writer.Write(head.sharedMesh.blendShapeCount);
 for(int s=0;s<head.sharedMesh.blendShapeCount;s++){
 string name=head.sharedMesh.GetBlendShapeName(s);var p=new Vector3[positions.Count];var n=new Vector3[p.Length];var t=new Vector3[p.Length];
 for(int section=0;section<sources.Length;section++){var source=sources[section].sharedMesh;int shape=source.GetBlendShapeIndex(name);if(shape<0)throw new Exception("Missing source expression "+name);var dp=new Vector3[source.vertexCount];var dn=new Vector3[dp.Length];var dt=new Vector3[dp.Length];source.GetBlendShapeFrameVertices(shape,0,dp,dn,dt);var x=matrices[section];var nx=x.inverse.transpose;
 for(int j=0;j<dp.Length;j++){int index=offsets[section]+j;p[index]=section==0?dp[j]:x.MultiplyVector(dp[j]);n[index]=section==0?dn[j]:nx.MultiplyVector(dn[j]);t[index]=section==0?dt[j]:x.MultiplyVector(dt[j]);}}
 for(int j=0;j<p.Length;j++){Write(writer,p[j]);Write(writer,n[j]);Write(writer,t[j]);}
 // Temporary tangent sentinels prevent loss of tiny sparse position records.
 var sentinel=Enumerable.Range(0,p.Length).Select(j=>p[j].sqrMagnitude>0||n[j].sqrMagnitude>0||t[j].sqrMagnitude>0?Vector3.right:Vector3.zero).ToArray();mesh.AddBlendShapeFrame(name,head.sharedMesh.GetBlendShapeFrameWeight(s,0),p,n,sentinel);
 }}
 mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,Folder+"Liora_Face.asset");var renderer=target.AddComponent<SkinnedMeshRenderer>();renderer.sharedMesh=mesh;renderer.bones=bones.ToArray();renderer.rootBone=head.rootBone;renderer.sharedMaterials=materials.ToArray();
 var actual=Skin(renderer,root.transform);float error=0;for(int section=0;section<sources.Length;section++){var expected=Skin(sources[section],root.transform);for(int j=0;j<expected.Length;j++)error=Mathf.Max(error,Vector3.Distance(expected[j],actual[offsets[section]+j]));}if(error>2e-6f)throw new Exception("Merged neutral mismatch "+error);
 var config=new{mesh=Folder+"Liora_Face.asset",vertices=mesh.vertexCount,shapes=mesh.blendShapeCount,sections,bonePaths=bones.Select(b=>AnimationUtility.CalculateTransformPath(b,root.transform)),materials=materials.Select(AssetDatabase.GetAssetPath),maxNeutralErrorM=error,excludedDisabledCards=true};File.WriteAllText(Data+"sections.json",JsonConvert.SerializeObject(config,Formatting.Indented));AssetDatabase.SaveAssets();return new{mesh.vertexCount,shapes=mesh.blendShapeCount,sections=sources.Length,maxNeutralErrorM=error,prefabChanged=false};
 }finally{PrefabUtility.UnloadPrefabContents(root);}
 }
 public static object Install(){
 if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Edit Mode required");File.Copy(Data+"Face_preserved.asset",Folder+"Liora_Face.asset",true);AssetDatabase.ImportAsset(Folder+"Liora_Face.asset",ImportAssetOptions.ForceUpdate);
 var root=PrefabUtility.LoadPrefabContents(Path);
 try{
 var cfg=Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(Data+"sections.json"));var old=root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r=>r.name=="BAKED_head_lod0_mesh");var outfit=root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r=>r.name=="BAKED_Outfits");var merged=new GameObject("Face");merged.transform.SetParent(root.transform,false);merged.transform.SetPositionAndRotation(old.transform.position,old.transform.rotation);merged.transform.localScale=old.transform.lossyScale;
 var renderer=merged.AddComponent<SkinnedMeshRenderer>();EditorUtility.CopySerialized(old,renderer);renderer.sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"Liora_Face.asset");renderer.bones=cfg["bonePaths"].Select(p=>root.transform.Find((string)p)).ToArray();renderer.sharedMaterials=cfg["materials"].Select(p=>AssetDatabase.LoadAssetAtPath<Material>((string)p)).ToArray();renderer.localBounds=renderer.sharedMesh.bounds;renderer.updateWhenOffscreen=true;
 var map=new Dictionary<UnityEngine.Object,UnityEngine.Object>{{old,renderer},{old.transform,merged.transform},{old.gameObject,merged}};
 var driver=old.GetComponent<uLipSync.uLipSyncBlendShape>();if(driver){var copy=merged.AddComponent<uLipSync.uLipSyncBlendShape>();EditorUtility.CopySerialized(driver,copy);map[driver]=copy;}
 foreach(var c in root.GetComponentsInChildren<Component>(true).Where(c=>c&&!(c is Transform)&&!(c is Renderer)&&!(c is CharacterFacialMeshSync))){var so=new SerializedObject(c);var p=so.GetIterator();while(p.Next(true))if(!p.propertyPath.StartsWith("m_")&&p.propertyType==SerializedPropertyType.ObjectReference&&p.objectReferenceValue&&map.TryGetValue(p.objectReferenceValue,out var value))p.objectReferenceValue=value;so.ApplyModifiedPropertiesWithoutUndo();}
 foreach(var sync in root.GetComponents<CharacterFacialMeshSync>())UnityEngine.Object.DestroyImmediate(sync);
 // Outfit retains its small nonzero expression arrays, so keep a single explicit
 // synchronization binding rather than silently discarding those records.
 if(outfit.sharedMesh.blendShapeCount>0){var sync=root.AddComponent<CharacterFacialMeshSync>();sync.Configure(renderer,new[]{outfit});sync.Apply();}
 foreach(var source in root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r=>r!=renderer&&r!=outfit).ToArray())UnityEngine.Object.DestroyImmediate(source.gameObject);
 outfit.name="Outfit";
 var errors=root.GetComponent<Sandbox.CharacterSystem.Character>().ValidateConfiguration();errors.AddRange(root.GetComponent<Sandbox.CharacterSystem.CharacterFlowController>().ValidateConfiguration());if(errors.Count>0)throw new Exception(string.Join(";",errors));
 PrefabUtility.SaveAsPrefabAsset(root,Path,out var success);if(!success)throw new Exception("Prefab save failed");AssetDatabase.SaveAssets();return new{prefab=Path,renderers=root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(r=>r.name).ToArray(),topLevel=Enumerable.Range(0,root.transform.childCount).Select(j=>root.transform.GetChild(j).name),outfitExpressionDataPreserved=true};
 }finally{PrefabUtility.UnloadPrefabContents(root);}
 }
}
