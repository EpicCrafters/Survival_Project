using UnityEngine;

public class WeaponBowStrategy : IItemUseStrategy
{
    private bool isCharging = false;
    private bool isAiming = false;
    private const float MAX_CHARGE_TIME = 1.5f;

    // ============================================================
    // CHUỘT PHẢI BẮT ĐẦU → NGẮM (AIM)
    // ============================================================
    public void OnAimStarted(ItemData itemData, PlayerItemUseHandler handler)
    {
        isAiming = true;
        Debug.Log("[Bow] Aiming started (hold right mouse)");
    }

    // ============================================================
    // CHUỘT TRÁI BẮT ĐẦU → CHARGE (sử dụng cached controller)
    // ============================================================
    public void OnUseStarted(ItemData itemData, PlayerItemUseHandler handler)
    {
        // Chỉ được kéo dây khi đang ngắm
        if (!isAiming)
        {
            Debug.Log("[Bow] Must aim (right-click) before charging!");
            return;
        }

        // ✅ Get cached bow controller from PlayerHoldingItem
        BowStringController bowController = handler.PlayerHoldingItem?.GetBowController();

        if (bowController != null)
        {
            bowController.StartDrawing();
            isCharging = true;
            Debug.Log("[Bow] Started charging (hold left mouse)");
        }
        else
        {
            Debug.LogWarning("[Bow] Bow controller not found!");
        }
    }

    // ============================================================
    // CHUỘT TRÁI GIỮ → TĂNG LỰC KÉO
    // ============================================================
    public void OnUseHeld(ItemData itemData, PlayerItemUseHandler handler, float heldTime)
    {
        if (!isCharging)
            return;

        BowStringController bowController = handler.PlayerHoldingItem?.GetBowController();
        if (bowController == null)
            return;

        float chargePercent = Mathf.Clamp01(heldTime / MAX_CHARGE_TIME);
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

        if (chargePercent >= 0.2f)
        {
            // ✅ Use cached controller for aim calculation
            BowStringController bowController = handler.PlayerHoldingItem?.GetBowController();

            Vector3 clientAimDir = Vector3.forward;
            Vector3 clientSpawnPos = handler.transform.position;

            if (bowController != null)
            {
                var aimData = bowController.GetRealisticArrowSpawnData();
                clientSpawnPos = aimData.spawnPos;
                clientAimDir = aimData.shootDir;

                Debug.Log($"[Bow] CLIENT calculated aim: pos={clientSpawnPos}, dir={clientAimDir}");

                bowController.Release();
            }
            else
            {
                Debug.LogWarning("[Bow] Bow controller is null during fire!");
            }

            handler.CmdFireArrow(itemData.id, chargePercent, clientAimDir, clientSpawnPos);
            Debug.Log($"[Bow] Released arrow at {chargePercent * 100}% charge");
        }
        else
        {
            BowStringController bowController = handler.PlayerHoldingItem?.GetBowController();
            if (bowController != null)
                bowController.CancelDraw();

            Debug.Log("[Bow] Charge too low, cancelled");
        }

        isCharging = false;
    }

    // ============================================================
    // HỦY CHARGE
    // ============================================================
    public void OnUseCancelled(ItemData itemData, PlayerItemUseHandler handler)
    {
        BowStringController bowController = handler.PlayerHoldingItem?.GetBowController();
        if (bowController != null)
        {
            bowController.CancelDraw();
        }

        isCharging = false;
        Debug.Log("[Bow] Charge cancelled");
    }

    // ============================================================
    // CHUỘT PHẢI NHẢ → THOÁT NGẮM
    // ============================================================
    public void OnAimReleased(ItemData itemData, PlayerItemUseHandler handler)
    {
        isAiming = false;

        // Cancel charging if still active
        if (isCharging)
        {
            OnUseCancelled(itemData, handler);
        }

        Debug.Log("[Bow] Aiming stopped");
    }
}