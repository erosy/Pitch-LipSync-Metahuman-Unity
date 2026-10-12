if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) throw new System.Exception("Stop Play Mode before editing the graph.");
var flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.FlattenHierarchy;
var path = "Assets/_Sandbox/Shaders/EthanHair.shadergraph";
var sg = System.AppDomain.CurrentDomain.GetAssemblies().First(a => a.GetType("UnityEditor.ShaderGraph.GraphData") != null);
var graphType = sg.GetType("UnityEditor.ShaderGraph.GraphData");
var importer = sg.GetType("UnityEditor.ShaderGraph.ShaderGraphImporter");
var args = new object[] { path, null, null, null };
importer.GetMethods(flags).First(m => m.Name == "GetShaderText" && m.GetParameters().Length == 4).Invoke(null, args);
var graph = args[3];
var nodeType = sg.GetType("UnityEditor.ShaderGraph.AbstractMaterialNode");
var nodes = ((System.Collections.IEnumerable)graphType.GetMethod("GetNodes", flags).MakeGenericMethod(nodeType).Invoke(graph, null)).Cast<object>().ToArray();
System.Func<object, string, object> get = (o, key) => o.GetType().GetProperty(key, flags).GetValue(o);
System.Action<object, string, object> set = (o, key, val) => o.GetType().GetProperty(key, flags).SetValue(o, val);
var hairNode = nodes.First(n => (string)get(n, "objectId") == "7ab2f0c9b7fc411180b63181354e4736");
var smoothnessNode = nodes.First(n => (string)get(n, "name") == "SurfaceDescription.Smoothness");
var properties = ((System.Collections.IEnumerable)graphType.GetProperty("properties", flags).GetValue(graph)).Cast<object>().ToArray();
var alreadyExposed = properties.Any(p => (string)get(p, "referenceName") == "_HairWidthScale");
if (!alreadyExposed) foreach (var item in new[] { new { display = "Hair Width Scale", reference = "_HairWidthScale", value = 1f, range = new UnityEngine.Vector2(0.75f, 1.5f), target = hairNode, slot = 147058159 }, new { display = "Hair Smoothness", reference = "_HairSmoothness", value = 0.5f, range = new UnityEngine.Vector2(0f, 1f), target = smoothnessNode, slot = 0 } })
{
    var property = System.Activator.CreateInstance(sg.GetType("UnityEditor.ShaderGraph.Internal.Vector1ShaderProperty"), true);
    set(property, "displayName", item.display); set(property, "overrideReferenceName", item.reference);
    set(property, "value", item.value); set(property, "generatePropertyBlock", true);
    set(property, "floatType", System.Enum.Parse(sg.GetType("UnityEditor.ShaderGraph.Internal.FloatType"), "Slider"));
    set(property, "rangeValues", item.range);
    graphType.GetMethod("AddGraphInput", flags).Invoke(graph, new object[] { property, -1 });
    var node = System.Activator.CreateInstance(sg.GetType("UnityEditor.ShaderGraph.PropertyNode"), true);
    graphType.GetMethod("AddNode", flags).Invoke(graph, new object[] { node, false });
    set(node, "property", property);
    var from = nodeType.GetMethod("GetSlotReference", flags).Invoke(node, new object[] { 0 });
    var to = nodeType.GetMethod("GetSlotReference", flags).Invoke(item.target, new object[] { item.slot });
    graphType.GetMethod("Connect", flags).Invoke(graph, new[] { from, to });
}
// Keep both controls visible in the Shader Graph blackboard and separate their nodes.
var category = ((System.Collections.IEnumerable)graphType.GetProperty("categories", flags).GetValue(graph)).Cast<object>().First();
var categoryGuid = get(category, "categoryGuid");
var currentProperties = ((System.Collections.IEnumerable)graphType.GetProperty("properties", flags).GetValue(graph)).Cast<object>().ToArray();
var currentNodes = ((System.Collections.IEnumerable)graphType.GetMethod("GetNodes", flags).MakeGenericMethod(nodeType).Invoke(graph, null)).Cast<object>().ToArray();
foreach (var property in currentProperties.Where(p => new[] { "_HairWidthScale", "_HairSmoothness" }.Contains((string)get(p, "referenceName"))))
{
    if (!(bool)category.GetType().GetMethod("IsItemInCategory", flags).Invoke(category, new[] { property }))
        graphType.GetMethod("InsertItemIntoCategory", flags).Invoke(graph, new object[] { categoryGuid, property, -1 });
    var node = currentNodes.First(n => n.GetType().Name == "PropertyNode" && get(n, "property") == property);
    var state = get(node, "drawState");
    var widthControl = (string)get(property, "referenceName") == "_HairWidthScale";
    set(state, "position", new UnityEngine.Rect(widthControl ? -1250 : -400, widthControl ? -300 : 600, 200, 40));
    set(node, "drawState", state);
}
graphType.GetMethod("ValidateGraph", flags).Invoke(graph, null);
var multi = sg.GetType("UnityEditor.ShaderGraph.Serialization.MultiJson");
var json = (string)multi.GetMethod("Serialize", flags).Invoke(null, new[] { graph });
System.IO.File.WriteAllText(path, json);
UnityEditor.AssetDatabase.ImportAsset(path, UnityEditor.ImportAssetOptions.ForceSynchronousImport);
var material = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>("Assets/_Sandbox/Characters/Ethan/Materials/Ethan_StrandHair.mat");
if (!material.HasProperty("_HairWidthScale") || !material.HasProperty("_HairSmoothness")) throw new System.Exception("Rendering properties were not imported.");
if (!alreadyExposed) { material.SetFloat("_HairWidthScale", 1f); material.SetFloat("_HairSmoothness", 0.5f); }
UnityEditor.EditorUtility.SetDirty(material); UnityEditor.AssetDatabase.SaveAssets();
return new { width = material.GetFloat("_HairWidthScale"), smoothness = material.GetFloat("_HairSmoothness"), errors = UnityEditor.ShaderUtil.GetShaderMessages(material.shader) };
