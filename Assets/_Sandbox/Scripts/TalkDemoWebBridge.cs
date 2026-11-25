using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;
using Sandbox.CharacterSystem;

[DefaultExecutionOrder(-200), RequireComponent(typeof(TalkDemoController), typeof(TalkDemoSpeechInput))]
public sealed class TalkDemoWebBridge : MonoBehaviour
{
    [SerializeField] RenderPipelineAsset transparentPipeline;
    TalkDemoController controller;
    TalkDemoSpeechInput speech;
    RenderPipelineAsset previousPipeline;
    string readyPersona, reportedError;
    bool announced;

    [Serializable] sealed class Command { public string cmd, persona_key, speech_json, request_id; }
    [Serializable] sealed class Event { public string ns = "metahuman-talk"; public string event_name, persona_key, request_id, message; }
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern void TalkDemoPublish(string json);
    [DllImport("__Internal")] static extern void TalkDemoAudioInit();
    [DllImport("__Internal")] static extern int TalkDemoAudioReady();
#endif
    void Awake()
    {
        controller = GetComponent<TalkDemoController>(); speech = GetComponent<TalkDemoSpeechInput>();
        // Transparent black avoids RGB leakage at hair edges when compositing the iframe.
        var camera = Camera.main;
        if (camera && camera.gameObject.scene == gameObject.scene)
        { camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear; }
#if UNITY_WEBGL && !UNITY_EDITOR
        speech.PlaybackAllowed = false;
#endif
    }
    void OnEnable()
    {
        if (!Application.IsPlaying(gameObject)) return;
        previousPipeline = QualitySettings.renderPipeline;
        if (transparentPipeline) QualitySettings.renderPipeline = transparentPipeline;
        speech.SpeechEvent += OnSpeech; controller.PersonaChanging += OnPersonaChanging;
        announced = false;
    }
    void Start()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        TalkDemoAudioInit();
#endif
    }
    void OnDisable()
    {
        if (speech) speech.SpeechEvent -= OnSpeech;
        if (controller) controller.PersonaChanging -= OnPersonaChanging;
        if (transparentPipeline && QualitySettings.renderPipeline == transparentPipeline) QualitySettings.renderPipeline = previousPipeline;
    }
    void LateUpdate()
    {
        if (!Application.IsPlaying(gameObject)) return;
#if UNITY_WEBGL && !UNITY_EDITOR
        speech.PlaybackAllowed = TalkDemoAudioReady() != 0;
#endif
        if (!announced) { announced = true; Publish("bridge-ready"); }
        if (controller.IsReady && readyPersona != controller.SelectedPersonaKey)
        { readyPersona = controller.SelectedPersonaKey; Publish("character-ready"); }
        if (controller.State == TalkDemoController.DemoState.Error && reportedError != controller.LastError)
        { reportedError = controller.LastError; Publish("error", message: reportedError); }
    }
    void OnPersonaChanging() { readyPersona = null; reportedError = null; }
    void OnSpeech(TalkDemoSpeechInput.Notification n) => Publish(n.Event, n.RequestId, n.Message, n.PersonaKey);
    public void ReceiveCommand(string json)
    {
        if (!Application.IsPlaying(gameObject) || !isActiveAndEnabled) return;
        Command command;
        try { command = JsonUtility.FromJson<Command>(json); }
        catch (ArgumentException) { Publish("error", message: "Invalid bridge command JSON."); return; }
        if (command == null) { Publish("error", message: "Missing bridge command."); return; }
        switch (command.cmd)
        {
            case "select-persona":
                string key = command.persona_key == "ethan" ? "Ethan Reeve" : command.persona_key == "liora" ? "Liora Minji" : command.persona_key;
                if (!controller.SelectPersona(key)) Publish("error", command.request_id, controller.LastError);
                break;
            case "speak":
                if (!speech.TryEnqueueSpeechJson(command.speech_json, out var error)) Publish("error", command.request_id, error);
                break;
            case "stop": speech.StopAndClearSpeech(); break;
            default: Publish("error", command.request_id, "Unknown bridge command."); break;
        }
    }
    void Publish(string kind, string request = null, string message = null, string persona = null)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        TalkDemoPublish(JsonUtility.ToJson(new Event { event_name = kind, request_id = request, message = message, persona_key = persona ?? controller.SelectedPersonaKey }));
#endif
    }
}
