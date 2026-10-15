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

