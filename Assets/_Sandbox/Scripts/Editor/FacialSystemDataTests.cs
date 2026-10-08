using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// These tests only operate on definitions and plain runtime classes. No GameObjects,
// meshes, renderer writes, animation sampling, or Play Mode are needed.
public static class FacialSystemDataTests
{
    sealed class FixedRandom : IFacialRandom
    {
        public int calls;
        readonly float value;
        public FixedRandom(float value = .25f) { this.value = value; }
        public float Unit() { calls++; return value; }
    }
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Near(float actual, float expected, string message) => Check(Mathf.Abs(actual - expected) < .001f, message + $" ({actual} != {expected})");
    static FacialActivityDefinition Activity(string name = "A", string channel = "Mouth", string shape = "a")
        => new FacialActivityDefinition { name = name, channel = channel, interval = new Vector2(.1f, .1f),
            easeIn = .2f, hold = new Vector2(.3f, .3f), easeOut = .4f,
            variants = new List<ActivityVariant> { FacialPresets.Variant("Pair", Vector2.one, new BlendShapeTarget(shape, 100)) } };
    static FacialActivityScheduler Scheduler(params FacialActivityDefinition[] definitions)
    {
        var scheduler = new FacialActivityScheduler(new FixedRandom());
        scheduler.Configure(definitions, new HashSet<string> { "a", "b", "c" }, new HashSet<string>(), new List<string>());
        scheduler.Tick(0, new FacialContext()); return scheduler;
    }
    static FacialActivityScheduler.ChannelState State(FacialActivityScheduler scheduler) => scheduler.Channels[0];
    [MenuItem("Tools/Facial System/Run Data-only Checks")]
    public static void RunFromMenu() => Debug.Log(RunAll());
    public static string RunAll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Data-only checks require Edit Mode.");
        var passed = new List<string>();
        Action<string, Action> test = (name, action) => { action(); passed.Add(name); };
        test("Elapsed time crosses phase boundaries", () =>
        {
            var scheduler = Scheduler(Activity()); scheduler.Tick(.2f, new FacialContext());
            Check(State(scheduler).Phase == FacialActivityPhase.EasingIn, "Expected easing in"); Near(scheduler.Output["a"], 50, "Halfway ease-in");
            scheduler.Tick(.25f, new FacialContext()); Check(State(scheduler).Phase == FacialActivityPhase.Holding, "Expected holding");
            Near(State(scheduler).Remaining, .15f, "Carry-over into hold");
            scheduler.Tick(.25f, new FacialContext()); Check(State(scheduler).Phase == FacialActivityPhase.EasingOut, "Expected easing out");
            Near(State(scheduler).Remaining, .3f, "Carry-over into ease-out");
        });
        test("Phase progression is frame-rate independent", () =>
        {
            var coarse = Scheduler(Activity()); var fine = Scheduler(Activity());
            coarse.Tick(3.735f, new FacialContext()); for (int i = 0; i < 747; i++) fine.Tick(.005f, new FacialContext());
            Check(State(coarse).Phase == State(fine).Phase, "Different phases"); Near(State(coarse).Remaining, State(fine).Remaining, "Different elapsed time");
            Near(coarse.Output["a"], fine.Output["a"], "Different weights");
        });
        test("A variant samples one synchronized strength", () =>
        {
            var activity = Activity(); activity.variants[0].strength = new Vector2(.5f, 1);
            activity.variants[0].pose = new BlendShapePose(new BlendShapeTarget("a", 20), new BlendShapeTarget("b", 5));
            var scheduler = Scheduler(activity); scheduler.Tick(.3f, new FacialContext());
            Near(scheduler.Output["a"], 12.5f, "First target"); Near(scheduler.Output["b"], 3.125f, "Second target");
        });
        test("Repeats reuse the pose and roll once", () =>
        {
            var random = new FixedRandom(); var scheduler = new FacialActivityScheduler(random); var activity = Activity();
            activity.repeat = new RepeatSettings { probability = 1, maximumAdditionalRepetitions = 1, gap = new Vector2(.15f, .15f) };
            scheduler.Configure(new[] { activity }, new HashSet<string> { "a" }, new HashSet<string>(), new List<string>());
            scheduler.Tick(0, new FacialContext()); scheduler.Tick(.95f, new FacialContext());
            Check(State(scheduler).Phase == FacialActivityPhase.EasingOut, "First blink must still be opening"); int calls = random.calls;
            var variant = State(scheduler).Variant; float strength = State(scheduler).Strength;
            scheduler.Tick(.1f, new FacialContext()); Check(State(scheduler).Phase == FacialActivityPhase.RepeatGap, "Expected repeat gap");
            scheduler.Tick(.1001f, new FacialContext()); Check(State(scheduler).Phase == FacialActivityPhase.EasingIn, "Second repetition starts");
            Check(random.calls == calls && State(scheduler).Variant == variant, "Repeat sampled again"); Near(State(scheduler).Strength, strength, "Repeat strength changed");
            scheduler.Tick(.91f, new FacialContext()); Check(State(scheduler).Phase == FacialActivityPhase.Waiting, "Exactly two gestures must end the sequence");
        });
        test("Zero probability never repeats", () =>
        {
            var activity = Activity(); activity.repeat.probability = 0;
            var scheduler = Scheduler(activity); scheduler.Tick(1.01f, new FacialContext());
            Check(State(scheduler).Phase == FacialActivityPhase.Waiting, "Unexpected repeat");
        });
        test("Repeated activities stay within their configured bound", () =>
        {
            var activity = Activity(); activity.repeat.probability = 1; activity.repeat.maximumAdditionalRepetitions = 2;
            activity.repeat.gap = new Vector2(.1f, .1f);
            var scheduler = Scheduler(activity); scheduler.Tick(3.01f, new FacialContext());
            Check(State(scheduler).Phase == FacialActivityPhase.Waiting, "Three gestures should complete the sequence");
        });
        test("One activity per channel, independent channel overlap", () =>
        {
            var scheduler = Scheduler(Activity("A"), Activity("B", "Mouth", "b"), Activity("C", "Gaze", "c"));
            Check(scheduler.Channels.Count == 2, "Channel grouping failed"); scheduler.Tick(.3f, new FacialContext());
            Check(scheduler.Output["a"] > 0 && scheduler.Output["c"] > 0 && !scheduler.Output.ContainsKey("b"), "Channel exclusivity/overlap failed");
        });
        test("Uniform selection reaches each activity and variant", () =>
        {
            foreach (float roll in new[] { .1f, .9f })
            {
                var scheduler = new FacialActivityScheduler(new FixedRandom(roll)); var a = Activity();
                a.variants.Add(FacialPresets.Variant("Other", Vector2.one, new BlendShapeTarget("b", 25)));
                scheduler.Configure(new[] { a, Activity("Other activity") }, new HashSet<string> { "a", "b" }, new HashSet<string>(), new List<string>());
                scheduler.Tick(0, new FacialContext()); Check(State(scheduler).Activity == (roll < .5f ? "A" : "Other activity"), "Activity selection failed");
                scheduler.Configure(new[] { a }, new HashSet<string> { "a", "b" }, new HashSet<string>(), new List<string>());
                scheduler.Tick(0, new FacialContext()); scheduler.Tick(.2f, new FacialContext());
                Check(State(scheduler).Variant == (roll < .5f ? "Pair" : "Other"), "Variant selection failed");
            }
        });
        test("Speech, Joy, and transitions cancel with a fresh wait", () =>
        {
            foreach (var context in new[] { new FacialContext { speaking = true }, new FacialContext { expression = "Joy" }, new FacialContext { transitioning = true } })
            {
                var scheduler = Scheduler(Activity()); scheduler.Tick(.3f, new FacialContext()); scheduler.Tick(.01f, context);
                Check(scheduler.Output.Count == 0 && State(scheduler).Phase == FacialActivityPhase.Inactive, "Gesture did not cancel");
                scheduler.Tick(99, new FacialContext()); Check(State(scheduler).Phase == FacialActivityPhase.Waiting, "Resumption skipped fresh wait");
                Near(State(scheduler).Remaining, .1f, "Fresh interval missing");
            }
        });
        test("Blink eligibility and selected-expression eligibility", () =>
        {
            var blink = FacialPresets.Activities()[0];
            Check(new FacialContext { expression = "Joy", speaking = true, transitioning = true }.Allows(blink), "Blink incorrectly blocked");
            var selected = Activity(); selected.eligibility = ActivityEligibility.SelectedExpressions; selected.expressionNames.Add("Joy");
            Check(new FacialContext { expression = "Joy" }.Allows(selected) && !new FacialContext().Allows(selected), "Selected expression filter failed");
        });
        test("Expression, viseme, and cross-channel conflicts are rejected", () =>
        {
            var scheduler = new FacialActivityScheduler(new FixedRandom()); var warnings = new List<string>();
            scheduler.Configure(new[] { Activity("A"), Activity("B", "Gaze"), Activity("C", "Blink", "b") },
                new HashSet<string> { "a", "b" }, new HashSet<string> { "b" }, warnings);
            Check(scheduler.Channels.Count == 0 && warnings.Count >= 3, "Ownership conflicts allowed");
        });
        test("Invalid paired variants are excluded as a whole", () =>
        {
            var activity = Activity(); activity.variants[0].pose.targets.Add(new BlendShapeTarget("missing", 50));
            activity.variants.Add(FacialPresets.Variant("Valid", Vector2.one, new BlendShapeTarget("b", 50)));
            var scheduler = Scheduler(activity); scheduler.Tick(.3f, new FacialContext());
            Check(State(scheduler).Variant == "Valid" && !scheduler.Output.ContainsKey("a"), "Partial invalid pose was used");
        });
        test("Cancel/re-enable and pause do not advance a gesture", () =>
        {
            var scheduler = Scheduler(Activity()); scheduler.Tick(.2f, new FacialContext()); var weight = scheduler.Output["a"];
            scheduler.Tick(0, new FacialContext()); Near(scheduler.Output["a"], weight, "Paused gesture advanced");
            scheduler.Cancel(); Check(scheduler.Output.Count == 0, "Cancellation left weights"); scheduler.Tick(9, new FacialContext());
            Check(State(scheduler).Phase == FacialActivityPhase.Waiting, "Re-enable did not schedule fresh wait");
        });
        test("Zero-duration phases terminate and numeric inputs are bounded", () =>
        {
            var activity = Activity(); activity.easeIn = activity.easeOut = 0; activity.hold = Vector2.zero;
            activity.repeat.probability = 1; activity.repeat.maximumAdditionalRepetitions = 999; activity.repeat.gap = Vector2.zero;
            var scheduler = Scheduler(activity); scheduler.Tick(.105f, new FacialContext());
            Check(State(scheduler).Phase == FacialActivityPhase.Waiting, "Zero-duration sequence did not complete");
            Near(FacialMath.Weight(float.NaN), 0, "NaN not bounded"); Near(FacialMath.Weight(150), 100, "Weight not clamped");
        });
        test("Persistent expression crossfade and speech poses", () =>
        {
            var playback = new FacialExpressionPlayback(); playback.Configure(new[] { "a", "b" });
            var definition = new ExpressionDefinition { name = "Joy", silentPose = new BlendShapePose(new BlendShapeTarget("a", 100)), speakingPose = new BlendShapePose(new BlendShapeTarget("b", 40)) };
            playback.Tick(.1f, new[] { definition }, "Joy", false, .5f, .2f); Near(playback.Output["a"], 25, "Crossfade mismatch");
            playback.Tick(10, new[] { definition }, "Joy", false, .5f, .2f); Near(playback.Output["a"], 50, "Expression did not persist");
            playback.Tick(.2f, new[] { definition }, "Joy", true, .5f, .2f); Near(playback.Output["a"], 0, "Silent pose lingered"); Near(playback.Output["b"], 20, "Speaking pose mismatch");
            playback.Tick(.2f, new[] { definition }, null, false, .5f, .2f); Near(playback.Output["b"], 0, "Neutral did not clear expression");
            definition.silentPose.targets.Add(new BlendShapeTarget(null, 40));
            playback.Tick(.2f, new[] { definition }, "Joy", false, .5f, .2f); Near(playback.Output["a"], 50, "Invalid target should be skipped safely");
            playback.Tick(.2f, new[] { definition }, "Joy", false, 1.5f, .2f); Near(playback.Output["a"], 150, "Intensity scaling above 100 was lost");
        });
        test("Shared definitions round-trip through Unity serialization", () =>
        {
            var activity = FacialPresets.Activities()[3]; string json = JsonUtility.ToJson(activity);
            var copy = JsonUtility.FromJson<FacialActivityDefinition>(json);
            Check(copy.channel == "Gaze" && copy.variants.Count == 4 && copy.variants[0].pose.targets.Count == 2, "Unity serialization lost nested data");
            Check(copy.variants[0].strength == activity.variants[0].strength && copy.hold == activity.hold, "Unity serialization lost ranges");
            var expression = FacialPresets.Expressions()[0]; var expressionCopy = JsonUtility.FromJson<ExpressionDefinition>(JsonUtility.ToJson(expression));
            Check(expressionCopy.speakingPose.targets.Count == 7 && expressionCopy.silentPose.targets[0].shape == BlendShapeNames.Joy, "Expression serialization failed");
        });
        test("Configuration warnings identify invalid ranges", () =>
        {
            var activity = Activity(); activity.interval = new Vector2(2, 1); activity.easeIn = float.NaN; activity.repeat.probability = 2;
            var warnings = new List<string>(); FacialValidation.Check(null, new[] { activity }, warnings);
            Check(warnings.Count == 3, "Expected interval, phase, and repeat warnings");
        });
        return passed.Count + " data-only facial checks passed:\n" + string.Join("\n", passed);
    }
}
