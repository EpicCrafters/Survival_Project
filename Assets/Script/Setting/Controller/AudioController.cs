using UnityEngine;
using UnityEngine.InputSystem;
using System;

public class AudioController
{
    private AudioSettingsData data;
    public event Action OnSettingsChanged;

    public AudioController(AudioSettingsData data)
    {
        this.data = data;
    }

    public void SetMasterVolume(float volume)
    {
        data.masterVolume = Mathf.Clamp01(volume);

        // Áp dụng vào AudioManager nếu có
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetMasterVolume(data.muted ? 0 : data.masterVolume);
        }

        OnSettingsChanged?.Invoke();
    }

    public void SetMusicVolume(float volume)
    {
        data.musicVolume = Mathf.Clamp01(volume);

        // Áp dụng vào AudioManager
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetMusicVolume(data.musicVolume);
        }

        OnSettingsChanged?.Invoke();
    }

    public void SetSFXVolume(float volume)
    {
        data.sfxVolume = Mathf.Clamp01(volume);
        // Có thể áp dụng vào Audio Mixer Group cho SFX
        OnSettingsChanged?.Invoke();
    }

    public void SetMuted(bool muted)
    {
        data.muted = muted;

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetMasterVolume(muted ? 0 : data.masterVolume);
        }

        OnSettingsChanged?.Invoke();
    }

    public void ApplySettings()
    {
        SetMasterVolume(data.masterVolume);
        SetMusicVolume(data.musicVolume);
        SetSFXVolume(data.sfxVolume);
        SetMuted(data.muted);
    }

    public AudioSettingsData GetData() => data;
}