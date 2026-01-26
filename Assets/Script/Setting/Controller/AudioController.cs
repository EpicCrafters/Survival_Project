using UnityEngine;
using System;

public class AudioController
{
    private AudioSettingsData data;
    public event Action OnSettingsChanged;

    public AudioController(AudioSettingsData data)
    {
        this.data = data;
    }

    // ONLY store data, DON'T apply immediately
    public void SetMasterVolume(float volume)
    {
        data.masterVolume = Mathf.Clamp01(volume);
        OnSettingsChanged?.Invoke();
    }

    public void SetMusicVolume(float volume)
    {
        data.musicVolume = Mathf.Clamp01(volume);
        OnSettingsChanged?.Invoke();
    }

    public void SetSFXVolume(float volume)
    {
        data.sfxVolume = Mathf.Clamp01(volume);
        OnSettingsChanged?.Invoke();
    }

    public void SetMuted(bool muted)
    {
        data.muted = muted;
        OnSettingsChanged?.Invoke();
    }

    // Apply ALL settings at once when called
    public void ApplySettings()
    {
        if (AudioManager.Instance != null)
        {
            // Apply master volume (with mute check)
            AudioManager.Instance.SetMasterVolume(data.muted ? 0 : data.masterVolume);

            // Apply music volume
            AudioManager.Instance.SetMusicVolume(data.musicVolume);

            // Apply SFX volume
          
        }
        else
        {
            // Fallback if AudioManager doesn't exist
            AudioListener.volume = data.muted ? 0 : data.masterVolume;
        }

        Debug.Log($"Audio Applied - Master: {data.masterVolume}, Music: {data.musicVolume}, SFX: {data.sfxVolume}, Muted: {data.muted}");
    }

    public void ResetToDefaults()
    {
        data.masterVolume = 1.0f;
        data.musicVolume = 0.8f;
        data.sfxVolume = 1.0f;
        data.muted = false;
        OnSettingsChanged?.Invoke();
    }

    public AudioSettingsData GetData() => data;
}