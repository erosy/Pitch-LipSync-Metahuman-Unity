using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

// Unity serializes these ordinary data classes. Odin only changes their presentation.
[Serializable]
public class BlendShapeTarget
{
    public string shape;
    [Range(0, 100)] public float weight;
    public BlendShapeTarget() { }
    public BlendShapeTarget(string shape, float weight) { this.shape = shape; this.weight = weight; }
}

[Serializable]
public class BlendShapePose
{
    [ListDrawerSettings(ShowFoldout = true)]
    public List<BlendShapeTarget> targets = new List<BlendShapeTarget>();
    public BlendShapePose() { }
    public BlendShapePose(params BlendShapeTarget[] targets) { this.targets.AddRange(targets); }
}

[Serializable]
public class ExpressionDefinition
{
    [OnInspectorGUI("DrawPlayButton", append: true)] public string name;
    public BlendShapePose silentPose = new BlendShapePose();
    public BlendShapePose speakingPose = new BlendShapePose();
#if UNITY_EDITOR
    // Plays this definition on the ExpressionController(s) being inspected; only available in Play Mode.
    void DrawPlayButton(Sirenix.OdinInspector.Editor.InspectorProperty property)
    {
        using (new UnityEditor.EditorGUI.DisabledScope(!Application.isPlaying || string.IsNullOrEmpty(name)))
            if (GUILayout.Button("Play " + name))
                foreach (var target in property.Tree.WeakTargets)
                    if (target is ExpressionController controller) controller.SetExpression(name);
    }
#endif
}

public enum ActivityEligibility { AllExpressions, NeutralOnly, SelectedExpressions }

[Serializable]
public class ActivityVariant
{
    public string name;
    public BlendShapePose pose = new BlendShapePose();
    [MinMaxSlider(0, 2, true)] public Vector2 strength = Vector2.one;
}

[Serializable]
public class RepeatSettings
{
    [Range(0, 1)] public float probability;
    [ShowIf("HasProbability"), Min(0)] public int maximumAdditionalRepetitions = 1;
    [ShowIf("HasProbability"), MinMaxSlider(0, 2, true)] public Vector2 gap = new Vector2(.12f, .25f);
    bool HasProbability => probability > 0;
}

[Serializable]
public class FacialActivityDefinition
{
    public string name;
    public bool enabled = true;
    public string channel = "Mouth";
    public ActivityEligibility eligibility = ActivityEligibility.NeutralOnly;
    [ShowIf("UsesSelectedExpressions")] public List<string> expressionNames = new List<string>();
    public bool allowSpeech;
    public bool allowExpressionTransitions;
    [MinMaxSlider(0, 30, true)] public Vector2 interval = new Vector2(8, 16);
    [Min(0)] public float easeIn = .25f;
    [MinMaxSlider(0, 5, true)] public Vector2 hold = new Vector2(.25f, .5f);
    [Min(0)] public float easeOut = .35f;
    [ListDrawerSettings(ShowFoldout = true)] public List<ActivityVariant> variants = new List<ActivityVariant>();
    public RepeatSettings repeat = new RepeatSettings();
    bool UsesSelectedExpressions => eligibility == ActivityEligibility.SelectedExpressions;
}

public static class BlendShapeNames
{
    public const string Joy = "emo_joy";
    public const string EyeBlinkLeft = "eyeBlinkLeft", EyeBlinkRight = "eyeBlinkRight";
    public const string MouthPucker = "mouthPucker", MouthPressLeft = "mouthPressLeft", MouthPressRight = "mouthPressRight";
    public const string MouthLeft = "mouthLeft", MouthRight = "mouthRight";
    public const string EyeLookOutLeft = "eyeLookOutLeft", EyeLookInRight = "eyeLookInRight";
    public const string EyeLookInLeft = "eyeLookInLeft", EyeLookOutRight = "eyeLookOutRight";
    public const string EyeLookUpLeft = "eyeLookUpLeft", EyeLookUpRight = "eyeLookUpRight";
    public const string EyeLookDownLeft = "eyeLookDownLeft", EyeLookDownRight = "eyeLookDownRight";
    public const string MouthSmileLeft = "mouthSmileLeft", MouthSmileRight = "mouthSmileRight";
    public const string CheekSquintLeft = "cheekSquintLeft", CheekSquintRight = "cheekSquintRight";
    public const string EyeSquintLeft = "eyeSquintLeft", EyeSquintRight = "eyeSquintRight", BrowInnerUp = "browInnerUp";
}

public static class FacialPresets
{
    public static List<ExpressionDefinition> Expressions() => new List<ExpressionDefinition>
    {
        new ExpressionDefinition { name = "Joy",
            silentPose = new BlendShapePose(new BlendShapeTarget(BlendShapeNames.Joy, 100)),
            speakingPose = new BlendShapePose(
                new BlendShapeTarget(BlendShapeNames.MouthSmileLeft, 55), new BlendShapeTarget(BlendShapeNames.MouthSmileRight, 55),
                new BlendShapeTarget(BlendShapeNames.CheekSquintLeft, 50), new BlendShapeTarget(BlendShapeNames.CheekSquintRight, 50),
                new BlendShapeTarget(BlendShapeNames.EyeSquintLeft, 30), new BlendShapeTarget(BlendShapeNames.EyeSquintRight, 30),
                new BlendShapeTarget(BlendShapeNames.BrowInnerUp, 10)) }
    };

    public static ActivityVariant Variant(string name, Vector2 strength, params BlendShapeTarget[] targets)
        => new ActivityVariant { name = name, strength = strength, pose = new BlendShapePose(targets) };

    public static List<FacialActivityDefinition> Activities() => new List<FacialActivityDefinition>
    {
        new FacialActivityDefinition { name = "Blink", channel = "Blink", eligibility = ActivityEligibility.AllExpressions,
            allowSpeech = true, allowExpressionTransitions = true, interval = new Vector2(2, 6),
            easeIn = .06f, hold = new Vector2(.03f, .03f), easeOut = .12f,
            repeat = new RepeatSettings { probability = .15f, maximumAdditionalRepetitions = 1, gap = new Vector2(.12f, .25f) },
            variants = new List<ActivityVariant> { Variant("Both eyes", Vector2.one,
                new BlendShapeTarget(BlendShapeNames.EyeBlinkLeft, 100), new BlendShapeTarget(BlendShapeNames.EyeBlinkRight, 100)) } },
        new FacialActivityDefinition { name = "Lip purse", channel = "Mouth", variants = new List<ActivityVariant>
            { Variant("Purse", Vector2.one, new BlendShapeTarget(BlendShapeNames.MouthPucker, 15),
                new BlendShapeTarget(BlendShapeNames.MouthPressLeft, 5), new BlendShapeTarget(BlendShapeNames.MouthPressRight, 5)) } },
        new FacialActivityDefinition { name = "Lip twitch", channel = "Mouth", easeIn = .06f,
            hold = new Vector2(.03f, .03f), easeOut = .12f,
            variants = new List<ActivityVariant> {
                Variant("Left", new Vector2(.5f, 1), new BlendShapeTarget(BlendShapeNames.MouthLeft, 10)),
                Variant("Right", new Vector2(.5f, 1), new BlendShapeTarget(BlendShapeNames.MouthRight, 10)) } },
        new FacialActivityDefinition { name = "Gaze", channel = "Gaze", interval = new Vector2(4, 9),
            easeIn = .18f, hold = new Vector2(.8f, 1.8f), easeOut = .25f,
            variants = new List<ActivityVariant> {
                Variant("Character left", new Vector2(.6f, 1), new BlendShapeTarget(BlendShapeNames.EyeLookOutLeft, 25), new BlendShapeTarget(BlendShapeNames.EyeLookInRight, 25)),
                Variant("Character right", new Vector2(.6f, 1), new BlendShapeTarget(BlendShapeNames.EyeLookInLeft, 25), new BlendShapeTarget(BlendShapeNames.EyeLookOutRight, 25)),
                Variant("Up", new Vector2(.6f, 1), new BlendShapeTarget(BlendShapeNames.EyeLookUpLeft, 25), new BlendShapeTarget(BlendShapeNames.EyeLookUpRight, 25)),
                Variant("Down", new Vector2(.6f, 1), new BlendShapeTarget(BlendShapeNames.EyeLookDownLeft, 25), new BlendShapeTarget(BlendShapeNames.EyeLookDownRight, 25)) } }
    };
}
