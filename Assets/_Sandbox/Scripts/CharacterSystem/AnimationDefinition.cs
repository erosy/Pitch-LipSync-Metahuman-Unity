using System.Collections.Generic;
using UnityEngine;

namespace Sandbox.CharacterSystem
{
    [CreateAssetMenu(fileName = "Animation", menuName = "Character System/Animation Definition")]
    public sealed class AnimationDefinition : CharacterDefinition
    {
        [SerializeField] AnimationClip clip;
        [SerializeField, Tooltip("Overrides the source clip's loop setting during module playback.")]
        bool loop;
        [SerializeField, Min(.01f)] float playbackSpeed = 1f;
        [SerializeField, Min(0)] float crossfadeDuration = .2f;

        public AnimationClip Clip => clip;
        public bool Loop => loop;
        public float PlaybackSpeed => playbackSpeed;
        public float CrossfadeDuration => crossfadeDuration;

        protected override void CollectValidationErrors(List<string> errors)
        {
            base.CollectValidationErrors(errors);
            if (!clip) errors.Add("Assign an animation Clip.");
            else
            {
                if (clip.legacy) errors.Add("Clip must use Mecanim animation, not Legacy animation.");
                if (!IsFinite(clip.length) || clip.length <= 0) errors.Add("Clip must have a positive, finite duration.");
#if UNITY_EDITOR
                foreach (var binding in UnityEditor.AnimationUtility.GetCurveBindings(clip))
                    if (binding.type != typeof(Transform) && binding.type != typeof(Animator))
                    {
                        errors.Add("Body clips may animate only Transform and Animator properties; facial or component curves belong to other modules.");
                        break;
                    }
                if (UnityEditor.AnimationUtility.GetObjectReferenceCurveBindings(clip).Length > 0)
                    errors.Add("Body clips must not contain object-reference curves.");
#endif
            }
            if (!IsFinite(playbackSpeed) || playbackSpeed <= 0)
                errors.Add("Playback Speed must be finite and greater than zero.");
            if (!IsFinite(crossfadeDuration) || crossfadeDuration < 0)
                errors.Add("Crossfade Duration must be finite and non-negative.");
        }
    }
}
