using UnityEngine;

public class ConsumableStrategy : IItemUseStrategy
{
    public void OnUseStarted(ItemData itemData, PlayerItemUseHandler handler)
    {
        Debug.Log($"[ConsumableStrategy] OnUseStarted called for {itemData.itemName}");

        // Check if player can eat (hunger not full)
        var statManager = handler.GetComponent<PlayerStatManager>();
        if (statManager == null)
        {
            Debug.LogError("[Food] No PlayerStatManager found on player");
            return;
        }

        if (!statManager.CanEat())
        {
            Debug.Log("[Food] ❌ Cannot eat - hunger bar is full!");
            // Optional: Show UI message to player
            return;
        }

        // Get inventory components from SystemManager
        var systemManager = SystemManager.Instance;
        if (systemManager == null)
        {
            Debug.LogError("[Food] SystemManager.Instance is null!");
            return;
        }

        var inventoryInput = systemManager.GetComponentInChildren<InventoryInput>();
        if (inventoryInput == null)
        {
            Debug.LogError("[Food] No InventoryInput found in SystemManager");
            return;
        }

        // Get InventoryData from the player
        var inventoryData = handler.GetComponentInChildren<InventoryData>();
        if (inventoryData == null)
        {
            Debug.LogError("[Food] No InventoryData found on player");
            return;
        }

        handler.CmdConsumeFood(itemData.id);
        Debug.Log("[Food] ✓ CmdConsumeFood called");

        // Remove from inventory
        inventoryInput.RequestConsumeHeldItem(1);
        Debug.Log("[Food] ✓ RequestConsumeHeldItem called");

        // Optional: Play eating animation
        // handler.PlayerAnimator?.TriggerEat();

        Debug.Log($"[Food] ✅ Consumed {itemData.itemName} successfully!");
    }

    public void OnUseHeld(ItemData itemData, PlayerItemUseHandler handler, float heldTime)
    {
        // Consumables don't need hold behavior
    }

    public void OnUseReleased(ItemData itemData, PlayerItemUseHandler handler, float heldTime)
    {
        // No release action for consumables
    }

    public void OnUseCancelled(ItemData itemData, PlayerItemUseHandler handler)
    {
        // Handle interruption if needed
    }

    public void OnAimStarted(ItemData itemData, PlayerItemUseHandler handler)
    {
        // Consumables don't have aim
    }

    public void OnAimReleased(ItemData itemData, PlayerItemUseHandler handler)
    {
        // Consumables don't have aim
    }
}