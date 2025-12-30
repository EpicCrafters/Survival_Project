using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class KeybindingsSettingsUI : MonoBehaviour
{
    [Header("UI References")]
    public Transform keybindContainer;  // Container chứa các keybind rows
    public GameObject keybindRowPrefab; // Prefab cho mỗi row

    [Header("Reset Button")]
    public Button resetKeybindsButton;

    private KeybindingsController controller;
    private List<KeybindRowUI> keybindRows = new List<KeybindRowUI>();

    void OnEnable()
    {
        if (SettingsManager.Instance != null)
        {
            controller = SettingsManager.Instance.Keybindings;
            LoadCurrentKeybinds();
        }
    }

    void Start()
    {
        if (controller == null)
        {
            controller = SettingsManager.Instance.Keybindings;
            LoadCurrentKeybinds();
        }

        if (resetKeybindsButton != null)
        {
            resetKeybindsButton.onClick.AddListener(OnResetKeybinds);
        }
    }

    public void LoadCurrentKeybinds()
    {
        // Xóa các row cũ
        foreach (var row in keybindRows)
        {
            if (row != null && row.gameObject != null)
            {
                Destroy(row.gameObject);
            }
        }
        keybindRows.Clear();

        // Tạo row mới cho mỗi keybind
        List<KeybindData> keybinds = controller.GetAllKeybinds();

        foreach (var keybind in keybinds)
        {
            GameObject rowObj = Instantiate(keybindRowPrefab, keybindContainer);
            KeybindRowUI row = rowObj.GetComponent<KeybindRowUI>();

            if (row != null)
            {
                row.Initialize(keybind, controller);
                keybindRows.Add(row);
            }
        }
    }

    private void OnResetKeybinds()
    {
        controller.ResetToDefaults();
        LoadCurrentKeybinds();

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayUISFX(SoundType.UI_Click);
        }
    }
}