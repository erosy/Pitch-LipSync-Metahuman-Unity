using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Republish Pipeline's normal discovery descriptor without changing authentication.
public static class UnityCliDiscoveryRepair
{
    const string AttemptKey = "UnityCliDiscoveryRepair.RegisteredDescriptor.Attempted";

    [InitializeOnLoadMethod]
    static void ScheduleRepair()
    {
        if (SessionState.GetBool(AttemptKey, false))
            return;
        EditorApplication.delayCall += () =>
        {
            SessionState.SetBool(AttemptKey, true);
            Repair();
        };
    }

    [MenuItem("Tools/Unity CLI/Repair Discovery")]
    public static void Repair()
    {
        try
        {
            var startup = FindType("Unity.Pipeline.Editor.PipelineServerStartup");
            if (startup == null)
                throw new InvalidOperationException("Pipeline assemblies are not loaded. Resolve Console compilation errors first.");

            var server = startup.GetProperty("Server", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null);
            if (server == null || !(bool)server.GetType().GetProperty("IsRunning").GetValue(server))
                throw new InvalidOperationException("Start the server from Window/Pipeline/Start Server, then run this command again.");
            var port = Convert.ToInt32(server.GetType().GetProperty("Port").GetValue(server));
            // Register with the owner so its heartbeat updates the same descriptor.
            // A standalone descriptor write leaves the owner's field uninitialized.
            var createDescriptor = server.GetType().GetMethod("CreateInstanceDescriptor",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (createDescriptor == null)
                throw new InvalidOperationException("This Pipeline version does not expose its descriptor creation hook.");
            createDescriptor.Invoke(server, null);
            Debug.Log($"Unity CLI discovery repaired for this project on port {port}. Authentication remains enabled.");
        }
        catch (Exception error)
        {
            var cause = error is TargetInvocationException invocation ? invocation.InnerException : error;
            Debug.LogError("Unity CLI discovery repair failed: " + cause?.Message);
        }
    }

    static Type FindType(string name)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var type = assembly.GetType(name, false);
            if (type != null)
                return type;
        }
        return null;
    }
}
