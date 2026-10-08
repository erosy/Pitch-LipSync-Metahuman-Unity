using TMPro;
using UnityEngine;

/// <summary>
/// Prototype: plays a voice clip when a UI button is pressed and lets uLipSync drive the character's mouth.
/// Each voice belongs to its own character; pressing a button shows that character, hides the others, and plays
/// the clip through the character's own AudioSource so its uLipSync drives its face.
/// </summary>
public class VoiceLipSyncDemo : MonoBehaviour
{
    [System.Serializable]
    public class Voice
    {
        public string label;
        [Tooltip("Character root that is shown while this voice is selected and hidden otherwise.")]
        public GameObject character;
        [Tooltip("AudioSource on the character that uLipSync analyses.")]
        public AudioSource audioSource;
        public uLipSync.uLipSync lipSync;
        public AudioClip clip;
        [Tooltip("uLipSync calibration that matches this speaker (male/female sample or a custom one).")]
        public uLipSync.Profile profile;
    }

    [SerializeField] Voice boy = new Voice { label = "Ethan" };
    [SerializeField] Voice girl = new Voice { label = "Liora" };
    [SerializeField] TMP_Text statusText;

    Voice current;

    public void PlayBoy() => Play(boy, girl);
    public void PlayGirl() => Play(girl, boy);

    void Play(Voice voice, Voice other)
    {
        if (voice.clip == null)
        {
            Debug.LogWarning($"{name}: no clip assigned for {voice.label}");
            return;
        }

        if (other.audioSource != null) other.audioSource.Stop();
        if (other.character != null) other.character.SetActive(false);
        if (voice.character != null) voice.character.SetActive(true);

        voice.audioSource.Stop();
        if (voice.profile != null) voice.lipSync.profile = voice.profile;
        voice.audioSource.clip = voice.clip;
        voice.audioSource.Play();
        current = voice;
        SetStatus($"Playing: {voice.label}");
    }

    void Update()
    {
        if (statusText != null && statusText.text.Length > 0 && (current == null || !current.audioSource.isPlaying)) SetStatus("");
    }

    void SetStatus(string text)
    {
        if (statusText != null) statusText.text = text;
    }
}
