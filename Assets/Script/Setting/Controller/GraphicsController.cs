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

    public void SetQualityLevel(int level)
    {
        data.qualityLevel = level;
        QualitySettings.SetQualityLevel(level);
        OnSettingsChanged?.Invoke();
    }

    public void SetResolution(int index)
    {
        data.resolutionIndex = index;
        Resolution res = Screen.resolutions[index];
        Screen.SetResolution(res.width, res.height, data.screenMode);
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

        // Áp dụng screen mode với resolution hiện tại
        Resolution currentRes = Screen.resolutions[data.resolutionIndex];
        Screen.SetResolution(currentRes.width, currentRes.height, mode);

        OnSettingsChanged?.Invoke();
    }

    public void SetVSync(bool enabled)
    {
        data.vsync = enabled;
        QualitySettings.vSyncCount = enabled ? 1 : 0;
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
        Application.targetFrameRate = fps;
        OnSettingsChanged?.Invoke();
    }

    public void SetBrightness(float brightness)
    {
        data.brightness = brightness;
        RenderSettings.ambientIntensity = brightness;
        OnSettingsChanged?.Invoke();
    }

    public void ApplySettings()
    {
        SetQualityLevel(data.qualityLevel);
        SetResolution(data.resolutionIndex);

        // Áp dụng screen mode
        int modeIndex = data.screenMode switch
        {
            FullScreenMode.ExclusiveFullScreen => 0,
            FullScreenMode.Windowed => 1,
            FullScreenMode.FullScreenWindow => 2,
            _ => 0
        };
        SetScreenMode(modeIndex);

        SetVSync(data.vsync);

        // Áp dụng framerate
        int framerateIndex = data.targetFrameRate switch
        {
            60 => 0,
            90 => 1,
            144 => 2,
            -1 => 3,
            _ => 0
        };
        SetTargetFrameRate(framerateIndex);

        SetBrightness(data.brightness);
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

    public GraphicsSettingsData GetData() => data;
}