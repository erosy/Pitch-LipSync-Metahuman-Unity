using TMPro;
using UnityEngine;

/// <summary>
/// Prototype: plays a voice clip when a UI button is pressed and lets uLipSync drive the character's mouth.
/// uLipSync analyses whatever the AudioSource plays, so this only swaps the clip and the matching voice profile.
/// </summary>
public class VoiceLipSyncDemo : MonoBehaviour
{
    [System.Serializable]
    public class Voice
    {
        public string label;
        public AudioClip clip;
        [Tooltip("uLipSync calibration that matches this speaker (male/female sample or a custom one).")]
        public uLipSync.Profile profile;
    }

    [SerializeField] AudioSource audioSource;
    [SerializeField] uLipSync.uLipSync lipSync;
    [SerializeField] Voice boy = new Voice { label = "Ethan" };
    [SerializeField] Voice girl = new Voice { label = "Liora" };
    [SerializeField] TMP_Text statusText;

    public void PlayBoy() => Play(boy);
    public void PlayGirl() => Play(girl);

    void Play(Voice voice)
    {
        if (voice.clip == null)
        {
            Debug.LogWarning($"{name}: no clip assigned for {voice.label}");
            return;
        }

        audioSource.Stop();
        if (voice.profile != null) lipSync.profile = voice.profile;
        audioSource.clip = voice.clip;
        audioSource.Play();
        SetStatus($"Playing: {voice.label}");
    }

    void Update()
    {
        if (statusText != null && !audioSource.isPlaying && statusText.text.Length > 0) SetStatus("");
    }

    void SetStatus(string text)
    {
        if (statusText != null) statusText.text = text;
    }
}
