using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Sandbox.CharacterSystem
{
    /// <summary>Instance-only clip mixer. Both gameplay and previews use the same explicit clock.</summary>
    internal sealed class AnimationPlayback : IDisposable
    {
        sealed class Track
        {
            internal AnimationClipPlayable Playable;
            internal int Input;
            internal double Time, Length;
            internal float Speed, Weight, StartWeight;
            internal bool Loop;
        }

        readonly List<Track> tracks = new List<Track>();
        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        AnimationPlayableOutput output;
        Track current;
        float fadeElapsed, fadeDuration;
        bool completionReported;

        internal AnimationDefinition Definition { get; private set; }
        internal double Time => current?.Time ?? 0;
        internal float NormalizedTime => current == null ? 0 : (float)(current.Time / current.Length);
        internal bool Loop => current != null && current.Loop;
        internal bool IsTransitioning => tracks.Count > 1;
        internal int ClipCount => tracks.Count;
        internal bool IsValid => graph.IsValid();

        internal AnimationPlayback(Animator animator, string graphName)
        {
            try
            {
                graph = PlayableGraph.Create(graphName);
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                mixer = AnimationMixerPlayable.Create(graph, 0);
                output = AnimationPlayableOutput.Create(graph, "Body", animator);
                output.SetSourcePlayable(mixer);
                graph.Play();
            }
            catch { Dispose(); throw; }
        }

        internal void Play(AnimationDefinition definition)
        {
            Definition = definition;
            completionReported = false;
            fadeElapsed = 0;
            fadeDuration = definition.CrossfadeDuration;
            // Preserve every currently contributing clip when a crossfade is interrupted.
            for (int i = tracks.Count - 1; i >= 0; i--)
            {
                if (tracks[i].Weight <= .00001f) RemoveTrack(i);
                else tracks[i].StartWeight = tracks[i].Weight;
            }
            int input = 0;
            while (tracks.Exists(track => track.Input == input)) input++;
            if (input >= mixer.GetInputCount()) mixer.SetInputCount(input + 1);
            var playable = AnimationClipPlayable.Create(graph, definition.Clip);
            playable.SetApplyFootIK(false);
            playable.SetApplyPlayableIK(false);
            playable.SetSpeed(0);
            playable.SetDuration(double.MaxValue);
            graph.Connect(playable, 0, mixer, input);
            current = new Track
            {
                Playable = playable, Input = input, Length = definition.Clip.length,
                Speed = definition.PlaybackSpeed, Loop = definition.Loop,
                Weight = tracks.Count == 0 || fadeDuration == 0 ? 1 : 0
            };
            tracks.Add(current);
            if (fadeDuration == 0) RemoveOutgoing();
            Evaluate();
        }

        /// <returns>True once, when the current one-shot naturally reaches its end.</returns>
        internal bool Advance(float deltaTime)
        {
            if (!IsValid || current == null) return false;
            float delta = FiniteDelta(deltaTime);
            if (delta > 0)
                foreach (var track in tracks)
                {
                    double next = track.Time + delta * (double)track.Speed;
                    track.Time = track.Loop ? next % track.Length : Math.Min(next, track.Length);
                }
            if (tracks.Count > 1)
            {
                fadeElapsed = Mathf.Min(fadeDuration, fadeElapsed + delta);
                float progress = fadeDuration > 0 ? Mathf.SmoothStep(0, 1, fadeElapsed / fadeDuration) : 1;
                foreach (var track in tracks)
                    track.Weight = track == current ? progress : track.StartWeight * (1 - progress);
                if (progress >= 1) RemoveOutgoing();
            }
            Evaluate();
            bool completed = !current.Loop && current.Time >= current.Length && !completionReported;
            if (completed) completionReported = true;
            return completed;
        }

        internal void Seek(float normalizedTime)
        {
            if (current == null || float.IsNaN(normalizedTime) || float.IsInfinity(normalizedTime)) return;
            current.Time = Mathf.Clamp01(normalizedTime) * current.Length;
            // Scrubbing is a preview operation, never a gameplay completion notification.
            completionReported = current.Time >= current.Length;
            Evaluate();
        }

        internal void SetOutputWeight(float weight) { if (IsValid) output.SetWeight(weight); }

        internal void Evaluate()
        {
            if (!IsValid || current == null) return;
            foreach (var track in tracks)
            {
                // An imported looping clip samples its first pose at exactly Length. Sample just
                // before that boundary so a definition-forced one-shot holds the last pose.
                double sampleTime = Math.Min(track.Time, Math.Max(0, track.Length - 0.00001));
                track.Playable.SetTime(sampleTime);
                track.Playable.SetDone(false);
                mixer.SetInputWeight(track.Input, track.Weight);
            }
            graph.Evaluate(0);
        }

        void RemoveOutgoing()
        {
            for (int i = tracks.Count - 1; i >= 0; i--)
            {
                var track = tracks[i];
                if (track == current) continue;
                RemoveTrack(i);
            }
            current.Weight = 1;
        }

        void RemoveTrack(int index)
        {
            var track = tracks[index];
            mixer.DisconnectInput(track.Input);
            graph.DestroyPlayable(track.Playable);
            tracks.RemoveAt(index);
        }

        internal static float FiniteDelta(float delta) => float.IsNaN(delta) || float.IsInfinity(delta) ? 0 : Mathf.Max(0, delta);

        public void Dispose()
        {
            if (graph.IsValid()) graph.Destroy();
            tracks.Clear(); current = null; Definition = null;
        }
    }
}
