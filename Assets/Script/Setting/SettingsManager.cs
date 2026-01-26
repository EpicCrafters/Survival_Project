using UnityEngine;
using UnityEngine.InputSystem;

public class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance { get; private set; }

    [SerializeField] private InputActionAsset inputActions;

    private SettingsData data;
    private GraphicsController graphicsController;
    private AudioController audioController;
    private ControlsController controlsController;
    private KeybindingsController keybindingsController;

    public GraphicsController Graphics => graphicsController;
    public AudioController Audio => audioController;
    public ControlsController Controls => controlsController;
    public KeybindingsController Keybindings => keybindingsController;

    private const string SETTINGS_KEY = "GameSettings";

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Initialize();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Initialize()
    {
        Debug.Log("SettingsManager: Initializing...");

        // Load settings FIRST
        LoadSettings();

        // Then initialize controllers with loaded data
        graphicsController = new GraphicsController(data.graphics);
        audioController = new AudioController(data.audio);
        controlsController = new ControlsController(data.controls);
        keybindingsController = new KeybindingsController(data.keybindings, inputActions);

        // Subscribe to events
        graphicsController.OnSettingsChanged += OnAnySettingChanged;
        audioController.OnSettingsChanged += OnAnySettingChanged;
        controlsController.OnSettingsChanged += OnAnySettingChanged;
        keybindingsController.OnKeybindChanged += OnAnySettingChanged;

        // Apply loaded settings immediately
        ApplyAllSettings();

        Debug.Log("SettingsManager: Initialized and settings applied");
    }

    private void OnAnySettingChanged()
    {
        // Settings changed event
    }

    public void ApplyAllSettings()
    {
        Debug.Log("SettingsManager: Applying all settings...");

        graphicsController.ApplySettings();
        audioController.ApplySettings();
        controlsController.ApplySettings();

        Debug.Log("SettingsManager: All settings applied to Unity");
    }

    public void SaveSettings()
    {
        Debug.Log("SettingsManager: Saving settings...");

        // Save main settings
        string json = JsonUtility.ToJson(data, true);
        PlayerPrefs.SetString(SETTINGS_KEY, json);
        PlayerPrefs.Save();

        // Save keybinds separately
        keybindingsController.SaveKeybindsToPlayerPrefs();

        Debug.Log("SettingsManager: Settings saved successfully");
        Debug.Log($"Saved data: {json}");
    }

    public void LoadSettings()
    {
        Debug.Log("SettingsManager: Loading settings...");

        if (PlayerPrefs.HasKey(SETTINGS_KEY))
        {
            string json = PlayerPrefs.GetString(SETTINGS_KEY);
            data = JsonUtility.FromJson<SettingsData>(json);
            Debug.Log("SettingsManager: Settings loaded from PlayerPrefs");
            Debug.Log($"Loaded data: {json}");
        }
        else
        {
            // Create new default settings
            data = new SettingsData();
            Debug.Log("SettingsManager: No saved settings found, using defaults");

            // Save defaults immediately
            string json = JsonUtility.ToJson(data, true);
            PlayerPrefs.SetString(SETTINGS_KEY, json);
            PlayerPrefs.Save();
            Debug.Log("SettingsManager: Default settings saved");
        }
    }

    public void ResetToDefaults()
    {
        Debug.Log("SettingsManager: Resetting to defaults...");

        // Reset all controllers to defaults
        graphicsController.ResetToDefaults();
        audioController.ResetToDefaults();
        controlsController.ResetToDefaults();
        keybindingsController.ResetToDefaults();

        // Apply the defaults
        ApplyAllSettings();

        // Save the defaults
        SaveSettings();

        Debug.Log("SettingsManager: Reset complete");
    }

    private void OnApplicationQuit()
    {
        Debug.Log("SettingsManager: Application quitting");
    }
}