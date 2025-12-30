using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GraphicsSettingsUI : MonoBehaviour
{
    [Header("UI References")]
    public TMP_Dropdown qualityDropdown;
    public TMP_Dropdown resolutionDropdown;
    public Toggle fullscreenToggle;
    public Toggle vsyncToggle;
    public Slider brightnessSlider;
    public TMP_Text brightnessValueText;

    private GraphicsController controller;

    void OnEnable()
    {
        if (SettingsManager.Instance != null)
        {
            controller = SettingsManager.Instance.Graphics;
            InitializeUI();
            LoadCurrentSettings();
        }
    }

    void Start()
    {
        if (controller == null)
        {
            controller = SettingsManager.Instance.Graphics;
            InitializeUI();
            LoadCurrentSettings();
        }
    }

    private void InitializeUI()
    {
        // Populate quality dropdown
        qualityDropdown.ClearOptions();
        qualityDropdown.AddOptions(new List<string>(QualitySettings.names));

        // Populate resolution dropdown
        resolutionDropdown.ClearOptions();
        var resOptions = new List<string>();
        foreach (var res in Screen.resolutions)
        {
            resOptions.Add($"{res.width} x {res.height} @ {res.refreshRate}Hz");
        }
        resolutionDropdown.AddOptions(resOptions);

        // Xóa listeners cũ để tránh duplicate
        qualityDropdown.onValueChanged.RemoveAllListeners();
        resolutionDropdown.onValueChanged.RemoveAllListeners();
        fullscreenToggle.onValueChanged.RemoveAllListeners();
        vsyncToggle.onValueChanged.RemoveAllListeners();
        brightnessSlider.onValueChanged.RemoveAllListeners();

        // Add listeners mới
        qualityDropdown.onValueChanged.AddListener(OnQualityChanged);
        resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
        fullscreenToggle.onValueChanged.AddListener(OnFullscreenChanged);
        vsyncToggle.onValueChanged.AddListener(OnVSyncChanged);
        brightnessSlider.onValueChanged.AddListener(OnBrightnessChanged);
    }

    public void LoadCurrentSettings()
    {
        var data = controller.GetData();

        qualityDropdown.SetValueWithoutNotify(data.qualityLevel);
        resolutionDropdown.SetValueWithoutNotify(data.resolutionIndex);
        fullscreenToggle.SetIsOnWithoutNotify(data.fullscreen);
        vsyncToggle.SetIsOnWithoutNotify(data.vsync);
        brightnessSlider.SetValueWithoutNotify(data.brightness);
        UpdateBrightnessText(data.brightness);
    }

    private void OnQualityChanged(int value)
    {
        controller.SetQualityLevel(value);
    }

    private void OnResolutionChanged(int value)
    {
        controller.SetResolution(value);
    }

    private void OnFullscreenChanged(bool value)
    {
        controller.SetFullscreen(value);
    }

    private void OnVSyncChanged(bool value)
    {
        controller.SetVSync(value);
    }

    private void OnBrightnessChanged(float value)
    {
        controller.SetBrightness(value);
        UpdateBrightnessText(value);
    }

    private void UpdateBrightnessText(float value)
    {
        brightnessValueText.text = $"{Mathf.RoundToInt(value * 100)}%";
    }
}