using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GraphicsSettingsUI : MonoBehaviour
{
    [Header("UI References")]
    public TMP_Dropdown qualityDropdown;
    public TMP_Dropdown resolutionDropdown;
    public TMP_Dropdown screenModeDropdown;  // Fullscreen, Windowed, Borderless
    public TMP_Dropdown framerateDropdown;   // 60, 90, 144, Unlimited
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
            resOptions.Add($"{res.width} x {res.height}");
        }
        resolutionDropdown.AddOptions(resOptions);

        // Populate screen mode dropdown
        screenModeDropdown.ClearOptions();
        screenModeDropdown.AddOptions(new List<string>
        {
            "Fullscreen",
            "Windowed",
            "Borderless"
        });

        // Populate framerate dropdown
        framerateDropdown.ClearOptions();
        framerateDropdown.AddOptions(new List<string>
        {
            "60 FPS",
            "90 FPS",
            "144 FPS",
            "Unlimited"
        });

        // Xóa listeners cũ để tránh duplicate
        qualityDropdown.onValueChanged.RemoveAllListeners();
        resolutionDropdown.onValueChanged.RemoveAllListeners();
        screenModeDropdown.onValueChanged.RemoveAllListeners();
        framerateDropdown.onValueChanged.RemoveAllListeners();
        vsyncToggle.onValueChanged.RemoveAllListeners();
        brightnessSlider.onValueChanged.RemoveAllListeners();

        // Add listeners mới
        qualityDropdown.onValueChanged.AddListener(OnQualityChanged);
        resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
        screenModeDropdown.onValueChanged.AddListener(OnScreenModeChanged);
        framerateDropdown.onValueChanged.AddListener(OnFramerateChanged);
        vsyncToggle.onValueChanged.AddListener(OnVSyncChanged);
        brightnessSlider.onValueChanged.AddListener(OnBrightnessChanged);
    }

    public void LoadCurrentSettings()
    {
        var data = controller.GetData();

        qualityDropdown.SetValueWithoutNotify(data.qualityLevel);
        resolutionDropdown.SetValueWithoutNotify(data.resolutionIndex);
        screenModeDropdown.SetValueWithoutNotify(controller.GetScreenModeIndex());
        framerateDropdown.SetValueWithoutNotify(controller.GetFrameRateIndex());
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

    private void OnScreenModeChanged(int value)
    {
        controller.SetScreenMode(value);
    }

    private void OnFramerateChanged(int value)
    {
        controller.SetTargetFrameRate(value);

        // Nếu chọn Unlimited hoặc FPS cao, có thể tắt VSync
        if (value == 3) // Unlimited
        {
            vsyncToggle.SetIsOnWithoutNotify(false);
            controller.SetVSync(false);
        }
    }

    private void OnVSyncChanged(bool value)
    {
        controller.SetVSync(value);

        // Nếu bật VSync, có thể khóa framerate dropdown
        // framerateDropdown.interactable = !value;
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