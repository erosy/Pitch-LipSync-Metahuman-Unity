using System;
using System.Collections.Generic;
using UnityEngine;

public interface IFacialRandom { float Unit(); }
public sealed class FacialRandom : IFacialRandom
{
    readonly System.Random random = new System.Random();
    public float Unit() => (float)random.NextDouble();
}

public static class FacialMath
{
    public static float NonNegative(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 0 : Mathf.Max(0, value);
    public static Vector2 Range(Vector2 value)
    {
        float a = NonNegative(value.x), b = NonNegative(value.y);
        return new Vector2(Mathf.Min(a, b), Mathf.Max(a, b));
    }
    public static float Sample(Vector2 range, IFacialRandom random)
    {
        range = Range(range);
        return Mathf.Lerp(range.x, range.y, Mathf.Clamp01(random.Unit()));
    }
    public static float Smooth(float value) { value = Mathf.Clamp01(value); return value * value * (3 - 2 * value); }
    public static float Weight(float value) => Mathf.Clamp(NonNegative(value), 0, 100);
    public static IEnumerable<BlendShapeTarget> Targets(BlendShapePose pose)
    {
        if (pose?.targets == null) yield break;
        foreach (var target in pose.targets) if (target != null) yield return target;
    }
}

public struct FacialContext
{
    public string expression;
    public bool speaking, transitioning;
    public bool Allows(FacialActivityDefinition activity)
    {
        if (!activity.enabled || (speaking && !activity.allowSpeech) || (transitioning && !activity.allowExpressionTransitions)) return false;
        if (activity.eligibility == ActivityEligibility.NeutralOnly) return string.IsNullOrEmpty(expression);
        if (activity.eligibility == ActivityEligibility.SelectedExpressions)
            return activity.expressionNames != null && activity.expressionNames.Contains(expression ?? "");
        return true;
    }
}

public enum FacialActivityPhase { Inactive, Waiting, EasingIn, Holding, EasingOut, RepeatGap }

// One independent state per channel; serialized definitions never receive runtime state.
public sealed class FacialActivityScheduler
{
    public sealed class ChannelState
    {
        public string Channel { get; internal set; }
        public string Activity => definition?.name ?? "";
        public string Variant => variant?.name ?? "";
        public FacialActivityPhase Phase { get; internal set; }
        public float Remaining => Mathf.Max(0, duration - elapsed);
        public float Strength { get; internal set; }
        public int AdditionalRepetitions { get; internal set; }
        internal FacialActivityDefinition definition;
        internal ActivityVariant variant;
        internal float elapsed, duration, easeIn, hold, easeOut, gap;
        internal readonly Dictionary<string, float> sampledWeights = new Dictionary<string, float>();
        internal readonly List<FacialActivityDefinition> choices = new List<FacialActivityDefinition>();
    }

    readonly IFacialRandom random;
    readonly List<ChannelState> channels = new List<ChannelState>();
    readonly Dictionary<FacialActivityDefinition, List<ActivityVariant>> variants = new Dictionary<FacialActivityDefinition, List<ActivityVariant>>();
    readonly Dictionary<string, float> output = new Dictionary<string, float>();
    public IReadOnlyList<ChannelState> Channels => channels;
    public IReadOnlyDictionary<string, float> Output => output;
    public IEnumerable<string> OwnedShapes
    {
        get
        {
            foreach (var channel in channels)
                foreach (var definition in channel.choices)
                    foreach (var variant in variants[definition])
                        foreach (var target in FacialMath.Targets(variant.pose)) yield return target.shape;
        }
    }
    public FacialActivityScheduler(IFacialRandom random) { this.random = random; }

    public void Configure(IReadOnlyList<FacialActivityDefinition> definitions, HashSet<string> available,
        HashSet<string> reserved, List<string> warnings)
    {
        channels.Clear(); variants.Clear(); output.Clear();
        if (definitions == null) return;
        var candidates = new List<FacialActivityDefinition>();
        var owners = new Dictionary<string, HashSet<string>>();
        var names = new HashSet<string>();
        foreach (var definition in definitions)
        {
            if (definition == null) { warnings.Add("Null activity definition."); continue; }
            if (string.IsNullOrWhiteSpace(definition.name) || !names.Add(definition.name))
            { warnings.Add("Activity names must be non-empty and unique: '" + definition.name + "'."); continue; }
            if (string.IsNullOrWhiteSpace(definition.channel)) { warnings.Add(definition.name + ": channel is empty."); continue; }
            var valid = new List<ActivityVariant>();
            var variantNames = new HashSet<string>();
            if (definition.variants != null)
                foreach (var variant in definition.variants)
                {
                    if (variant == null || string.IsNullOrWhiteSpace(variant.name) || !variantNames.Add(variant.name))
                    { warnings.Add(definition.name + ": variant names must be non-empty and unique."); continue; }
                    bool usable = true; int count = 0; var shapes = new HashSet<string>();
                    foreach (var target in FacialMath.Targets(variant.pose))
                    {
                        count++;
                        if (string.IsNullOrEmpty(target.shape) || !available.Contains(target.shape) || reserved.Contains(target.shape) || !shapes.Add(target.shape))
                        { warnings.Add(definition.name + "/" + variant.name + ": missing, repeated, or reserved shape '" + target.shape + "'; variant disabled."); usable = false; }
                    }
                    if (usable && count > 0) valid.Add(variant);
                }
            if (valid.Count == 0) { warnings.Add(definition.name + ": no valid variants; activity disabled."); continue; }
            candidates.Add(definition); variants.Add(definition, valid);
            foreach (var variant in valid)
                foreach (var target in FacialMath.Targets(variant.pose))
                {
                    if (!owners.TryGetValue(target.shape, out var set)) owners.Add(target.shape, set = new HashSet<string>());
                    set.Add(definition.channel);
                }
        }
        foreach (var definition in candidates)
        {
            bool conflict = false;
            foreach (var variant in variants[definition])
                foreach (var target in FacialMath.Targets(variant.pose))
                    if (owners[target.shape].Count > 1) conflict = true;
            if (conflict) { warnings.Add(definition.name + ": shape ownership overlaps different channels; activity disabled."); continue; }
            var channel = channels.Find(c => c.Channel == definition.channel);
            if (channel == null) { channel = new ChannelState { Channel = definition.channel }; channels.Add(channel); }
            channel.choices.Add(definition);
        }
    }

    int Pick(int count) => Mathf.Min(count - 1, (int)(Mathf.Clamp01(random.Unit()) * count));
    void Wait(ChannelState channel, FacialContext context)
    {
        channel.definition = null; channel.variant = null; channel.sampledWeights.Clear();
        channel.AdditionalRepetitions = 0; channel.Strength = 0;
        var eligible = channel.choices.FindAll(context.Allows);
        if (eligible.Count == 0) { channel.Phase = FacialActivityPhase.Inactive; return; }
        channel.definition = eligible[Pick(eligible.Count)];
        Enter(channel, FacialActivityPhase.Waiting, Mathf.Max(.001f, FacialMath.Sample(channel.definition.interval, random)));
    }
    static void Enter(ChannelState channel, FacialActivityPhase phase, float duration)
    { channel.Phase = phase; channel.duration = duration; channel.elapsed = 0; }
    void Start(ChannelState channel)
    {
        var definition = channel.definition;
        var choices = variants[definition];
        channel.variant = choices[Pick(choices.Count)];
        channel.Strength = FacialMath.Sample(channel.variant.strength, random);
        channel.sampledWeights.Clear();
        foreach (var target in FacialMath.Targets(channel.variant.pose))
            channel.sampledWeights[target.shape] = FacialMath.Weight(target.weight * channel.Strength);
        channel.easeIn = FacialMath.NonNegative(definition.easeIn);
        channel.hold = FacialMath.Sample(definition.hold, random);
        channel.easeOut = FacialMath.NonNegative(definition.easeOut);
        var repeat = definition.repeat;
        channel.AdditionalRepetitions = repeat != null && random.Unit() < Mathf.Clamp01(repeat.probability)
            ? Mathf.Clamp(repeat.maximumAdditionalRepetitions, 0, 32) : 0;
        channel.gap = repeat == null ? 0 : FacialMath.Sample(repeat.gap, random);
        Enter(channel, FacialActivityPhase.EasingIn, channel.easeIn);
    }
    void Advance(ChannelState channel, FacialContext context)
    {
        switch (channel.Phase)
        {
            case FacialActivityPhase.Waiting: Start(channel); break;
            case FacialActivityPhase.EasingIn: Enter(channel, FacialActivityPhase.Holding, channel.hold); break;
            case FacialActivityPhase.Holding: Enter(channel, FacialActivityPhase.EasingOut, channel.easeOut); break;
            case FacialActivityPhase.EasingOut:
                if (channel.AdditionalRepetitions > 0) { channel.AdditionalRepetitions--; Enter(channel, FacialActivityPhase.RepeatGap, channel.gap); }
                else Wait(channel, context);
                break;
            case FacialActivityPhase.RepeatGap: Enter(channel, FacialActivityPhase.EasingIn, channel.easeIn); break;
        }
    }

    public void Cancel()
    {
        output.Clear();
        foreach (var channel in channels)
        { channel.Phase = FacialActivityPhase.Inactive; channel.definition = null; channel.variant = null; channel.sampledWeights.Clear(); }
    }

    public void Tick(float deltaTime, FacialContext context)
    {
        output.Clear();
        foreach (var channel in channels)
        {
            // Discard this frame's elapsed time when eligibility changes: always start a fresh wait.
            if (channel.definition != null && !context.Allows(channel.definition)) { Wait(channel, context); continue; }
            if (channel.Phase == FacialActivityPhase.Inactive) { Wait(channel, context); continue; }
            float remaining = FacialMath.NonNegative(deltaTime);
            // Zero duration phases are allowed; the minimum interval guarantees forward progress.
            for (int boundary = 0; boundary < 4096 && channel.Phase != FacialActivityPhase.Inactive; boundary++)
            {
                float step = Mathf.Min(remaining, Mathf.Max(0, channel.duration - channel.elapsed));
                channel.elapsed += step; remaining -= step;
                if (channel.elapsed < channel.duration) break;
                Advance(channel, context);
                if (remaining <= 0 && channel.duration > 0) break;
            }
            float progress = channel.duration > 0 ? channel.elapsed / channel.duration : 1;
            float factor = channel.Phase == FacialActivityPhase.EasingIn ? FacialMath.Smooth(progress)
                : channel.Phase == FacialActivityPhase.Holding ? 1
                : channel.Phase == FacialActivityPhase.EasingOut ? 1 - FacialMath.Smooth(progress) : 0;
            foreach (var pair in channel.sampledWeights) output[pair.Key] = pair.Value * factor;
        }
    }
}
