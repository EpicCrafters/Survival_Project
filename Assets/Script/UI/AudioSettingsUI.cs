using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AudioSettingsUI : MonoBehaviour
{
    [Header("UI References")]
    public Slider masterVolumeSlider;
    public Slider musicVolumeSlider;
    public Slider sfxVolumeSlider;
    public Toggle muteToggle;

    public TMP_Text masterValueText;
    public TMP_Text musicValueText;
    public TMP_Text sfxValueText;

    private AudioController controller;

    void OnEnable()
    {
        if (SettingsManager.Instance != null)
        {
            controller = SettingsManager.Instance.Audio;
            InitializeUI();
            LoadCurrentSettings();
        }
    }

    void Start()
    {
        if (controller == null)
        {
            controller = SettingsManager.Instance.Audio;
            InitializeUI();
            LoadCurrentSettings();
        }
    }

    private void InitializeUI()
    {
        // Xóa listeners cũ
        masterVolumeSlider.onValueChanged.RemoveAllListeners();
        musicVolumeSlider.onValueChanged.RemoveAllListeners();
        sfxVolumeSlider.onValueChanged.RemoveAllListeners();
        muteToggle.onValueChanged.RemoveAllListeners();

        // Add listeners mới
        masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
        musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
        sfxVolumeSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
        muteToggle.onValueChanged.AddListener(OnMuteChanged);
    }

    public void LoadCurrentSettings()
    {
        var data = controller.GetData();

        masterVolumeSlider.SetValueWithoutNotify(data.masterVolume);
        musicVolumeSlider.SetValueWithoutNotify(data.musicVolume);
        sfxVolumeSlider.SetValueWithoutNotify(data.sfxVolume);
        muteToggle.SetIsOnWithoutNotify(data.muted);

        UpdateVolumeText(masterValueText, data.masterVolume);
        UpdateVolumeText(musicValueText, data.musicVolume);
        UpdateVolumeText(sfxValueText, data.sfxVolume);
    }

    private void OnMasterVolumeChanged(float value)
    {
        controller.SetMasterVolume(value);
        UpdateVolumeText(masterValueText, value);
    }

    private void OnMusicVolumeChanged(float value)
    {
        controller.SetMusicVolume(value);
        UpdateVolumeText(musicValueText, value);
    }

    private void OnSFXVolumeChanged(float value)
    {
        controller.SetSFXVolume(value);
        UpdateVolumeText(sfxValueText, value);
    }

    private void OnMuteChanged(bool value)
    {
        controller.SetMuted(value);
    }

    private void UpdateVolumeText(TMP_Text text, float value)
    {
        text.text = $"{Mathf.RoundToInt(value * 100)}%";
    }
}