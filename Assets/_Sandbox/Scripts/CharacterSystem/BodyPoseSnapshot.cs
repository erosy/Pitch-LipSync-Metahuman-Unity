using UnityEngine;

namespace Sandbox.CharacterSystem
{
    /// <summary>Restores transforms only; facial weights belong to the expression/lip-sync writers.</summary>
    internal sealed class BodyPoseSnapshot
    {
        struct Pose
        {
            internal Transform Transform;
            internal Vector3 Position, Scale;
            internal Quaternion Rotation;
        }
        readonly Pose[] poses;

        internal BodyPoseSnapshot(Transform root)
        {
            var transforms = root.GetComponentsInChildren<Transform>(true);
            poses = new Pose[transforms.Length];
            for (int i = 0; i < transforms.Length; i++)
            {
                var target = transforms[i];
                poses[i] = new Pose { Transform = target, Position = target.localPosition,
                    Rotation = target.localRotation, Scale = target.localScale };
            }
        }

        internal void Restore()
        {
            foreach (var pose in poses)
                if (pose.Transform)
                {
                    pose.Transform.localPosition = pose.Position;
                    pose.Transform.localRotation = pose.Rotation;
                    pose.Transform.localScale = pose.Scale;
                }
        }
    }

    /// <summary>Temporarily gives the module sole Animator ownership, restoring original settings afterwards.</summary>
    internal sealed class BodyAnimatorBinding
    {
        internal Animator Animator { get; }
        readonly RuntimeAnimatorController controller;
        readonly bool rootMotion, fireEvents;
        readonly AnimatorCullingMode culling;
        readonly BodyPoseSnapshot pose;

        internal BodyAnimatorBinding(Animator animator)
        {
            Animator = animator;
            pose = new BodyPoseSnapshot(animator.transform);
            controller = animator.runtimeAnimatorController;
            rootMotion = animator.applyRootMotion;
            fireEvents = animator.fireEvents;
            culling = animator.cullingMode;
            animator.runtimeAnimatorController = null;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }

        internal void Restore()
        {
            if (Animator)
            {
                Animator.runtimeAnimatorController = controller;
                Animator.applyRootMotion = rootMotion;
                Animator.fireEvents = fireEvents;
                Animator.cullingMode = culling;
            }
            pose.Restore();
        }
    }
}
