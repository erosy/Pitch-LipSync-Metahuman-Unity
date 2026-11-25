using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Sandbox.CharacterSystem;
using UnityEngine;

[DefaultExecutionOrder(-300)]
public sealed class EthanHairComparison : MonoBehaviour
{
    public enum HairMode { Ribbons, Cards, Hidden }

    [SerializeField] CharacterHairFallback fallback;
    [SerializeField] MeshRenderer ribbons;
    [SerializeField] MeshRenderer cards;
    [SerializeField] Transform head;
    [SerializeField] TalkDemoController demo;
    [SerializeField] TalkDemoSpeechInput speech;
    [SerializeField] HairMode mode = HairMode.Cards;

    const double WarmupSeconds = 15;
    const double SampleSeconds = 30;
    readonly List<double> intervals = new List<double>(4000);
    double startedAt, sampleStart, previousFrame;
    int samples;
    bool measuring, turnedHead;
    Quaternion unturnedRotation;

    [Serializable]
    sealed class Report
    {
        public string status, mode, api;
        public int sample, frames, width, height;
        public double medianMs, p95Ms;
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern void EthanHairComparisonPublish(string json);
#endif

    void OnEnable()
    {
        if (!Application.isPlaying) return;
        fallback.ActivateFallback();
        ApplyMode(mode);
    }

    void OnDisable()
    {
        measuring = false;
        RestoreHead();
    }

    public void SetHairMode(string value)
    {
        if (!measuring && Enum.TryParse(value, out HairMode selected) && Enum.IsDefined(typeof(HairMode), selected))
            ApplyMode(selected);
    }

    public void ApplyMode(HairMode selected)
    {
        mode = selected;
        ribbons.enabled = selected == HairMode.Ribbons;
        cards.enabled = selected == HairMode.Cards;
    }

    [ContextMenu("Preview current ribbons")] void ShowRibbons() => SetPreview(HairMode.Ribbons);
    [ContextMenu("Preview groom-baked cards")] void ShowCards() => SetPreview(HairMode.Cards);
    [ContextMenu("Preview hidden hair")] void ShowHidden() => SetPreview(HairMode.Hidden);

    void SetPreview(HairMode selected)
    {
        if (measuring) return;
#if UNITY_EDITOR
        if (!Application.isPlaying)
            UnityEditor.Undo.RecordObjects(new UnityEngine.Object[] { this, ribbons, cards }, "Preview Ethan hair");
#endif
        ApplyMode(selected);
    }

    public void StartSampleRun()
    {
        if (measuring || demo.SelectedPersonaKey != "Ethan Reeve" || !demo.IsReady || !speech.PlaybackAllowed)
        {
            Publish("Wait for Ethan to finish greeting and enable audio.");
            return;
        }
        samples = 0;
        measuring = true;
        startedAt = Time.realtimeSinceStartupAsDouble;
        sampleStart = startedAt + WarmupSeconds;
        previousFrame = 0;
        intervals.Clear();
        speech.SendFixture();
        Publish("Warming up for 15 seconds");
    }

    void Update()
    {
        RestoreHead();
        if (!measuring) return;
        if (demo.SelectedPersonaKey != "Ethan Reeve" || demo.State == TalkDemoController.DemoState.Error)
        {
            measuring = false;
            Publish("Run interrupted. Select Ethan and restart.");
            return;
        }
        if (demo.IsReady && speech.PendingReplies == 0) speech.SendFixture();
        double now = Time.realtimeSinceStartupAsDouble;
        if (now < sampleStart) return;
        if (previousFrame > 0) intervals.Add((now - previousFrame) * 1000);
        previousFrame = now;
        if (now - sampleStart < SampleSeconds || intervals.Count == 0) return;

        intervals.Sort();
        samples++;
        Publish(samples == 3 ? "Complete" : "Sampling", intervals[intervals.Count / 2],
            intervals[(int)Math.Ceiling(intervals.Count * 0.95) - 1]);
        measuring = samples < 3;
        intervals.Clear();
        sampleStart = now;
        previousFrame = 0;
    }

    void LateUpdate()
    {
        if (!measuring) return;
        unturnedRotation = head.localRotation;
        float yaw = 36 * Mathf.Sin((float)((Time.realtimeSinceStartupAsDouble - startedAt) * Math.PI / 3));
        head.rotation = Quaternion.AngleAxis(yaw, Vector3.up) * head.rotation;
        turnedHead = true;
    }

    void RestoreHead()
    {
        if (!turnedHead) return;
        head.localRotation = unturnedRotation;
        turnedHead = false;
    }

    void Publish(string status, double median = 0, double p95 = 0)
    {
        string json = JsonUtility.ToJson(new Report {
            status = status, mode = mode.ToString(), api = SystemInfo.graphicsDeviceType.ToString(),
            sample = samples, frames = intervals.Count, medianMs = median, p95Ms = p95,
            width = Screen.width, height = Screen.height
        });
#if UNITY_WEBGL && !UNITY_EDITOR
        EthanHairComparisonPublish(json);
#else
        Debug.Log(json, this);
#endif
    }
}
