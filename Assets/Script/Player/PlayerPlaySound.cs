using UnityEngine;

public class PlayerPlaySound : MonoBehaviour
{
    [Header("Audio Source Setup")]
    [SerializeField] private AudioSource audioSource;

    [Header("Sound Configuration")]
    [SerializeField] private SoundsSO soundEffectsSO;

    [Header("Audio Settings")]
    [SerializeField] private float minDistance = 1f;
    [SerializeField] private float maxDistance = 20f;
    [SerializeField] private AudioRolloffMode rolloffMode = AudioRolloffMode.Linear;

    private void Awake()
    {
        // Create audio source if not assigned
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        SetupWorldSpaceAudio();
    }

    private void SetupWorldSpaceAudio()
    {
        // Configure for 3D world space audio
        audioSource.spatialBlend = 1f; // Full 3D
        audioSource.rolloffMode = rolloffMode;
        audioSource.minDistance = minDistance;
        audioSource.maxDistance = maxDistance;
        audioSource.playOnAwake = false;
        audioSource.loop = false;
    }

    public void PlayFootStep()
    {
        PlaySound(SoundType.PlayerFootstep);
    }


    public void PlayArrowRelease()
    {
        PlaySound(SoundType.ArrowRelease);
    }

    public void PlayHit()
    {
        PlaySound(SoundType.Hit);
    }

    public void PlayArrowCharge()
    {
        PlaySound(SoundType.BowCharge);
    }

    public void PlayChop()
    {
        PlaySound(SoundType.Chop);
    }

    public void PlaySwing()
    {
        PlaySound(SoundType.Swing);
    }

    private void PlaySound(SoundType soundType)
    {
        if (soundEffectsSO == null || soundEffectsSO.sounds.Length <= (int)soundType)
        {
            Debug.LogWarning($"Sound type '{soundType}' not configured!");
            return;
        }

        SoundList soundList = soundEffectsSO.sounds[(int)soundType];
        AudioClip[] clips = soundList.sounds;

        if (clips == null || clips.Length == 0)
        {
            Debug.LogWarning($"No clips found for sound type '{soundType}'!");
            return;
        }

        // Get random clip from array
        AudioClip randomClip = clips[Random.Range(0, clips.Length)];

        // Apply settings
        audioSource.outputAudioMixerGroup = soundList.mixer;
        audioSource.volume = soundList.volume;
        audioSource.pitch = Random.Range(0.95f, 1.05f); // Slight pitch variation for variety

        // Play the sound
        audioSource.PlayOneShot(randomClip);
    }

    // Optional: Play with custom parameters
    public void PlaySoundWithParams(SoundType soundType, float volumeMultiplier = 1f, float pitch = 1f)
    {
        if (soundEffectsSO == null || soundEffectsSO.sounds.Length <= (int)soundType)
        {
            Debug.LogWarning($"Sound type '{soundType}' not configured!");
            return;
        }

        SoundList soundList = soundEffectsSO.sounds[(int)soundType];
        AudioClip[] clips = soundList.sounds;

        if (clips == null || clips.Length == 0)
        {
            Debug.LogWarning($"No clips found for sound type '{soundType}'!");
            return;
        }

        AudioClip randomClip = clips[Random.Range(0, clips.Length)];

        audioSource.outputAudioMixerGroup = soundList.mixer;
        audioSource.pitch = pitch;

        // PlayOneShot with volume parameter
        audioSource.PlayOneShot(randomClip, soundList.volume * volumeMultiplier);
    }

    // Utility method to stop all sounds
    public void StopAllSounds()
    {
        audioSource.Stop();
    }
}