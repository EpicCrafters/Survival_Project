using System;
using UnityEngine;

public class ControlsController
{
    private ControlsSettingsData data;
    public event Action OnSettingsChanged;

    public ControlsController(ControlsSettingsData data)
    {
        this.data = data;
    }

    // ONLY store data, DON'T apply immediately
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

    // Apply ALL settings at once when called
    public void ApplySettings()
    {
        // Apply to your player/camera controller here
        // Example:
        // if (PlayerController.Instance != null)
        // {
        //     PlayerController.Instance.SetMouseSensitivity(data.mouseSensitivity);
        //     PlayerController.Instance.SetInvertY(data.invertY);
        // }

        Debug.Log($"Controls Applied - Sensitivity: {data.mouseSensitivity}, Invert Y: {data.invertY}");
    }

    public void ResetToDefaults()
    {
        data.mouseSensitivity = 1.0f;
        data.invertY = false;
        OnSettingsChanged?.Invoke();
    }

    public ControlsSettingsData GetData() => data;
}