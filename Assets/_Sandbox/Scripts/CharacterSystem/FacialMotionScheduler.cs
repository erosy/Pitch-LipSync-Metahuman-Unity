using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sandbox.CharacterSystem
{
    public struct FacialMotionContext
    {
        public bool idle, speaking, transitioning;
        public HashSet<string> expressionShapes;
        public bool Allows(FacialMotionDefinition motion)
            => motion.enabled && (!motion.idleOnly || (idle && !speaking && !transitioning));
        public bool Allows(FacialMotionVariant variant)
        {
            foreach (var target in FacialMath.Targets(variant.pose))
                if (expressionShapes != null && expressionShapes.Contains(target.shape)) return false;
            return true;
        }
    }

    public enum FacialMotionPhase { Inactive, Waiting, EasingIn, Holding, EasingOut, RepeatGap }

    // One independent state per channel; serialized definitions never receive runtime state.
    public sealed class FacialMotionScheduler
    {
        public sealed class ChannelState
        {
            public string Channel { get; internal set; }
            public string Motion => definition?.name ?? "";
            public string Variant => variant?.name ?? "";
            public FacialMotionPhase Phase { get; internal set; }
            public float Remaining => Mathf.Max(0, duration - elapsed);
            public float Strength { get; internal set; }
            public int AdditionalRepetitions { get; internal set; }
            internal FacialMotionDefinition definition;
            internal FacialMotionVariant variant;
            internal float elapsed, duration, easeIn, hold, easeOut, gap;
            internal readonly Dictionary<string, float> sampledWeights = new Dictionary<string, float>();
            internal readonly List<FacialMotionDefinition> choices = new List<FacialMotionDefinition>();
        }

        readonly IFacialRandom random;
        readonly List<ChannelState> channels = new List<ChannelState>();
        readonly Dictionary<FacialMotionDefinition, List<FacialMotionVariant>> variants = new Dictionary<FacialMotionDefinition, List<FacialMotionVariant>>();
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
        public FacialMotionScheduler(IFacialRandom random) { this.random = random; }

        public void Configure(IReadOnlyList<FacialMotionDefinition> definitions, HashSet<string> available,
            HashSet<string> reserved, List<string> warnings)
        {
            channels.Clear(); variants.Clear(); output.Clear();
            if (definitions == null) return;
            var candidates = new List<FacialMotionDefinition>();
            var owners = new Dictionary<string, HashSet<string>>();
            var names = new HashSet<string>();
            foreach (var definition in definitions)
            {
                if (definition == null) { warnings.Add("Null facial motion definition."); continue; }
                if (string.IsNullOrWhiteSpace(definition.name) || !names.Add(definition.name))
                { warnings.Add("Facial motion names must be non-empty and unique: '" + definition.name + "'."); continue; }
                if (string.IsNullOrWhiteSpace(definition.channel)) { warnings.Add(definition.name + ": channel is empty."); continue; }
                var valid = new List<FacialMotionVariant>();
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
                if (valid.Count == 0) { warnings.Add(definition.name + ": no valid variants; facial motion disabled."); continue; }
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
                if (conflict) { warnings.Add(definition.name + ": shape ownership overlaps different channels; facial motion disabled."); continue; }
                var channel = channels.Find(c => c.Channel == definition.channel);
                if (channel == null) { channel = new ChannelState { Channel = definition.channel }; channels.Add(channel); }
                channel.choices.Add(definition);
            }
        }

        int Pick(int count) => Mathf.Min(count - 1, (int)(Mathf.Clamp01(random.Unit()) * count));
        void Wait(ChannelState channel, FacialMotionContext context)
        {
            channel.definition = null; channel.variant = null; channel.sampledWeights.Clear();
            channel.AdditionalRepetitions = 0; channel.Strength = 0;
            var eligible = channel.choices.FindAll(d => context.Allows(d) && variants[d].Exists(context.Allows));
            if (eligible.Count == 0) { channel.Phase = FacialMotionPhase.Inactive; return; }
            channel.definition = eligible[Pick(eligible.Count)];
            Enter(channel, FacialMotionPhase.Waiting, Mathf.Max(.001f, FacialMath.Sample(channel.definition.interval, random)));
        }
        static void Enter(ChannelState channel, FacialMotionPhase phase, float duration)
        { channel.Phase = phase; channel.duration = duration; channel.elapsed = 0; }
        void Start(ChannelState channel, FacialMotionContext context)
        {
            var definition = channel.definition;
            var choices = variants[definition].FindAll(context.Allows);
            if (choices.Count == 0) { Wait(channel, context); return; }
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
            Enter(channel, FacialMotionPhase.EasingIn, channel.easeIn);
        }
        void Advance(ChannelState channel, FacialMotionContext context)
        {
            switch (channel.Phase)
            {
                case FacialMotionPhase.Waiting: Start(channel, context); break;
                case FacialMotionPhase.EasingIn: Enter(channel, FacialMotionPhase.Holding, channel.hold); break;
                case FacialMotionPhase.Holding: Enter(channel, FacialMotionPhase.EasingOut, channel.easeOut); break;
                case FacialMotionPhase.EasingOut:
                    if (channel.AdditionalRepetitions > 0) { channel.AdditionalRepetitions--; Enter(channel, FacialMotionPhase.RepeatGap, channel.gap); }
                    else Wait(channel, context);
                    break;
                case FacialMotionPhase.RepeatGap: Enter(channel, FacialMotionPhase.EasingIn, channel.easeIn); break;
            }
        }

        public void Cancel()
        {
            output.Clear();
            foreach (var channel in channels)
            { channel.Phase = FacialMotionPhase.Inactive; channel.definition = null; channel.variant = null; channel.sampledWeights.Clear(); }
        }

        public void Tick(float deltaTime, FacialMotionContext context)
        {
            output.Clear();
            foreach (var channel in channels)
            {
                // Discard this frame's elapsed time when eligibility changes: always start a fresh wait.
                if (channel.definition != null && (!context.Allows(channel.definition) || (channel.variant != null && !context.Allows(channel.variant)))) { Wait(channel, context); continue; }
                if (channel.Phase == FacialMotionPhase.Inactive) { Wait(channel, context); continue; }
                float remaining = FacialMath.NonNegative(deltaTime);
                // Zero duration phases are allowed; the minimum interval guarantees forward progress.
                for (int boundary = 0; boundary < 4096 && channel.Phase != FacialMotionPhase.Inactive; boundary++)
                {
                    float step = Mathf.Min(remaining, Mathf.Max(0, channel.duration - channel.elapsed));
                    channel.elapsed += step; remaining -= step;
                    if (channel.elapsed < channel.duration) break;
                    Advance(channel, context);
                    if (remaining <= 0 && channel.duration > 0) break;
                }
                float progress = channel.duration > 0 ? channel.elapsed / channel.duration : 1;
                float factor = channel.Phase == FacialMotionPhase.EasingIn ? FacialMath.Smooth(progress)
                    : channel.Phase == FacialMotionPhase.Holding ? 1
                    : channel.Phase == FacialMotionPhase.EasingOut ? 1 - FacialMath.Smooth(progress) : 0;
                foreach (var pair in channel.sampledWeights) output[pair.Key] = pair.Value * factor;
            }
        }
    }

}
