using System;
using UnityEngine;

public class GraphicsController
{
    private GraphicsSettingsData data;
    public event Action OnSettingsChanged;

    public GraphicsController(GraphicsSettingsData data)
    {
        this.data = data;
    }

    // ONLY store data, DON'T apply immediately
    public void SetQualityLevel(int level)
    {
        data.qualityLevel = level;
        OnSettingsChanged?.Invoke();
    }

    public void SetResolution(int index)
    {
        data.resolutionIndex = index;
        OnSettingsChanged?.Invoke();
    }

    public void SetScreenMode(int modeIndex)
    {
        // 0 = Fullscreen, 1 = Windowed, 2 = Borderless
        FullScreenMode mode = modeIndex switch
        {
            0 => FullScreenMode.ExclusiveFullScreen,
            1 => FullScreenMode.Windowed,
            2 => FullScreenMode.FullScreenWindow,  // Borderless
            _ => FullScreenMode.ExclusiveFullScreen
        };
        data.screenMode = mode;
        OnSettingsChanged?.Invoke();
    }

    public void SetVSync(bool enabled)
    {
        data.vsync = enabled;
        OnSettingsChanged?.Invoke();
    }

    public void SetTargetFrameRate(int framerateIndex)
    {
        // 0 = 60, 1 = 90, 2 = 144, 3 = Unlimited
        int fps = framerateIndex switch
        {
            0 => 60,
            1 => 90,
            2 => 144,
            3 => -1,  // Unlimited
            _ => 60
        };
        data.targetFrameRate = fps;
        OnSettingsChanged?.Invoke();
    }

    public void SetBrightness(float brightness)
    {
        data.brightness = brightness;
        OnSettingsChanged?.Invoke();
    }

    // Apply ALL settings at once when called
    public void ApplySettings()
    {
        // Apply quality
        QualitySettings.SetQualityLevel(data.qualityLevel);

        // Apply VSync
        QualitySettings.vSyncCount = data.vsync ? 1 : 0;

        // Apply framerate
        Application.targetFrameRate = data.targetFrameRate;

        // Apply resolution and screen mode together
        if (data.resolutionIndex >= 0 && data.resolutionIndex < Screen.resolutions.Length)
        {
            Resolution res = Screen.resolutions[data.resolutionIndex];
            Screen.SetResolution(res.width, res.height, data.screenMode);
        }

        // Apply brightness
        RenderSettings.ambientIntensity = data.brightness;

        Debug.Log($"Graphics Applied - Quality: {data.qualityLevel}, Resolution: {data.resolutionIndex}, VSync: {data.vsync}, FPS: {data.targetFrameRate}");
    }

    // Helper để UI lấy screen mode index hiện tại
    public int GetScreenModeIndex()
    {
        return data.screenMode switch
        {
            FullScreenMode.ExclusiveFullScreen => 0,
            FullScreenMode.Windowed => 1,
            FullScreenMode.FullScreenWindow => 2,
            _ => 0
        };
    }

    // Helper để UI lấy framerate index hiện tại
    public int GetFrameRateIndex()
    {
        return data.targetFrameRate switch
        {
            60 => 0,
            90 => 1,
            144 => 2,
            -1 => 3,
            _ => 0
        };
    }

    public void ResetToDefaults()
    {
        data.qualityLevel = 2;
        data.resolutionIndex = Screen.resolutions.Length - 1;
        data.screenMode = FullScreenMode.ExclusiveFullScreen;
        data.targetFrameRate = 60;
        data.vsync = true;
        data.brightness = 1.0f;
        OnSettingsChanged?.Invoke();
    }

    public GraphicsSettingsData GetData() => data;
}