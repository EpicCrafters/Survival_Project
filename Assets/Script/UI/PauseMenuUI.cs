using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System;

public class PauseMenuUI : MonoBehaviour
{

    public static PauseMenuUI instance;
    [Header("Main Menu Panel")]
    public GameObject mainMenuPanel;

    [Header("Settings Menu")]
    public GameObject settingsMenuPanel;

    [Header("Main Menu Buttons")]
    public Button resumeButton;
    public Button settingsButton;
    public Button mainMenuButton;
    public Button quitButton;

    private bool isPaused = false;
    public GameInput gameInput;

    private void OnEnable()
    {
       // Debug.Log($"[PauseMenuUI] OnEnable - GameObject: {gameObject.name}");
        SubscribeToGameInput();
    }

    private void OnDisable()
    {
        //Debug.Log($"[PauseMenuUI] OnDisable - GameObject: {gameObject.name}");
        UnsubscribeFromGameInput();
    }

    void Awake()
    {
        instance = this;
        //Debug.Log($"[PauseMenuUI] Awake START - GameObject: {gameObject.name}");
        //Debug.Log($"[PauseMenuUI] MainMenuPanel null? {mainMenuPanel == null}");
        //Debug.Log($"[PauseMenuUI] SettingsMenuPanel null? {settingsMenuPanel == null}");

        if (mainMenuPanel == null || settingsMenuPanel == null)
        {
            //Debug.LogError("[PauseMenuUI] Panel references are NULL! Check Inspector!");
            return;
        }

        // Log initial state
       // Debug.Log($"[PauseMenuUI] MainMenuPanel initial state: {mainMenuPanel.activeSelf}");
        //Debug.Log($"[PauseMenuUI] SettingsMenuPanel initial state: {settingsMenuPanel.activeSelf}");

        // Setup buttons
        if (resumeButton != null) resumeButton.onClick.AddListener(OnResume);
        if (settingsButton != null) settingsButton.onClick.AddListener(OnSettings);
        if (mainMenuButton != null) mainMenuButton.onClick.AddListener(OnBackToMainMenu);
        if (quitButton != null) quitButton.onClick.AddListener(OnQuit);

        // Hide panels
        mainMenuPanel.SetActive(false);
        settingsMenuPanel.SetActive(false);

        //Debug.Log($"[PauseMenuUI] After SetActive(false) - MainMenuPanel: {mainMenuPanel.activeSelf}");
        //Debug.Log($"[PauseMenuUI] After SetActive(false) - SettingsMenuPanel: {settingsMenuPanel.activeSelf}");
        //Debug.Log($"[PauseMenuUI] Awake END");
    }

    void Start()
    {
       // Debug.Log($"[PauseMenuUI] Start - MainMenuPanel: {mainMenuPanel.activeSelf}");
       // Debug.Log($"[PauseMenuUI] Start - SettingsMenuPanel: {settingsMenuPanel.activeSelf}");
    }

   

    private void SubscribeToGameInput()
    {
        if (gameInput != null)
        {
            gameInput.OnShowPauseMenu -= GameInput_OnShowPauseMenu;
            gameInput.OnShowPauseMenu += GameInput_OnShowPauseMenu;
            //Debug.Log("[PauseMenuUI] Subscribed to GameInput");
        }
        else
        {
            //Debug.LogWarning("[PauseMenuUI] GameInput is NULL, cannot subscribe");
        }
    }

    private void UnsubscribeFromGameInput()
    {
        if (gameInput != null)
        {
            gameInput.OnShowPauseMenu -= GameInput_OnShowPauseMenu;
           // Debug.Log("[PauseMenuUI] Unsubscribed from GameInput");
        }
    }

    private void GameInput_OnShowPauseMenu(object sender, EventArgs e)
    {
       // Debug.Log("[PauseMenuUI] GameInput_OnShowPauseMenu triggered");

        if (settingsMenuPanel.activeSelf)
        {
            CloseSettings();
            return;
        }

        if (isPaused)
        {
            OnResume();
        }
        else
        {
            OpenPauseMenu();
        }
    }

    private void OpenPauseMenu()
    {
       // Debug.Log("[PauseMenuUI] OpenPauseMenu START");
        isPaused = true;

        mainMenuPanel.SetActive(true);
        settingsMenuPanel.SetActive(false);

        // Sử dụng CursorManager thay vì điều khiển con trỏ trực tiếp
        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.ShowCursor(CursorManager.CursorPriority.PauseMenu);
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayUISFX(SoundType.UI_Click);
        }
    }

    private void OnResume()
    {
       // Debug.Log("[PauseMenuUI] OnResume");
        isPaused = false;

        mainMenuPanel.SetActive(false);
        settingsMenuPanel.SetActive(false);

        // Sử dụng CursorManager - nó sẽ tự động kiểm tra các UI khác (death screen, inventory, etc.)
        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.HideCursor(CursorManager.CursorPriority.PauseMenu);
            // Refresh to check if inventory is still open
            CursorManager.Instance.RefreshCursorState();
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayUISFX(SoundType.UI_Click);
        }
    }

    private void OnSettings()
    {
        //Debug.Log("[PauseMenuUI] OnSettings");
        mainMenuPanel.SetActive(false);
        settingsMenuPanel.SetActive(true);

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayUISFX(SoundType.UI_Click);
        }
    }

    private void CloseSettings()
    {
       // Debug.Log("[PauseMenuUI] CloseSettings");
        settingsMenuPanel.SetActive(false);
        mainMenuPanel.SetActive(true);

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayUISFX(SoundType.UI_Click);
        }
    }

    private void OnBackToMainMenu()
    {
      //  Debug.Log("[PauseMenuUI] OnBackToMainMenu");
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayUISFX(SoundType.UI_Click);
        }

        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }

    private void OnQuit()
    {
       // Debug.Log("[PauseMenuUI] OnQuit");
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayUISFX(SoundType.UI_Click);
        }


       

            Application.Quit();

    }

    public void SetGameInput(GameInput input)
    {
       // Debug.Log($"[PauseMenuUI] SetGameInput called - input null? {input == null}");
        UnsubscribeFromGameInput();
        gameInput = input;
        SubscribeToGameInput();
    }


    public bool IsPaused()
    {
        return isPaused;
    }
}