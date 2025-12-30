using UnityEngine;
using UnityEngine.InputSystem;
using System;

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
        Screen.SetResolution(res.width, res.height, data.fullscreen);
        OnSettingsChanged?.Invoke();
    }

    public void SetFullscreen(bool fullscreen)
    {
        data.fullscreen = fullscreen;
        Screen.fullScreen = fullscreen;
        OnSettingsChanged?.Invoke();
    }

    public void SetVSync(bool enabled)
    {
        data.vsync = enabled;
        QualitySettings.vSyncCount = enabled ? 1 : 0;
        OnSettingsChanged?.Invoke();
    }

    public void SetTargetFrameRate(int fps)
    {
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
        SetFullscreen(data.fullscreen);
        SetVSync(data.vsync);
        SetTargetFrameRate(data.targetFrameRate);
        SetBrightness(data.brightness);
    }

    public GraphicsSettingsData GetData() => data;
}
