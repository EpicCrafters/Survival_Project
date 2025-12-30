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
        LoadSettings();

        graphicsController = new GraphicsController(data.graphics);
        audioController = new AudioController(data.audio);
        controlsController = new ControlsController(data.controls);
        keybindingsController = new KeybindingsController(data.keybindings, inputActions);

        graphicsController.OnSettingsChanged += OnAnySettingChanged;
        audioController.OnSettingsChanged += OnAnySettingChanged;
        controlsController.OnSettingsChanged += OnAnySettingChanged;
        keybindingsController.OnKeybindChanged += OnAnySettingChanged;

        ApplyAllSettings();
    }

    private void OnAnySettingChanged()
    {
        // Auto-save hoặc đánh dấu có thay đổi
    }

    public void ApplyAllSettings()
    {
        graphicsController.ApplySettings();
        audioController.ApplySettings();
        controlsController.ApplySettings();
    }

    public void SaveSettings()
    {
        string json = JsonUtility.ToJson(data, true);
        PlayerPrefs.SetString("GameSettings", json);
        PlayerPrefs.Save();

        // Lưu keybinds riêng
        keybindingsController.SaveKeybindsToPlayerPrefs();

        Debug.Log("Settings đã được lưu");
    }

    public void LoadSettings()
    {
        if (PlayerPrefs.HasKey("GameSettings"))
        {
            string json = PlayerPrefs.GetString("GameSettings");
            data = JsonUtility.FromJson<SettingsData>(json);
            Debug.Log("Settings đã được load");
        }
        else
        {
            data = new SettingsData();
            Debug.Log("Sử dụng settings mặc định");
        }
    }

    public void ResetToDefaults()
    {
        data = new SettingsData();
        keybindingsController.ResetToDefaults();
        ApplyAllSettings();
        SaveSettings();
    }
}