using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Playables;
using UnityEngine.Animations;
using Sandbox.CharacterSystem;
using Newtonsoft.Json.Linq;

public static class RenderV2FingerReview
{
    static Vector3[] Skin(SkinnedMeshRenderer r, Transform root)
    {
        var mesh=r.sharedMesh; var vertices=mesh.vertices; var bind=mesh.bindposes;
        var matrices=r.bones.Select((b,i)=>root.worldToLocalMatrix*b.localToWorldMatrix*bind[i]).ToArray();
        var result=new Vector3[vertices.Length];
        using(var counts=mesh.GetBonesPerVertex()) using(var weights=mesh.GetAllBoneWeights())
        { int at=0; for(int i=0;i<result.Length;i++) for(int j=0;j<counts[i];j++) { var w=weights[at++]; result[i]+=matrices[w.boneIndex].MultiplyPoint3x4(vertices[i])*w.weight; } }
        return result;
    }
    public static object Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Edit Mode required");
        var root=PrefabUtility.LoadPrefabContents("Assets/_Sandbox/Characters/Ethan_V2/Ethan_V2.prefab");
        var resources=new List<UnityEngine.Object>();
        try
        {
            var bridge=root.GetComponent<CharacterSkeletonPoseBridge>();
            var animator=root.GetComponentInChildren<AnimationModule>(true).Animator;
            var sources=animator.GetComponentsInChildren<Transform>(true).ToDictionary(t=>t.name);
            var body=root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r=>r.name=="BAKED_body_lod0_mesh");
            var rest=root.GetComponentsInChildren<Transform>(true).ToDictionary(t=>t,t=>(t.localPosition,t.localRotation,t.localScale));
            Action reset=()=>{foreach(var kv in rest){kv.Key.localPosition=kv.Value.Item1;kv.Key.localRotation=kv.Value.Item2;kv.Key.localScale=kv.Value.Item3;}};
            var repaired=bridge.Bindings.ToDictionary(p=>p,p=>(p.source,p.sourceBindInverse,p.mode));
            var legacy=JObject.Parse(File.ReadAllText("Data/EthanV2/FingerRepair/before_audit.json"))["rows"].ToDictionary(p=>(string)p["target"],p=>(string)p["source"]);
            foreach(var renderer in root.GetComponentsInChildren<Renderer>(true)) renderer.enabled=false;
            var cameraGo=new GameObject("Finger review camera"); SceneManager.MoveGameObjectToScene(cameraGo,root.scene); resources.Add(cameraGo);
            var camera=cameraGo.AddComponent<Camera>(); camera.scene=root.scene; camera.cullingMask=1<<30;
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.075f,.09f,.12f); camera.orthographic=true; camera.orthographicSize=.145f; camera.nearClipPlane=.01f; camera.farClipPlane=5;
            var lightGo=new GameObject("Finger review light"); SceneManager.MoveGameObjectToScene(lightGo,root.scene); resources.Add(lightGo);
            var light=lightGo.AddComponent<Light>(); light.type=LightType.Directional; light.intensity=2; light.cullingMask=1<<30;
            var material=new Material(Shader.Find("Universal Render Pipeline/Lit")); resources.Add(material); material.SetColor("_BaseColor",new Color(.65f,.47f,.34f)); material.SetFloat("_Smoothness",.25f); material.SetFloat("_Cull",0);
            var definitions=AssetDatabase.FindAssets("t:AnimationDefinition",new[]{"Assets/_Sandbox"}).Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<AnimationDefinition>).Where(d=>d&&d.Clip&&(d.name=="Waving"||d.name=="Thinking"||d.name=="Thumbs Up")).ToArray();
            Directory.CreateDirectory("Data/EthanV2/FingerRepair/Previews");
            int images=0;
            foreach(var definition in definitions)
            {
                reset();
                var graph=PlayableGraph.Create("Disposable hand render");
                try
                {
                    graph.SetTimeUpdateMode(DirectorUpdateMode.Manual); var playable=AnimationClipPlayable.Create(graph,definition.Clip); playable.SetApplyFootIK(false);
                    AnimationPlayableOutput.Create(graph,"hand",animator).SetSourcePlayable(playable); graph.Play(); playable.SetTime(definition.Clip.length*.5); graph.Evaluate(0); bridge.ApplyPose();
                    foreach(string side in new[]{"l","r"})
                    {
                        foreach(var pair in repaired){pair.Key.source=pair.Value.Item1;pair.Key.sourceBindInverse=pair.Value.Item2;pair.Key.mode=pair.Value.Item3;}
                        bridge.ApplyPose();
                        var hand=body.bones.Single(t=>t.name=="hand_"+side);
                        var index=body.bones.Single(t=>t.name=="index_01_"+side);
                        var pinky=body.bones.Single(t=>t.name=="pinky_01_"+side);
                        var center=root.transform.InverseTransformPoint((hand.position+index.position+pinky.position)/3);
                        var up=root.transform.InverseTransformDirection(((index.position+pinky.position)*.5f-hand.position).normalized);
                        center+=up*.035f;
                        var normal=root.transform.InverseTransformDirection(Vector3.Cross(index.position-hand.position,pinky.position-hand.position).normalized*(side=="l"?1:-1));
                        camera.transform.position=center+normal*.5f-up*.1f; camera.transform.rotation=Quaternion.LookRotation(center-camera.transform.position,up);
                        lightGo.transform.rotation=camera.transform.rotation*Quaternion.Euler(25,-30,0);
                        var included=body.bones.Select(b=>b==hand||b.IsChildOf(hand)).ToArray();
                        var selected=new bool[body.sharedMesh.vertexCount];
                        using(var counts=body.sharedMesh.GetBonesPerVertex())using(var weights=body.sharedMesh.GetAllBoneWeights())
                        { int at=0;for(int i=0;i<selected.Length;i++)for(int j=0;j<counts[i];j++){var w=weights[at++];if(included[w.boneIndex]&&w.weight>.01f)selected[i]=true;} }
                        var triangles=body.sharedMesh.triangles; var subset=new List<int>();
                        for(int i=0;i<triangles.Length;i+=3)if(selected[triangles[i]]||selected[triangles[i+1]]||selected[triangles[i+2]]){subset.Add(triangles[i]);subset.Add(triangles[i+1]);subset.Add(triangles[i+2]);}
                        foreach(string version in new[]{"before","after"})
                        {
                            foreach(var pair in repaired)
                            {
                                pair.Key.source=pair.Value.Item1;pair.Key.sourceBindInverse=pair.Value.Item2;pair.Key.mode=pair.Value.Item3;
                                if(version=="before"&&pair.Value.Item3!=CharacterSkeletonPoseBridge.TransferMode.WorldPose)
                                {pair.Key.source=sources[legacy[pair.Key.target.name]];pair.Key.sourceBindInverse=(root.transform.worldToLocalMatrix*restMatrix(pair.Key.source,root.transform,rest)).inverse;pair.Key.mode=CharacterSkeletonPoseBridge.TransferMode.WorldPose;}
                            }
                            bridge.ApplyPose();
                            var mesh=new Mesh(); mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32; mesh.vertices=Skin(body,root.transform);mesh.triangles=subset.ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();
                            var go=new GameObject("Hand snapshot");SceneManager.MoveGameObjectToScene(go,root.scene);go.layer=30;go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;
                            var rt=RenderTexture.GetTemporary(600,600,24);var previous=RenderTexture.active;
                            try
                            {
                                camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
                                var texture=new Texture2D(600,600,TextureFormat.RGB24,false);
                                try { texture.ReadPixels(new Rect(0,0,600,600),0,0);texture.Apply();File.WriteAllBytes("Data/EthanV2/FingerRepair/Previews/"+definition.name+"_"+side+"_"+version+".png",texture.EncodeToPNG());images++; }
                                finally {UnityEngine.Object.DestroyImmediate(texture);}
                            }
                            finally {camera.targetTexture=null;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.DestroyImmediate(go);UnityEngine.Object.DestroyImmediate(mesh);}
                        }
                    }
                }
                finally {graph.Destroy();foreach(var pair in repaired){pair.Key.source=pair.Value.Item1;pair.Key.sourceBindInverse=pair.Value.Item2;pair.Key.mode=pair.Value.Item3;}reset();}
            }
            return new {images,playModeEntered=false, rootPosition=root.transform.position.ToString(), rootScale=root.transform.lossyScale.ToString(), cameraPosition=camera.transform.position.ToString()};
        }
        finally {foreach(var item in resources)if(item)UnityEngine.Object.DestroyImmediate(item);PrefabUtility.UnloadPrefabContents(root);}
    }
    static Matrix4x4 restMatrix(Transform t,Transform root,Dictionary<Transform,(Vector3,Quaternion,Vector3)> rest)
    {
        if(t==root) return root.localToWorldMatrix;
        var value=rest[t];return restMatrix(t.parent,root,rest)*Matrix4x4.TRS(value.Item1,value.Item2,value.Item3);
    }
}
