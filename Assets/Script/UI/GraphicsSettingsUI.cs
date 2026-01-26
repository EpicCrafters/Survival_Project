using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GraphicsSettingsUI : MonoBehaviour
{
    [Header("UI References")]
    public TMP_Dropdown qualityDropdown;
    public TMP_Dropdown resolutionDropdown;
    public TMP_Dropdown screenModeDropdown;
    public TMP_Dropdown framerateDropdown;
    public Toggle vsyncToggle;
    public Slider brightnessSlider;
    public TMP_Text brightnessValueText;

    private GraphicsController controller;
    private bool isInitialized = false;

    // Store unique resolutions to avoid duplicates
    private List<Resolution> uniqueResolutions = new List<Resolution>();

    // Pending changes - lưu tạm thay đổi trước khi apply
    private int pendingQualityLevel;
    private int pendingResolutionIndex;
    private int pendingScreenMode;
    private int pendingFrameRate;
    private bool pendingVSync;
    private float pendingBrightness;

    void OnEnable()
    {
        // Delay initialization to next frame to ensure SettingsManager is ready
        if (!isInitialized)
        {
            StartCoroutine(DelayedInitialize());
        }
        else
        {
            LoadCurrentSettings();
        }
    }

    void Start()
    {
        if (!isInitialized)
        {
            StartCoroutine(DelayedInitialize());
        }
    }

    private System.Collections.IEnumerator DelayedInitialize()
    {
        // Wait until SettingsManager is ready
        while (SettingsManager.Instance == null)
        {
            yield return null;
        }

        EnsureInitialized();
        if (isInitialized)
        {
            LoadCurrentSettings();
        }
    }

    // Make sure controller is always initialized
    private void EnsureInitialized()
    {
        if (isInitialized) return;

        if (SettingsManager.Instance != null)
        {
            controller = SettingsManager.Instance.Graphics;
            InitializeUI();
            isInitialized = true;
            Debug.Log("GraphicsSettingsUI: Initialized");
        }
        else
        {
            Debug.LogError("GraphicsSettingsUI: SettingsManager.Instance is null!");
        }
    }

    private void InitializeUI()
    {
        // Populate quality dropdown
        qualityDropdown.ClearOptions();
        qualityDropdown.AddOptions(new List<string>(QualitySettings.names));

        // Populate resolution dropdown with UNIQUE resolutions
        resolutionDropdown.ClearOptions();
        var resOptions = new List<string>();
        uniqueResolutions.Clear();

        // Get unique resolutions (filter by width x height, ignore refresh rate)
        var allResolutions = Screen.resolutions;
        var seenResolutions = new HashSet<string>();

        foreach (var res in allResolutions)
        {
            string resKey = $"{res.width}x{res.height}";
            if (!seenResolutions.Contains(resKey))
            {
                seenResolutions.Add(resKey);
                uniqueResolutions.Add(res);
                resOptions.Add($"{res.width} x {res.height}");
            }
        }

        resolutionDropdown.AddOptions(resOptions);

        Debug.Log($"Found {uniqueResolutions.Count} unique resolutions");

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

        // Add listeners mới - CHỈ lưu vào pending, KHÔNG apply
        qualityDropdown.onValueChanged.AddListener(OnQualityChanged);
        resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
        screenModeDropdown.onValueChanged.AddListener(OnScreenModeChanged);
        framerateDropdown.onValueChanged.AddListener(OnFramerateChanged);
        vsyncToggle.onValueChanged.AddListener(OnVSyncChanged);
        brightnessSlider.onValueChanged.AddListener(OnBrightnessChanged);
    }

    public void LoadCurrentSettings()
    {
        EnsureInitialized();

        if (controller == null)
        {
            Debug.LogError("GraphicsSettingsUI: Cannot load settings - controller is null");
            return;
        }

        var data = controller.GetData();

        // Load quality
        qualityDropdown.SetValueWithoutNotify(data.qualityLevel);

        // Find matching resolution index
        int resIndex = FindResolutionIndex(data.resolutionIndex);
        resolutionDropdown.SetValueWithoutNotify(resIndex);

        // Load other settings
        screenModeDropdown.SetValueWithoutNotify(controller.GetScreenModeIndex());
        framerateDropdown.SetValueWithoutNotify(controller.GetFrameRateIndex());
        vsyncToggle.SetIsOnWithoutNotify(data.vsync);
        brightnessSlider.SetValueWithoutNotify(data.brightness);
        UpdateBrightnessText(data.brightness);

        // Load vào pending values
        pendingQualityLevel = data.qualityLevel;
        pendingResolutionIndex = resIndex;
        pendingScreenMode = controller.GetScreenModeIndex();
        pendingFrameRate = controller.GetFrameRateIndex();
        pendingVSync = data.vsync;
        pendingBrightness = data.brightness;

        Debug.Log($"Loaded settings - Resolution index: {resIndex}, Quality: {data.qualityLevel}");
    }

    // Find the correct resolution index in our unique list
    private int FindResolutionIndex(int savedIndex)
    {
        if (savedIndex < 0 || savedIndex >= Screen.resolutions.Length)
        {
            Debug.LogWarning($"Invalid saved resolution index: {savedIndex}, using current screen resolution");
            return FindCurrentResolutionIndex();
        }

        Resolution savedRes = Screen.resolutions[savedIndex];

        for (int i = 0; i < uniqueResolutions.Count; i++)
        {
            if (uniqueResolutions[i].width == savedRes.width &&
                uniqueResolutions[i].height == savedRes.height)
            {
                return i;
            }
        }

        return FindCurrentResolutionIndex();
    }

    // Find current screen resolution in our list
    private int FindCurrentResolutionIndex()
    {
        for (int i = 0; i < uniqueResolutions.Count; i++)
        {
            if (uniqueResolutions[i].width == Screen.width &&
                uniqueResolutions[i].height == Screen.height)
            {
                return i;
            }
        }
        return uniqueResolutions.Count - 1; // Default to highest
    }

    // CHỈ lưu vào pending, KHÔNG apply ngay
    private void OnQualityChanged(int value)
    {
        pendingQualityLevel = value;
        Debug.Log($"Quality changed to: {value}");
    }

    private void OnResolutionChanged(int value)
    {
        pendingResolutionIndex = value;
        Debug.Log($"Resolution changed to index: {value} ({uniqueResolutions[value].width}x{uniqueResolutions[value].height})");
    }

    private void OnScreenModeChanged(int value)
    {
        pendingScreenMode = value;
        Debug.Log($"Screen mode changed to: {value}");
    }

    private void OnFramerateChanged(int value)
    {
        pendingFrameRate = value;

        // Nếu chọn Unlimited, suggest tắt VSync
        if (value == 3) // Unlimited
        {
            vsyncToggle.SetIsOnWithoutNotify(false);
            pendingVSync = false;
        }
        Debug.Log($"Framerate changed to index: {value}");
    }

    private void OnVSyncChanged(bool value)
    {
        pendingVSync = value;
        Debug.Log($"VSync changed to: {value}");
    }

    private void OnBrightnessChanged(float value)
    {
        pendingBrightness = value;
        UpdateBrightnessText(value);
    }

    private void UpdateBrightnessText(float value)
    {
        brightnessValueText.text = $"{Mathf.RoundToInt(value * 100)}%";
    }

    // Method này được gọi bởi SettingsMenuUI khi nhấn Apply
    public void ApplyPendingChanges()
    {
        EnsureInitialized();

        if (controller == null)
        {
            Debug.LogError("GraphicsSettingsUI: Cannot apply - controller is null!");
            return;
        }

        Debug.Log("Graphics: Applying pending changes...");

        controller.SetQualityLevel(pendingQualityLevel);

        // Convert our unique resolution index back to Screen.resolutions index
        Resolution selectedRes = uniqueResolutions[pendingResolutionIndex];
        int actualIndex = System.Array.FindIndex(Screen.resolutions,
            r => r.width == selectedRes.width && r.height == selectedRes.height);

        controller.SetResolution(actualIndex);
        controller.SetScreenMode(pendingScreenMode);
        controller.SetTargetFrameRate(pendingFrameRate);
        controller.SetVSync(pendingVSync);
        controller.SetBrightness(pendingBrightness);

        Debug.Log($"Graphics pending applied - Quality: {pendingQualityLevel}, Resolution: {actualIndex} ({selectedRes.width}x{selectedRes.height}), Screen Mode: {pendingScreenMode}");
    }
}