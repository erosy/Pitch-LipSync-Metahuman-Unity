using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Transfers an animation driver's bind-relative motion to separate skin rigs.
/// Keeps each destination's authored rest offset, axes and unit scale.</summary>
[ExecuteAlways, DefaultExecutionOrder(900), DisallowMultipleComponent]
public sealed class CharacterSkeletonPoseBridge : MonoBehaviour
{
    public enum TransferMode { WorldPose, RotationOnly, FollowParent }

    [Serializable]
    public sealed class Binding
    {
        public Transform source, target;
        public Matrix4x4 sourceBindInverse, targetBind;
        public TransferMode mode;
        public Vector3 targetLocalBindPosition;
        public Quaternion targetLocalBindRotation = Quaternion.identity;
    }
    [SerializeField] Transform characterSpace;
    [SerializeField] Binding[] bindings = Array.Empty<Binding>();
    public IReadOnlyList<Binding> Bindings => bindings;

    public void Configure(Transform space, Binding[] pairs)
    {
        characterSpace = space;
        bindings = pairs;
        foreach (var pair in bindings)
        {
            pair.sourceBindInverse = (space.worldToLocalMatrix * pair.source.localToWorldMatrix).inverse;
            pair.targetBind = space.worldToLocalMatrix * pair.target.localToWorldMatrix;
            pair.targetLocalBindPosition = pair.target.localPosition;
            pair.targetLocalBindRotation = pair.target.localRotation;
        }
    }

    Sandbox.CharacterSystem.AnimationModule previewModule;
    bool wasPreviewing;
    void LateUpdate()
    {
        if (!previewModule) previewModule = GetComponentInChildren<Sandbox.CharacterSystem.AnimationModule>(true);
        bool previewing = previewModule && previewModule.IsPreviewing;
        if (Application.isPlaying || previewing || wasPreviewing) ApplyPose();
        wasPreviewing = previewing;
    }

    // Explicitly callable by disposable Edit Mode animation verification.
    public void ApplyPose()
    {
        if (!characterSpace) return;
        var world = characterSpace.localToWorldMatrix;
        var inverse = characterSpace.worldToLocalMatrix;
        foreach (var pair in bindings)
        {
            if (!pair.source || !pair.target) continue;
            // Finger correctives have no equivalent on the animation driver.
            // Keep them attached to their authored joint instead of independently
            // pulling weighted vertices towards a guessed source bone.
            if (pair.mode == TransferMode.FollowParent)
            {
                pair.target.SetLocalPositionAndRotation(pair.targetLocalBindPosition, pair.targetLocalBindRotation);
                continue;
            }
            var pose = world * inverse * pair.source.localToWorldMatrix
                * pair.sourceBindInverse * pair.targetBind;
            if (pair.mode == TransferMode.RotationOnly)
            {
                // Different hand proportions must not translate each phalanx
                // independently. Parents are evaluated before their children.
                pair.target.localPosition = pair.targetLocalBindPosition;
                pair.target.rotation = pose.rotation;
            }
            else pair.target.SetPositionAndRotation(pose.GetColumn(3), pose.rotation);
        }
    }
}
