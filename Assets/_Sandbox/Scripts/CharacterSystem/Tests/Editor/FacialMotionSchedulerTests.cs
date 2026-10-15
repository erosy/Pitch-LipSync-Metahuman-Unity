using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Sandbox.CharacterSystem.Tests
{
    public sealed class FacialMotionSchedulerTests
    {
        sealed class FixedRandom : IFacialRandom { public float Unit() => .25f; }
        static FacialMotionDefinition Motion(string name, string channel, bool idleOnly, params BlendShapeTarget[] targets)
            => new FacialMotionDefinition {
                name = name, channel = channel, idleOnly = idleOnly, interval = new Vector2(.1f, .1f),
                easeIn = .1f, hold = new Vector2(.5f, .5f), easeOut = .1f,
                variants = new List<FacialMotionVariant> { FacialMotionDefaults.Variant("Paired", Vector2.one, targets) }
            };
        static FacialMotionContext Context(bool idle = true, bool speaking = false, bool transitioning = false, params string[] shapes)
            => new FacialMotionContext { idle = idle, speaking = speaking, transitioning = transitioning, expressionShapes = new HashSet<string>(shapes) };
        static FacialMotionScheduler Scheduler(List<FacialMotionDefinition> motions, List<string> warnings = null, params string[] reserved)
        {
            var scheduler = new FacialMotionScheduler(new FixedRandom());
            scheduler.Configure(motions, new HashSet<string>(BlendShapeDatabase.Default.Names), new HashSet<string>(reserved), warnings ?? new List<string>());
            return scheduler;
        }
        static FacialMotionDefinition Blink() => Motion("Blink", "Blink", false,
            new BlendShapeTarget("eyeBlinkLeft", 100), new BlendShapeTarget("eyeBlinkRight", 100));
        static FacialMotionDefinition Mouth() => Motion("Mouth", "Mouth", true, new BlendShapeTarget("mouthPucker", 20));

        [Test]
        public void BlinkRemainsAvailableDuringNonIdleSpeechAndExpressionTransitions()
        {
            var scheduler = Scheduler(new List<FacialMotionDefinition> { Blink(), Mouth() });
            var context = Context(false, true, true);
            scheduler.Tick(0, context); scheduler.Tick(.15f, context);
            Assert.That(scheduler.Output["eyeBlinkLeft"], Is.EqualTo(50).Within(.01));
            Assert.That(scheduler.Output["eyeBlinkRight"], Is.EqualTo(50).Within(.01));
            Assert.That(scheduler.Output.ContainsKey("mouthPucker"), Is.False);
        }

        [Test]
        public void IdleHappyAllowsNonConflictingMotionsAfterExpressionSettles()
        {
            var scheduler = Scheduler(new List<FacialMotionDefinition> { Mouth() });
            var context = Context(true, false, false, "emo_joy");
            scheduler.Tick(0, context); scheduler.Tick(.2f, context);
            Assert.That(scheduler.Output["mouthPucker"], Is.EqualTo(20).Within(.01));
        }

        [Test]
        public void LosingAnyIdleGateCancelsOutputAndEligibilityRestartsWithFreshWait()
        {
            foreach (var disallowed in new[] { Context(false), Context(true, true), Context(true, false, true) })
            {
                var scheduler = Scheduler(new List<FacialMotionDefinition> { Mouth() });
                scheduler.Tick(0, Context()); scheduler.Tick(.2f, Context());
                Assert.That(scheduler.Output["mouthPucker"], Is.EqualTo(20).Within(.01));
                scheduler.Tick(5, disallowed); Assert.That(scheduler.Output, Is.Empty);
                scheduler.Tick(5, Context()); Assert.That(scheduler.Channels[0].Phase, Is.EqualTo(FacialMotionPhase.Waiting));
                Assert.That(scheduler.Channels[0].Remaining, Is.EqualTo(.1f).Within(.001));
            }
        }

        [Test]
        public void ConflictOnOneEyeCancelsTheWholeBlinkPose()
        {
            var scheduler = Scheduler(new List<FacialMotionDefinition> { Blink() });
            scheduler.Tick(0, Context()); scheduler.Tick(.15f, Context());
            scheduler.Tick(0, Context(true, false, false, "eyeBlinkLeft"));
            Assert.That(scheduler.Output, Is.Empty); Assert.That(scheduler.Channels[0].Phase, Is.EqualTo(FacialMotionPhase.Inactive));
        }

        [Test]
        public void ConflictingVariantsAreFilteredWhileOtherVariantsRemainEligible()
        {
            var blink = Blink();
            blink.variants.Add(FacialMotionDefaults.Variant("Safe", Vector2.one, new BlendShapeTarget("eyeSquintRight", 30)));
            var scheduler = Scheduler(new List<FacialMotionDefinition> { blink });
            var context = Context(true, false, false, "eyeBlinkLeft");
            scheduler.Tick(0, context); scheduler.Tick(.2f, context);
            Assert.That(scheduler.Channels[0].Variant, Is.EqualTo("Safe"));
            Assert.That(scheduler.Output.ContainsKey("eyeBlinkRight"), Is.False);
            Assert.That(scheduler.Output["eyeSquintRight"], Is.EqualTo(30).Within(.01));
        }

        [Test]
        public void ReservedOrMissingShapeDisablesAnEntireVariant()
        {
            foreach (var shape in new[] { "eyeBlinkLeft", "missing" })
            {
                var motion = Blink();
                if (shape == "missing") motion.variants[0].pose.targets[0].shape = shape;
                var warnings = new List<string>();
                var scheduler = Scheduler(new List<FacialMotionDefinition> { motion }, warnings, "eyeBlinkLeft");
                scheduler.Tick(10, Context());
                Assert.That(scheduler.Output, Is.Empty); Assert.That(scheduler.Channels, Is.Empty);
                Assert.That(warnings, Has.Some.Contains("variant disabled"));
            }
        }

        [Test]
        public void DifferentChannelsCannotOwnTheSameShape()
        {
            var warnings = new List<string>();
            var scheduler = Scheduler(new List<FacialMotionDefinition> {
                Mouth(), Motion("Other", "Other", true, new BlendShapeTarget("mouthPucker", 30))
            }, warnings);
            Assert.That(scheduler.Channels, Is.Empty);
            Assert.That(warnings, Has.Some.Contains("overlaps different channels"));
        }

        [Test]
        public void RepeatsReuseOneVariantAndStrengthSampleAndThenWaitAgain()
        {
            var motion = Blink();
            motion.variants[0].strength = new Vector2(.5f, 1.5f);
            motion.repeat = new FacialMotionRepeat { probability = 1, maximumAdditionalRepetitions = 1, gap = new Vector2(.1f, .1f) };
            var scheduler = Scheduler(new List<FacialMotionDefinition> { motion });
            scheduler.Tick(0, Context()); scheduler.Tick(.2f, Context());
            Assert.That(scheduler.Output["eyeBlinkLeft"], Is.EqualTo(75).Within(.01));
            scheduler.Tick(.6f, Context()); Assert.That(scheduler.Channels[0].Phase, Is.EqualTo(FacialMotionPhase.RepeatGap));
            scheduler.Tick(.2f, Context()); Assert.That(scheduler.Output["eyeBlinkRight"], Is.EqualTo(75).Within(.01));
            Assert.That(scheduler.Channels[0].AdditionalRepetitions, Is.Zero);
            scheduler.Tick(.65f, Context()); Assert.That(scheduler.Channels[0].Phase, Is.EqualTo(FacialMotionPhase.Waiting));
        }

        [Test]
        public void OutgoingExpressionShapesStayProtectedUntilTheirFadeFinishes()
        {
            var playback = new ExpressionPlayback(new[] { "eyeBlinkLeft", "eyeBlinkRight" },
                new Dictionary<string, float> { ["eyeBlinkLeft"] = 100 });
            playback.Play(null, 1, .2f);
            playback.Tick(.1f); Assert.That(playback.ProtectedShapes, Does.Contain("eyeBlinkLeft"));
            var scheduler = Scheduler(new List<FacialMotionDefinition> { Blink() });
            var context = Context(); context.expressionShapes = playback.ProtectedShapes;
            scheduler.Tick(1, context); Assert.That(scheduler.Output, Is.Empty);
            playback.Tick(.1f); Assert.That(playback.ProtectedShapes, Is.Empty);
            scheduler.Tick(0, context); scheduler.Tick(.2f, context);
            Assert.That(scheduler.Output["eyeBlinkLeft"], Is.EqualTo(100).Within(.01));
        }
    }
}
