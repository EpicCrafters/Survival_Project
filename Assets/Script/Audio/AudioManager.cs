using UnityEngine;
using UnityEngine.Audio;
using System;
using System.Collections;
using System.Collections.Generic;

public enum SoundType
{
    PlayerFootstep,
    //Jump,
    Collect,
    UI_Click,
    //UI_Hover,
    EnemyHit,
    EnemySlashed,
    ArrowRelease,
    Hit,
    Open,
    Close,
    Fire,
    Chop,
    Swing,
    CraftingDone
}

[System.Serializable]
public struct SoundList
{
    [HideInInspector] public string name;
    [Range(0, 1)] public float volume;
    public AudioMixerGroup mixer;
    public AudioClip[] sounds;
}

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [Header("Audio Sources")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource musicSource2; // For crossfading
    [SerializeField] private AudioSource ambientSource;
    [SerializeField] private AudioSource ambientSource2; // For crossfading
    [SerializeField] private int sfxPoolSize = 10;

    [Header("SFX Configuration")]
    [SerializeField] private SoundsSO soundEffectsSO;

    private Queue<AudioSource> sfxPool = new Queue<AudioSource>();
    private Coroutine currentMusicFade;
    private Coroutine currentAmbientFade;
    private bool usingPrimaryMusicSource = true;
    private bool usingPrimaryAmbientSource = true;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Initialize();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Initialize()
    {
        // Create SFX pool
        for (int i = 0; i < sfxPoolSize; i++)
        {
            GameObject go = new GameObject($"SFX_{i}");
            go.transform.parent = transform;
            AudioSource source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            sfxPool.Enqueue(source);
        }
    }

    // ========== MUSIC METHODS ==========
    public void PlayMenuMusicWithFade()
    {
        PlayMusic(musicSource.clip, true, 2f, 0.8f);
    }

    public void PlayGameAmbientWithFade()
    {
        PlayAmbient(ambientSource.clip, 3f, 0.6f);
    }


    // ========== SFX METHODS ==========
    public void PlaySFX(SoundType soundType, Vector3 position = default, float volumeMultiplier = 1f, float pitch = 1f, bool is3D = false)
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

        AudioClip randomClip = clips[UnityEngine.Random.Range(0, clips.Length)];

        AudioSource source = sfxPool.Dequeue();
        sfxPool.Enqueue(source);

        source.clip = randomClip;
        source.outputAudioMixerGroup = soundList.mixer;
        source.volume = soundList.volume * volumeMultiplier;
        source.pitch = pitch;
        source.spatialBlend = is3D ? 1f : 0f;
        source.loop = false;

        if (position != default)
            source.transform.position = position;

        source.Play();
    }

    public void PlayUISFX(SoundType soundType, float volumeMultiplier = 1f)
    {
        PlaySFX(soundType, default, volumeMultiplier, 1f, false);
    }

    // ========== MUSIC METHODS ==========
    public void PlayMusic(AudioClip clip, bool loop = true, float fadeTime = 1f, float targetVolume = 1f)
    {
        if (currentMusicFade != null)
            StopCoroutine(currentMusicFade);

        currentMusicFade = StartCoroutine(FadeMusicCoroutine(clip, loop, fadeTime, targetVolume));
    }

    private IEnumerator FadeMusicCoroutine(AudioClip newClip, bool loop, float fadeTime, float targetVolume)
    {
        AudioSource oldSource = usingPrimaryMusicSource ? musicSource : musicSource2;
        AudioSource newSource = usingPrimaryMusicSource ? musicSource2 : musicSource;

        // Set up new source
        newSource.clip = newClip;
        newSource.loop = loop;
        newSource.volume = 0f;
        newSource.Play();

        float timer = 0f;

        // Crossfade between sources
        while (timer <= fadeTime)
        {
            timer += Time.deltaTime;
            float progress = timer / fadeTime;

            // Fade out old source
            oldSource.volume = Mathf.Lerp(targetVolume, 0f, progress);

            // Fade in new source
            newSource.volume = Mathf.Lerp(0f, targetVolume, progress);

            yield return null;
        }

        // Ensure final volumes
        oldSource.volume = 0f;
        newSource.volume = targetVolume;

        // Stop old source if completely faded
        if (oldSource.volume <= 0.01f)
        {
            oldSource.Stop();
        }

        // Switch active source
        usingPrimaryMusicSource = !usingPrimaryMusicSource;
    }

    // ========== AMBIENT METHODS ==========
    public void PlayAmbient(AudioClip clip, float fadeTime = 2f, float targetVolume = 0.6f)
    {
        if (currentAmbientFade != null)
            StopCoroutine(currentAmbientFade);

        currentAmbientFade = StartCoroutine(FadeAmbientCoroutine(clip, fadeTime, targetVolume));
    }

    private IEnumerator FadeAmbientCoroutine(AudioClip newClip, float fadeTime, float targetVolume)
    {
        AudioSource oldSource = usingPrimaryAmbientSource ? ambientSource : ambientSource2;
        AudioSource newSource = usingPrimaryAmbientSource ? ambientSource2 : ambientSource;

        // Set up new source
        newSource.clip = newClip;
        newSource.volume = 0f;
        newSource.Play();

        float timer = 0f;

        // Crossfade between ambient sources
        while (timer <= fadeTime)
        {
            timer += Time.deltaTime;
            float progress = timer / fadeTime;

            // Fade out old source
            oldSource.volume = Mathf.Lerp(targetVolume, 0f, progress);

            // Fade in new source
            newSource.volume = Mathf.Lerp(0f, targetVolume, progress);

            yield return null;
        }

        // Ensure final volumes
        oldSource.volume = 0f;
        newSource.volume = targetVolume;

        // Stop old source if completely faded
        if (oldSource.volume <= 0.01f)
        {
            oldSource.Stop();
        }

        // Switch active ambient source
        usingPrimaryAmbientSource = !usingPrimaryAmbientSource;
    }

    // ========== STOP METHODS ==========
    public void StopMusic(float fadeTime = 1f)
    {
        StartCoroutine(FadeOutCoroutine(usingPrimaryMusicSource ? musicSource : musicSource2, fadeTime));
    }

    public void StopAmbient(float fadeTime = 2f)
    {
        StartCoroutine(FadeOutCoroutine(usingPrimaryAmbientSource ? ambientSource : ambientSource2, fadeTime));
    }

    private IEnumerator FadeOutCoroutine(AudioSource source, float fadeTime)
    {
        float startVolume = source.volume;
        float timer = 0f;

        while (timer <= fadeTime)
        {
            timer += Time.deltaTime;
            float progress = timer / fadeTime;
            source.volume = Mathf.Lerp(startVolume, 0f, progress);
            yield return null;
        }

        source.Stop();
        source.volume = startVolume; // Reset volume for next use
    }

    // ========== PAUSE/RESUME METHODS ==========
    public void PauseMusicWithFade(float fadeTime = 0.5f)
    {
        StartCoroutine(FadeToPause(usingPrimaryMusicSource ? musicSource : musicSource2, fadeTime));
    }

    public void ResumeMusicWithFade(float fadeTime = 0.5f, float targetVolume = 1f)
    {
        StartCoroutine(FadeInFromPause(usingPrimaryMusicSource ? musicSource : musicSource2, fadeTime, targetVolume));
    }
    // ========== AMBIENT PAUSE/RESUME METHODS ==========
    public void PauseAmbientWithFade(float fadeTime = 0.5f)
    {
        StartCoroutine(FadeToPause(usingPrimaryAmbientSource ? ambientSource : ambientSource2, fadeTime));
    }

    public void ResumeAmbientWithFade(float fadeTime = 0.5f, float targetVolume = 0.6f)
    {
        StartCoroutine(FadeInFromPause(usingPrimaryAmbientSource ? ambientSource : ambientSource2, fadeTime, targetVolume));
    }

    private IEnumerator FadeToPause(AudioSource source, float fadeTime)
    {
        float startVolume = source.volume;
        float timer = 0f;

        while (timer <= fadeTime)
        {
            timer += Time.deltaTime;
            float progress = timer / fadeTime;
            source.volume = Mathf.Lerp(startVolume, 0f, progress);
            yield return null;
        }

        source.Pause();
        source.volume = startVolume; // Reset for when we resume
    }

    private IEnumerator FadeInFromPause(AudioSource source, float fadeTime, float targetVolume)
    {
        source.UnPause();
        float currentVolume = source.volume;
        float timer = 0f;

        while (timer <= fadeTime)
        {
            timer += Time.deltaTime;
            float progress = timer / fadeTime;
            source.volume = Mathf.Lerp(currentVolume, targetVolume, progress);
            yield return null;
        }

        source.volume = targetVolume;
    }

    // ========== VOLUME CONTROL METHODS ==========
    public void SetMusicVolume(float volume, float fadeTime = 0.5f)
    {
        StartCoroutine(AdjustVolumeCoroutine(
            usingPrimaryMusicSource ? musicSource : musicSource2,
            volume,
            fadeTime
        ));
    }

    public void SetAmbientVolume(float volume, float fadeTime = 0.5f)
    {
        StartCoroutine(AdjustVolumeCoroutine(
            usingPrimaryAmbientSource ? ambientSource : ambientSource2,
            volume,
            fadeTime
        ));
    }

    public void SetMasterVolume(float volume)
    {
        AudioListener.volume = Mathf.Clamp01(volume);
    }

    private IEnumerator AdjustVolumeCoroutine(AudioSource source, float targetVolume, float fadeTime)
    {
        float startVolume = source.volume;
        float timer = 0f;

        while (timer <= fadeTime)
        {
            timer += Time.deltaTime;
            float progress = timer / fadeTime;
            source.volume = Mathf.Lerp(startVolume, Mathf.Clamp01(targetVolume), progress);
            yield return null;
        }

        source.volume = Mathf.Clamp01(targetVolume);
    }

    // ========== UTILITY METHODS ==========
    public bool IsMusicPlaying()
    {
        return usingPrimaryMusicSource ? musicSource.isPlaying : musicSource2.isPlaying;
    }

    public bool IsAmbientPlaying()
    {
        return usingPrimaryAmbientSource ? ambientSource.isPlaying : ambientSource2.isPlaying;
    }

    public float GetMusicVolume()
    {
        return usingPrimaryMusicSource ? musicSource.volume : musicSource2.volume;
    }

    public float GetAmbientVolume()
    {
        return usingPrimaryAmbientSource ? ambientSource.volume : ambientSource2.volume;
    }
    public void PlayButtonSound(SoundType soundType)
    {
        PlayUISFX(soundType, 1f);
    }
    /*
    // ========== EDITOR HELPERS ==========
#if UNITY_EDITOR
    private void OnValidate()
    {
        if (soundEffectsSO != null && soundEffectsSO.sounds != null)
        {
            for (int i = 0; i < soundEffectsSO.sounds.Length; i++)
            {
                soundEffectsSO.sounds[i].name = ((SoundType)i).ToString();
            }
        }
    }
#endif*/
}