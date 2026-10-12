if (UnityEditor.EditorApplication.isPlaying) throw new System.Exception("Stop Play Mode first.");
System.IO.Directory.CreateDirectory("Docs/HairRendering/Rollback");
foreach (var path in new[] { "Assets/_Sandbox/Shaders/EthanHair.shadergraph", "Assets/_Sandbox/Characters/Ethan/Materials/Ethan_StrandHair.mat", "Assets/_Sandbox/Characters/Ethan/Ethan.prefab", "Assets/_Sandbox/Scenes/CharacterTesting.unity", "Assets/Settings/URP/URP_Asset.asset" })
{
    var backup = "Docs/HairRendering/Rollback/" + System.IO.Path.GetFileName(path) + ".txt";
    if (!System.IO.File.Exists(backup)) System.IO.File.Copy(path, backup);
}
var flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.FlattenHierarchy;
var assembly = typeof(UnityEditor.Editor).Assembly;
var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
var sizes = sizesType.GetProperty("instance", flags).GetValue(null);
var groupType = assembly.GetType("UnityEditor.GameViewSizeGroupType");
var group = sizesType.GetMethod("GetGroup", flags).Invoke(sizes, new[] { System.Enum.Parse(groupType, "Standalone") });
var sizeType = assembly.GetType("UnityEditor.GameViewSize");
var fixedType = assembly.GetType("UnityEditor.GameViewSizeType");
var size = System.Activator.CreateInstance(sizeType, flags, null, new object[] { System.Enum.Parse(fixedType, "FixedResolution"), 1920, 1080, "Hair rendering comparison" }, null);
group.GetType().GetMethod("AddCustomSize", flags).Invoke(group, new[] { size });
var total = (int)group.GetType().GetMethod("GetTotalCount", flags).Invoke(group, null);
var viewType = assembly.GetType("UnityEditor.GameView");
var view = UnityEditor.EditorWindow.GetWindow(viewType);
UnityEditor.SessionState.SetInt("HairRendering.PreviousGameViewSize", (int)viewType.GetProperty("selectedSizeIndex", flags).GetValue(view));
viewType.GetProperty("selectedSizeIndex", flags).SetValue(view, total - 1);
view.Show(); view.Focus(); view.Repaint();
UnityEngine.Application.runInBackground = true;
return new { selectedSize = total - 1, sceneDirty = UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty };
