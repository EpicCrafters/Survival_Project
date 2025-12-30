using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ControlsSettingsUI : MonoBehaviour
{
    [Header("UI References")]
    public Slider mouseSensitivitySlider;
    public TMP_Text sensitivityValueText;
    public Toggle invertYToggle;

    private ControlsController controller;

    void OnEnable()
    {
        if (SettingsManager.Instance != null)
        {
            controller = SettingsManager.Instance.Controls;
            InitializeUI();
            LoadCurrentSettings();
        }
    }

    void Start()
    {
        if (controller == null)
        {
            controller = SettingsManager.Instance.Controls;
            InitializeUI();
            LoadCurrentSettings();
        }
    }

    private void InitializeUI()
    {
        // Xóa listeners cũ
        mouseSensitivitySlider.onValueChanged.RemoveAllListeners();
        invertYToggle.onValueChanged.RemoveAllListeners();

        // Add listeners mới
        mouseSensitivitySlider.onValueChanged.AddListener(OnSensitivityChanged);
        invertYToggle.onValueChanged.AddListener(OnInvertYChanged);
    }

    public void LoadCurrentSettings()
    {
        var data = controller.GetData();

        mouseSensitivitySlider.SetValueWithoutNotify(data.mouseSensitivity);
        invertYToggle.SetIsOnWithoutNotify(data.invertY);
        UpdateSensitivityText(data.mouseSensitivity);
    }

    private void OnSensitivityChanged(float value)
    {
        controller.SetMouseSensitivity(value);
        UpdateSensitivityText(value);
    }

    private void OnInvertYChanged(bool value)
    {
        controller.SetInvertY(value);
    }

    private void UpdateSensitivityText(float value)
    {
        sensitivityValueText.text = value.ToString("F2");
    }
}