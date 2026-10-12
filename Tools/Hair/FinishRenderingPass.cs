if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) throw new System.Exception("Stop Play Mode first.");
var source = "Assets/Docs/HairRendering";
var destination = "Docs/HairRendering";
foreach (var name in new[] { "EditBaseline.png", "BaselineWarm.png", "MotionVectorsAnimated.png", "MotionVectorsStationary.png", "MotionVectorsHeadOnly.png", "SavedFinal.png" })
{
    var path = source + "/" + name;
    if (System.IO.File.Exists(path))
    {
        System.IO.File.Copy(path, destination + "/" + name, true);
        UnityEditor.AssetDatabase.DeleteAsset(path);
    }
}
var geometry = Newtonsoft.Json.Linq.JObject.Parse(System.IO.File.ReadAllText("Temp/FinalRenderingGeometryCheck.json"))["data"]["result"]["result"];
System.IO.File.WriteAllText(destination + "/FinalGeometryCheck.json", geometry.ToString());
// Delete only this session's temporary connection bootstrap and empty capture folders.
UnityEditor.AssetDatabase.DeleteAsset("Assets/_Sandbox/Scripts/Editor/HairRenderingConnection.cs");
foreach (var folder in new[] { source, "Assets/Docs" })
{
    var resolved = System.IO.Path.GetFullPath(folder);
    var workspace = System.IO.Path.GetFullPath(".") + System.IO.Path.DirectorySeparatorChar;
    if (!resolved.StartsWith(workspace, System.StringComparison.OrdinalIgnoreCase)) throw new System.Exception("Capture folder is outside the workspace.");
    if (System.IO.Directory.Exists(resolved) && System.IO.Directory.GetFileSystemEntries(resolved).Length == 0)
        UnityEditor.AssetDatabase.DeleteAsset(folder);
}
UnityEditor.AssetDatabase.SaveAssets();
return "Rendering artifacts retained in Docs/HairRendering; temporary imported captures and bootstrap removed.";
