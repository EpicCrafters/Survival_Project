[System.Serializable]
public class SettingsData
{
    public GraphicsSettingsData graphics = new GraphicsSettingsData();
    public AudioSettingsData audio = new AudioSettingsData();
    public ControlsSettingsData controls = new ControlsSettingsData();
    public KeybindingsData keybindings = new KeybindingsData();
}