var h = UnityEngine.Object.FindObjectsByType<Unity.DemoTeam.Hair.HairInstance>(UnityEngine.FindObjectsInactive.Exclude).First();
var head = h.transform.parent;
var r = h.transform.root.GetComponentsInChildren<UnityEngine.SkinnedMeshRenderer>(true).First(x => x.name == "Face");
var mesh = new UnityEngine.Mesh();
r.BakeMesh(mesh);
var vertices = System.Array.ConvertAll(mesh.vertices, v => { var p = head.InverseTransformPoint(r.transform.TransformPoint(v)); return new[] {p.x,p.y,p.z}; });
System.IO.File.WriteAllText("C:/PROJECTS/Metahuman Unity Lip Sync Workaround/Temp/ethan_head_local.json", Newtonsoft.Json.JsonConvert.SerializeObject(new { vertices, triangles = mesh.triangles, headMatrix = head.localToWorldMatrix.ToString() }));
UnityEngine.Object.DestroyImmediate(mesh);
return new { vertices = vertices.Length, headMatrix = head.localToWorldMatrix.ToString() };
