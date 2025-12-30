using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class KeybindRowUI : MonoBehaviour
{
    [Header("UI References")]
    public TMP_Text actionNameText;
    public Button rebindButton;
    public TMP_Text keyText;

    private KeybindData keybindData;
    private KeybindingsController controller;
    private bool isWaitingForInput = false;

    public void Initialize(KeybindData data, KeybindingsController ctrl)
    {
        keybindData = data;
        controller = ctrl;

        // Hiển thị tên action
        actionNameText.text = data.displayName;

        // Hiển thị key hiện tại
        UpdateKeyText(data.bindingPath);

        // Setup button
        rebindButton.onClick.AddListener(OnRebindButtonClicked);
    }

    private void OnRebindButtonClicked()
    {
        if (isWaitingForInput) return;

        isWaitingForInput = true;
        keyText.text = "Press any key...";

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayUISFX(SoundType.UI_Click);
        }

        // Bắt đầu rebinding
        controller.StartRebinding(
            keybindData.actionName,
            keybindData.bindingIndex,
            OnRebindComplete,
            OnRebindCancel
        );
    }

    private void OnRebindComplete(string newPath)
    {
        isWaitingForInput = false;
        keybindData.bindingPath = newPath;
        UpdateKeyText(newPath);

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayUISFX(SoundType.UI_Click);
        }
    }

    private void OnRebindCancel()
    {
        isWaitingForInput = false;
        UpdateKeyText(keybindData.bindingPath);
    }

    private void UpdateKeyText(string path)
    {
        // Chuyển đổi binding path thành tên key dễ đọc
        string displayName = GetDisplayName(path);
        keyText.text = displayName;
    }

    // Chuyển đổi binding path thành tên hiển thị
    private string GetDisplayName(string path)
    {
        if (string.IsNullOrEmpty(path)) return "None";

        // Xử lý keyboard keys
        if (path.Contains("/Keyboard/"))
        {
            string key = path.Replace("<Keyboard>/", "");

            // Xử lý các key đặc biệt
            switch (key.ToLower())
            {
                case "space": return "Space";
                case "leftshift": return "L Shift";
                case "rightshift": return "R Shift";
                case "leftctrl": return "L Ctrl";
                case "rightctrl": return "R Ctrl";
                case "leftalt": return "L Alt";
                case "rightalt": return "R Alt";
                case "escape": return "Esc";
                default: return key.ToUpper();
            }
        }

        // Xử lý mouse buttons
        if (path.Contains("/Mouse/"))
        {
            if (path.Contains("leftButton")) return "Mouse Left";
            if (path.Contains("rightButton")) return "Mouse Right";
            if (path.Contains("middleButton")) return "Mouse Middle";
        }

        // Trả về path gốc nếu không match
        return path.Split('/').Length > 1 ? path.Split('/')[1] : path;
    }
}