using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class ControlsSettingsUI : MonoBehaviour
{
    [Header("UI References")]
    public Slider mouseSensitivitySlider;
    public TMP_Text sensitivityValueText;
    public Toggle invertYToggle;

    private ControlsController controller;
    private bool isInitialized = false;

    // Pending changes
    private float pendingMouseSensitivity;
    private bool pendingInvertY;

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

    private IEnumerator DelayedInitialize()
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
            controller = SettingsManager.Instance.Controls;
            InitializeUI();
            isInitialized = true;
            Debug.Log("ControlsSettingsUI: Initialized");
        }
        else
        {
            Debug.LogError("ControlsSettingsUI: SettingsManager.Instance is null!");
        }
    }

    private void InitializeUI()
    {
        // Xóa listeners cũ
        mouseSensitivitySlider.onValueChanged.RemoveAllListeners();
        invertYToggle.onValueChanged.RemoveAllListeners();

        // Add listeners mới - CHỈ lưu vào pending
        mouseSensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);
        invertYToggle.onValueChanged.AddListener(OnInvertYChanged);
    }

    public void LoadCurrentSettings()
    {
        EnsureInitialized();

        if (controller == null)
        {
            Debug.LogError("ControlsSettingsUI: Cannot load settings - controller is null");
            return;
        }

        var data = controller.GetData();

        mouseSensitivitySlider.SetValueWithoutNotify(data.mouseSensitivity);
        invertYToggle.SetIsOnWithoutNotify(data.invertY);
        UpdateSensitivityText(data.mouseSensitivity);

        // Load vào pending values
        pendingMouseSensitivity = data.mouseSensitivity;
        pendingInvertY = data.invertY;
    }

    // CHỈ lưu vào pending, KHÔNG apply ngay
    private void OnSensitivityChanged(float value)
    {
        pendingMouseSensitivity = value;
        UpdateSensitivityText(value);
    }

    private void OnInvertYChanged(bool value)
    {
        pendingInvertY = value;
    }

    private void UpdateSensitivityText(float value)
    {
        sensitivityValueText.text = value.ToString("F2");
    }

    // Method này được gọi bởi SettingsMenuUI khi nhấn Apply
    public void ApplyPendingChanges()
    {
        EnsureInitialized();

        if (controller == null)
        {
            Debug.LogError("ControlsSettingsUI: Cannot apply - controller is null!");
            return;
        }

        Debug.Log("Controls: Applying pending changes...");
        controller.SetMouseSensitivity(pendingMouseSensitivity);
        controller.SetInvertY(pendingInvertY);
        Debug.Log($"Controls pending applied - Sensitivity: {pendingMouseSensitivity}, Invert: {pendingInvertY}");
    }
}