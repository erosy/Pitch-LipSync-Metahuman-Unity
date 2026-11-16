using UnityEngine;

namespace Sandbox.CharacterSystem
{
    /// <summary>Selects a character; activation greets, repeated selection requests a response.</summary>
    [DisallowMultipleComponent, AddComponentMenu("Character System/Character Selection")]
    public sealed class CharacterSelectionController : MonoBehaviour
    {
        [SerializeField] CharacterFlowController boy;
        [SerializeField] CharacterFlowController girl;

        CharacterFlowController selected;

        void Awake()
        {
            selected = boy && boy.gameObject.activeSelf ? boy
                : girl && girl.gameObject.activeSelf ? girl : null;
        }

        public void SelectBoy() => Select(boy, girl);
        public void SelectGirl() => Select(girl, boy);

        void Select(CharacterFlowController target, CharacterFlowController other)
        {
            if (!Application.IsPlaying(gameObject)) return;
            if (!target || !other || target == other || target.transform.root == other.transform.root)
            {
                Debug.LogWarning("Character Selection: assign two separate character roots and their flow controllers.", this);
                return;
            }

            if (selected == target && target.gameObject.activeInHierarchy)
            {
                // The flow owns readiness checks, including ignoring loading-time clicks.
                target.PlayResponse();
                return;
            }

            // OnDisable cancels the previous character's flow, speech and warmup.
            other.gameObject.SetActive(false);
            selected = target;
            target.gameObject.SetActive(true);
            // Its normal activation lifecycle waits for readiness and greets.
        }
    }
}
