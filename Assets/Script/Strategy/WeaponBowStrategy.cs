using UnityEngine;

public class WeaponBowStrategy : IItemUseStrategy
{
    private bool isCharging = false;     // Đang giữ chuột trái để kéo dây cung
    private bool isAiming = false;       // Đang giữ chuột phải để ngắm
    private const float MAX_CHARGE_TIME = 1.5f; // Thời gian kéo hết dây (100%)
    private BowStringController bowController;  // Controller điều khiển dây cung

    // ============================================================
    // XỬ LÝ CHUỘT TRÁI: BẮT ĐẦU SỬ DỤNG / CHARGE CUNG
    // ============================================================
    public void OnUseStarted(ItemData itemData, PlayerItemUseHandler handler)
    {
        // Chỉ được kéo dây khi đang ngắm (hold chuột phải)
        if (!isAiming)
        {
            Debug.Log("[Bow] Must aim (right-click) before charging!");
            return;
        }

        // Lấy object đang cầm để tìm BowStringController
        GameObject heldObject = handler.PlayerHoldingItem?.GetCurrentHeldObject();
        if (heldObject != null)
        {
            bowController = heldObject.GetComponentInChildren<BowStringController>();
            if (bowController != null)
            {
                // Bắt đầu kéo dây cung
                bowController.StartDrawing();
                isCharging = true;

                // IK KHÔNG được bật ở đây → Aim đã bật IK rồi
                Debug.Log("[Bow] Started charging (hold left mouse)");
            }
            else
            {
                Debug.LogWarning("[Bow] Can't find BowStringController!");
            }
        }
    }

    // ============================================================
    // CHUỘT TRÁI GIỮ LIÊN TỤC: TĂNG LỰC KÉO (CHARGE)
    // ============================================================
    public void OnUseHeld(ItemData itemData, PlayerItemUseHandler handler, float heldTime)
    {
        if (!isCharging || bowController == null)
            return;

        // Tính % lực kéo (0 → 1)
        float chargePercent = Mathf.Clamp01(heldTime / MAX_CHARGE_TIME);

        // Cập nhật độ giãn dây cung theo % charge
        bowController.UpdateDrawAmount(chargePercent);
    }

    // ============================================================
    // CHUỘT TRÁI NHẢ → BẮN
    // ============================================================
    public void OnUseReleased(ItemData itemData, PlayerItemUseHandler handler, float heldTime)
    {
        if (!isCharging)
            return;

        float chargePercent = Mathf.Clamp01(heldTime / MAX_CHARGE_TIME);

        // Nếu lực kéo đủ để bắn
        if (chargePercent >= 0.2f)
        {
            // ✅ CALCULATE AIM ON CLIENT BEFORE SENDING TO SERVER
            Vector3 clientAimDir = Vector3.forward;
            Vector3 clientSpawnPos = handler.transform.position;

            if (bowController != null)
            {
                // Get realistic aim data from client's camera
                var aimData = bowController.GetRealisticArrowSpawnData();
                clientSpawnPos = aimData.spawnPos;
                clientAimDir = aimData.shootDir;

                Debug.Log($"[Bow] CLIENT calculated aim: pos={clientSpawnPos}, dir={clientAimDir}");

                // Animation thả dây
                bowController.Release();
            }

            // ✅ Send to server with CLIENT's aim data
            handler.CmdFireArrow(itemData.id, chargePercent, clientAimDir, clientSpawnPos);

            Debug.Log($"[Bow] Released arrow at {chargePercent * 100}% charge");
        }
        else
        {
            // Nếu lực kéo quá ít → hủy
            if (bowController != null)
                bowController.CancelDraw();

            Debug.Log("[Bow] Charge too low, cancelled");
        }

        // Không tắt IK ở đây vì vẫn đang Aim chuột phải
        isCharging = false;
        bowController = null;
    }

    // ============================================================
    // HỦY CHARGE (khi mất chuột phải hoặc bị interrupt)
    // ============================================================
    public void OnUseCancelled(ItemData itemData, PlayerItemUseHandler handler)
    {
        if (bowController != null)
        {
            // Reset dây cung
            bowController.CancelDraw();
            bowController = null;
        }

        // IK vẫn không tắt ở đây nếu vẫn đang giữ chuột phải
        isCharging = false;

        Debug.Log("[Bow] Charge cancelled");
    }

    // ============================================================
    // CHUỘT PHẢI BẮT ĐẦU → NGẮM (AIM)
    // ============================================================
    public void OnAimStarted(ItemData itemData, PlayerItemUseHandler handler)
    {
        isAiming = true;

        // Bật camera/animation/UI ngắm
        handler.EnableBowAiming(true);

        // IK được bật từ bên PlayerItemUseHandler → không bật ở đây
        Debug.Log("[Bow] Aiming started (hold right mouse)");
    }

    // ============================================================
    // CHUỘT PHẢI NHẢ → THOÁT NGẮM (STOP AIM)
    // ============================================================
    public void OnAimReleased(ItemData itemData, PlayerItemUseHandler handler)
    {
        isAiming = false;

        // Tắt camera/animation/UI ngắm
        handler.EnableBowAiming(false);

        // IK được tắt ở PlayerItemUseHandler → không can thiệp ở đây

        // Nếu đang kéo dây mà bị nhả Aim → cancel luôn
        if (isCharging)
        {
            OnUseCancelled(itemData, handler);
        }

        Debug.Log("[Bow] Aiming stopped");
    }
}