using UnityEngine;

public class CursorManager : MonoBehaviour
{
    public static CursorManager Instance { get; private set; }

    public enum CursorPriority
    {
        Gameplay = 0,      // Lowest priority - normal gameplay / Ưu tiên thấp nhất - gameplay bình thường
        PauseMenu = 1,     // Medium priority - pause menu / Ưu tiên trung bình - menu tạm dừng
        DeathScreen = 2,   // High priority - death screen / Ưu tiên cao - màn hình chết
        MainMenu = 3       // Highest priority - main menu / Ưu tiên cao nhất - menu chính
    }

    private CursorPriority currentPriority = CursorPriority.Gameplay;

    private void Awake()
    {
        // Singleton setup - Safe for multiplayer vì chỉ ảnh hưởng local client
        // Singleton setup - Safe for multiplayer because it only affects local client
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Set initial gameplay cursor state / Đặt trạng thái con trỏ gameplay ban đầu
        SetCursorState(CursorPriority.Gameplay);
    }

    public void ShowCursor(CursorPriority priority)
    {
        // Chỉ cập nhật nếu ưu tiên mới >= ưu tiên hiện tại
        if (priority >= currentPriority)
        {
            currentPriority = priority;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Debug.Log($"[CursorManager] Cursor shown - Priority: {priority}");
        }
    }

    public void HideCursor(CursorPriority priority)
    {
        // Chỉ cập nhật nếu ưu tiên được giải phóng >= ưu tiên hiện tại
        if (priority >= currentPriority)
        {
            // Check what the new priority should be / Kiểm tra ưu tiên mới nên là gì
            CursorPriority newPriority = GetActiveHighestPriority();
            currentPriority = newPriority;
            SetCursorState(newPriority);
            Debug.Log($"[CursorManager] Cursor updated - Released: {priority}, New Priority: {newPriority}");
        }
    }

    private CursorPriority GetActiveHighestPriority()
    {
        // Check main menu (highest priority) / Kiểm tra menu chính (ưu tiên cao nhất)
        if (IsMainMenuActive())
        {
            return CursorPriority.MainMenu;
        }

        // Check death screen (high priority) / Kiểm tra màn hình chết (ưu tiên cao)
        if (DeathUIManager.Instance != null && DeathUIManager.Instance.IsDeathScreenActive())
        {
            return CursorPriority.DeathScreen;
        }

        // Check pause menu (medium priority) / Kiểm tra menu tạm dừng (ưu tiên trung bình)
        if (PauseMenuUI.instance != null && PauseMenuUI.instance.IsPaused())
        {
            return CursorPriority.PauseMenu;
        }

        // Default to gameplay / Mặc định về gameplay
        return CursorPriority.Gameplay;
    }

    private bool IsMainMenuActive()
    {
        // Tìm MainMenuUI trong scene
        // Find MainMenuUI in scene
        var mainMenu = FindObjectOfType<MainMenuUI>();
        if (mainMenu != null && mainMenu.MenuUI != null)
        {
            return mainMenu.MenuUI.activeSelf;
        }
        return false;
    }

    private void SetCursorState(CursorPriority priority)
    {
        switch (priority)
        {
            case CursorPriority.Gameplay:
                Cursor.lockState = CursorLockMode.Locked; // Khóa con trỏ cho gameplay
                Cursor.visible = false; // Ẩn con trỏ
                break;

            case CursorPriority.PauseMenu:
            case CursorPriority.DeathScreen:
            case CursorPriority.MainMenu:
                Cursor.lockState = CursorLockMode.None; // Mở khóa con trỏ
                Cursor.visible = true; // Hiển thị con trỏ
                break;
        }
    }

    public void RefreshCursorState()
    {
        CursorPriority newPriority = GetActiveHighestPriority();
        currentPriority = newPriority;
        SetCursorState(newPriority);
    }

    // ✨ Helper methods
    /// <summary>
    /// Kiểm tra xem con trỏ có đang bị khóa (gameplay mode) không
    /// </summary>
    public bool IsCursorLocked()
    {
        return Cursor.lockState == CursorLockMode.Locked;
    }

    /// <summary>
    /// Get priority hiện tại
    /// </summary>
    public CursorPriority GetCurrentPriority()
    {
        return currentPriority;
    }
}