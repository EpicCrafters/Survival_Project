using UnityEngine;
using UnityEngine.InputSystem;
using System;

public class ControlsController
{
    private ControlsSettingsData data;
    public event Action OnSettingsChanged;

    public ControlsController(ControlsSettingsData data)
    {
        this.data = data;
    }

    public void SetMouseSensitivity(float sensitivity)
    {
        data.mouseSensitivity = sensitivity;
        OnSettingsChanged?.Invoke();
    }

    public void SetInvertY(bool invert)
    {
        data.invertY = invert;
        OnSettingsChanged?.Invoke();
    }

    public void ApplySettings()
    {
        // Apply settings ở đây nếu cần
    }

    public ControlsSettingsData GetData() => data;
}