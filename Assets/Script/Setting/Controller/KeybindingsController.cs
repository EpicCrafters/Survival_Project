using UnityEngine;
using UnityEngine.InputSystem;
using System;
using System.Collections.Generic;

public class KeybindingsController
{
    private KeybindingsData data;
    private InputActionAsset inputActions;
    public event Action OnKeybindChanged;

    public KeybindingsController(KeybindingsData data, InputActionAsset inputActions)
    {
        this.data = data;
        this.inputActions = inputActions;

        // Load keybinds từ PlayerPrefs khi khởi tạo
        LoadKeybindsFromPlayerPrefs();
    }

    // Lấy tất cả keybinds hiện tại
    public List<KeybindData> GetAllKeybinds()
    {
        if (data.keybinds.Count == 0)
        {
            InitializeDefaultKeybinds();
        }
        return data.keybinds;
    }

    // Khởi tạo keybinds mặc định từ Input Actions
    private void InitializeDefaultKeybinds()
    {
        data.keybinds.Clear();

        // Danh sách các action cần rebind
        string[] actionNames = { "Move", "Jump", "Sprint", "Interact", "Attack", "Aim", "ShowInventory", "DropItem" };
        string[] displayNames = { "Move", "Jump", "Sprint", "Interact", "Attack", "Aim", "Inventory", "Drop Item" };

        for (int i = 0; i < actionNames.Length; i++)
        {
            InputAction action = inputActions.FindAction(actionNames[i]);
            if (action != null && action.bindings.Count > 0)
            {
                // Lấy binding đầu tiên của action
                var binding = action.bindings[0];

                data.keybinds.Add(new KeybindData
                {
                    actionName = actionNames[i],
                    displayName = displayNames[i],
                    bindingPath = binding.effectivePath,
                    bindingIndex = 0
                });
            }
        }
    }

    // Bắt đầu rebind một key
    public void StartRebinding(string actionName, int bindingIndex, Action<string> onComplete, Action onCancel = null)
    {
        InputAction action = inputActions.FindAction(actionName);
        if (action == null)
        {
            Debug.LogError($"Action {actionName} không tìm thấy!");
            return;
        }

        // Disable action để rebind
        action.Disable();

        var rebindOperation = action.PerformInteractiveRebinding(bindingIndex)
            .WithControlsExcluding("Mouse")  // Loại trừ mouse movement
            .OnMatchWaitForAnother(0.1f)
            .OnComplete(operation =>
            {
                // Lưu binding mới
                string newPath = action.bindings[bindingIndex].effectivePath;
                UpdateKeybind(actionName, bindingIndex, newPath);

                action.Enable();
                operation.Dispose();

                onComplete?.Invoke(newPath);
                OnKeybindChanged?.Invoke();
            })
            .OnCancel(operation =>
            {
                action.Enable();
                operation.Dispose();
                onCancel?.Invoke();
            });

        rebindOperation.Start();
    }

    // Cập nhật keybind trong data
    private void UpdateKeybind(string actionName, int bindingIndex, string newPath)
    {
        KeybindData keybind = data.keybinds.Find(k => k.actionName == actionName);
        if (keybind != null)
        {
            keybind.bindingPath = newPath;
            keybind.bindingIndex = bindingIndex;
        }
    }

    // Reset về keybinds mặc định
    public void ResetToDefaults()
    {
        // Xóa tất cả overrides
        foreach (var map in inputActions.actionMaps)
        {
            map.RemoveAllBindingOverrides();
        }

        InitializeDefaultKeybinds();
        SaveKeybindsToPlayerPrefs();
        OnKeybindChanged?.Invoke();
    }

    // Lưu keybinds vào PlayerPrefs
    public void SaveKeybindsToPlayerPrefs()
    {
        string json = inputActions.SaveBindingOverridesAsJson();
        PlayerPrefs.SetString("InputBindings", json);
        PlayerPrefs.Save();
    }

    // Load keybinds từ PlayerPrefs
    private void LoadKeybindsFromPlayerPrefs()
    {
        if (PlayerPrefs.HasKey("InputBindings"))
        {
            string json = PlayerPrefs.GetString("InputBindings");
            inputActions.LoadBindingOverridesFromJson(json);

            // Cập nhật lại data
            InitializeDefaultKeybinds();
        }
    }

    public KeybindingsData GetData() => data;
}