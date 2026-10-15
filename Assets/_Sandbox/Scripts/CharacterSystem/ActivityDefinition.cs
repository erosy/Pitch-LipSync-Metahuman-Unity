using System.Collections.Generic;
using UnityEngine;

namespace Sandbox.CharacterSystem
{
    [CreateAssetMenu(fileName = "Activity", menuName = "Character System/Activity Definition")]
    public sealed class ActivityDefinition : CharacterDefinition
    {
        [SerializeField, Tooltip("Optional. An empty animation resets the body to the character's default idle animation.")]
        AnimationDefinition animation;
        [SerializeField, Tooltip("Optional. An empty expression resets the face to neutral.")]
        ExpressionDefinition expression;
        [SerializeField, Tooltip("Allows idle facial motions when speech and expression-transition gates also permit them.")]
        bool isIdle;

        public AnimationDefinition Animation => animation;
        public ExpressionDefinition Expression => expression;
        public bool IsIdle => isIdle;

        protected override void CollectValidationErrors(List<string> errors)
        {
            base.CollectValidationErrors(errors);
            if (animation)
                foreach (var error in animation.ValidateConfiguration()) errors.Add("Animation: " + error);
            if (expression)
                foreach (var error in expression.ValidateConfiguration()) errors.Add("Expression: " + error);
        }
    }
}
