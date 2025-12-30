using UnityEngine;
using UnityEngine.UI;

public class SettingsMenu : MonoBehaviour
{
    [SerializeField] private GameObject settingsPanel;
    private bool isSettingsOpen = false;

    // Called from New Input System action
    public void ToggleSettings()
    {
        isSettingsOpen = !isSettingsOpen;
        settingsPanel.SetActive(isSettingsOpen);

        if (isSettingsOpen)
        {
            // Pause ambient when settings open
            AudioManager.Instance.PauseAmbientWithFade(0.5f);
            AudioManager.Instance.PlayButtonSound(SoundType.UI_Click);
        }
        else
        {
            // Resume ambient when settings close
            AudioManager.Instance.ResumeAmbientWithFade(0.5f, 0.6f);
            AudioManager.Instance.PlayButtonSound(SoundType.UI_Click);
        }
    }

    // Alternative: Separate methods for opening/closing
    public void OpenSettings()
    {
        settingsPanel.SetActive(true);
        AudioManager.Instance.PauseAmbientWithFade(0.5f);
        AudioManager.Instance.PlayButtonSound(SoundType.UI_Click);
    }

    public void CloseSettings()
    {
        settingsPanel.SetActive(false);
        AudioManager.Instance.ResumeAmbientWithFade(0.5f, 0.6f);
        AudioManager.Instance.PlayButtonSound(SoundType.UI_Click);
    }
}