using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Sandbox.CharacterSystem
{
    [Serializable]
    public class FacialMotionVariant
    {
        public string name;
        public BlendShapePose pose = new BlendShapePose();
        [MinMaxSlider(0, 2, true)] public Vector2 strength = Vector2.one;
    }

    [Serializable]
    public class FacialMotionRepeat
    {
        [Range(0, 1)] public float probability;
        [ShowIf("HasProbability"), Min(0)] public int maximumAdditionalRepetitions = 1;
        [ShowIf("HasProbability"), MinMaxSlider(0, 2, true)] public Vector2 gap = new Vector2(.12f, .25f);
        bool HasProbability => probability > 0;
    }

    [Serializable]
    public class FacialMotionDefinition
    {
        public string name;
        public bool enabled = true;
        public string channel = "Mouth";
        [Tooltip("Mouth and gaze require Idle, a settled expression, and silence. Disable only for motions such as blinking.")] public bool idleOnly = true;
        [MinMaxSlider(0, 30, true)] public Vector2 interval = new Vector2(8, 16);
        [Min(0)] public float easeIn = .25f;
        [MinMaxSlider(0, 5, true)] public Vector2 hold = new Vector2(.25f, .5f);
        [Min(0)] public float easeOut = .35f;
        [ListDrawerSettings(ShowFoldout = true)] public List<FacialMotionVariant> variants = new List<FacialMotionVariant>();
        public FacialMotionRepeat repeat = new FacialMotionRepeat();
    }


    public static class FacialMotionDefaults
    {
        public static FacialMotionVariant Variant(string name, Vector2 strength, params BlendShapeTarget[] targets)
            => new FacialMotionVariant { name = name, strength = strength, pose = new BlendShapePose(targets) };

        public static List<FacialMotionDefinition> Create() => new List<FacialMotionDefinition>
        {
            new FacialMotionDefinition { name = "Blink", channel = "Blink", idleOnly = false,
                interval = new Vector2(2, 6),
                easeIn = .06f, hold = new Vector2(.03f, .03f), easeOut = .12f,
                repeat = new FacialMotionRepeat { probability = .15f, maximumAdditionalRepetitions = 1, gap = new Vector2(.12f, .25f) },
                variants = new List<FacialMotionVariant> { Variant("Both eyes", Vector2.one,
                    new BlendShapeTarget(BlendShapeNames.EyeBlinkLeft, 100), new BlendShapeTarget(BlendShapeNames.EyeBlinkRight, 100)) } },
            new FacialMotionDefinition { name = "Lip purse", channel = "Mouth", variants = new List<FacialMotionVariant>
                { Variant("Purse", Vector2.one, new BlendShapeTarget(BlendShapeNames.MouthPucker, 15),
                    new BlendShapeTarget(BlendShapeNames.MouthPressLeft, 5), new BlendShapeTarget(BlendShapeNames.MouthPressRight, 5)) } },
            new FacialMotionDefinition { name = "Lip twitch", channel = "Mouth", easeIn = .06f,
                hold = new Vector2(.03f, .03f), easeOut = .12f,
                variants = new List<FacialMotionVariant> {
                    Variant("Left", new Vector2(.5f, 1), new BlendShapeTarget(BlendShapeNames.MouthLeft, 10)),
                    Variant("Right", new Vector2(.5f, 1), new BlendShapeTarget(BlendShapeNames.MouthRight, 10)) } },
            new FacialMotionDefinition { name = "Gaze", channel = "Gaze", interval = new Vector2(4, 9),
                easeIn = .18f, hold = new Vector2(.8f, 1.8f), easeOut = .25f,
                variants = new List<FacialMotionVariant> {
                    Variant("Character left", new Vector2(.6f, 1), new BlendShapeTarget(BlendShapeNames.EyeLookOutLeft, 25), new BlendShapeTarget(BlendShapeNames.EyeLookInRight, 25)),
                    Variant("Character right", new Vector2(.6f, 1), new BlendShapeTarget(BlendShapeNames.EyeLookInLeft, 25), new BlendShapeTarget(BlendShapeNames.EyeLookOutRight, 25)),
                    Variant("Up", new Vector2(.6f, 1), new BlendShapeTarget(BlendShapeNames.EyeLookUpLeft, 25), new BlendShapeTarget(BlendShapeNames.EyeLookUpRight, 25)),
                    Variant("Down", new Vector2(.6f, 1), new BlendShapeTarget(BlendShapeNames.EyeLookDownLeft, 25), new BlendShapeTarget(BlendShapeNames.EyeLookDownRight, 25)) } }
        };
    }

}
