using UnityEngine;
using UnityEngine.InputSystem;
using System;
using System.Collections.Generic;
using UnityEngine.UI;

public class SettingsMenuUI : MonoBehaviour
{
    [Header("Tab Buttons")]
    public Button graphicsTabButton;
    public Button audioTabButton;
    public Button controlsTabButton;
    public Button keybindingsTabButton;

    [Header("Panel GameObjects")]
    public GameObject graphicsPanel;
    public GameObject audioPanel;
    public GameObject controlsPanel;
    public GameObject keybindingsPanel;

    [Header("Action Buttons")]
    public Button applyButton;
    public Button saveButton;
    public Button resetButton;
    public Button backButton;

    [Header("Tab Visual Colors")]
    public Color activeTabColor = Color.white;
    public Color inactiveTabColor = Color.gray;

    private GameObject currentActivePanel;

    void Start()
    {
        // Setup tab buttons
        graphicsTabButton.onClick.AddListener(() => ShowPanel(graphicsPanel));
        audioTabButton.onClick.AddListener(() => ShowPanel(audioPanel));
        controlsTabButton.onClick.AddListener(() => ShowPanel(controlsPanel));
        keybindingsTabButton.onClick.AddListener(() => ShowPanel(keybindingsPanel));

        // Setup action buttons
        applyButton.onClick.AddListener(OnApply);
        saveButton.onClick.AddListener(OnSave);
        resetButton.onClick.AddListener(OnReset);
        backButton.onClick.AddListener(OnBack);

        // Hiển thị graphics panel mặc định
        ShowPanel(graphicsPanel);
    }

    public void ShowPanel(GameObject panelToShow)
    {
        // Tắt tất cả panels
        graphicsPanel.SetActive(false);
        audioPanel.SetActive(false);
        controlsPanel.SetActive(false);
        keybindingsPanel.SetActive(false);

        // Bật panel được chọn
        panelToShow.SetActive(true);
        currentActivePanel = panelToShow;

        // Cập nhật màu tab buttons
        UpdateTabColors(panelToShow);

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayUISFX(SoundType.UI_Click);
        }
    }

    private void UpdateTabColors(GameObject activePanel)
    {
        // Reset tất cả màu tab
        SetButtonColor(graphicsTabButton, inactiveTabColor);
        SetButtonColor(audioTabButton, inactiveTabColor);
        SetButtonColor(controlsTabButton, inactiveTabColor);
        SetButtonColor(keybindingsTabButton, inactiveTabColor);

        // Highlight tab đang active
        if (activePanel == graphicsPanel)
            SetButtonColor(graphicsTabButton, activeTabColor);
        else if (activePanel == audioPanel)
            SetButtonColor(audioTabButton, activeTabColor);
        else if (activePanel == controlsPanel)
            SetButtonColor(controlsTabButton, activeTabColor);
        else if (activePanel == keybindingsPanel)
            SetButtonColor(keybindingsTabButton, activeTabColor);
    }

    private void SetButtonColor(Button button, Color color)
    {
        var colors = button.colors;
        colors.normalColor = color;
        colors.selectedColor = color;
        button.colors = colors;
    }

    private void OnApply()
    {
        SettingsManager.Instance.ApplyAllSettings();

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayUISFX(SoundType.UI_Click);
        }
    }

    private void OnSave()
    {
        SettingsManager.Instance.SaveSettings();

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayUISFX(SoundType.UI_Click);
        }
    }

    private void OnReset()
    {
        SettingsManager.Instance.ResetToDefaults();

        // Refresh tất cả UI panels
        RefreshAllPanels();

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayUISFX(SoundType.UI_Click);
        }
    }

    private void RefreshAllPanels()
    {
        var graphicsUI = graphicsPanel.GetComponent<GraphicsSettingsUI>();
        var audioUI = audioPanel.GetComponent<AudioSettingsUI>();
        var controlsUI = controlsPanel.GetComponent<ControlsSettingsUI>();
        var keybindingsUI = keybindingsPanel.GetComponent<KeybindingsSettingsUI>();

        if (graphicsUI != null) graphicsUI.LoadCurrentSettings();
        if (audioUI != null) audioUI.LoadCurrentSettings();
        if (controlsUI != null) controlsUI.LoadCurrentSettings();
        if (keybindingsUI != null) keybindingsUI.LoadCurrentKeybinds();
    }

    private void OnBack()
    {
        gameObject.SetActive(false);

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayUISFX(SoundType.UI_Click);
        }
    }
}
