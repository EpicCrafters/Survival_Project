using UnityEngine;

public class ConsumableStrategy : IItemUseStrategy
{
    public void OnUseStarted(ItemData itemData, PlayerItemUseHandler handler)
    {
        if (InventoryManager.instance.GetItemCount(itemData) <= 0)
        {
            Debug.Log($"[Food] No {itemData.itemName} left");
            return;
        }

        // Remove from inventory
        if (InventoryManager.instance.RemoveItem(itemData, 1))
        {
            // Restore hunger on server
            handler.CmdConsumeFood(itemData.id);

            // Play eating animation
            //handler.PlayerAnimator?.TriggerEat();

            Debug.Log($"[Food] Consumed {itemData.itemName}");
        }
    }

    public void OnUseHeld(ItemData itemData, PlayerItemUseHandler handler, float heldTime)
    {
        // Melee weapons don't need hold behavior
    }

    public void OnUseReleased(ItemData itemData, PlayerItemUseHandler handler, float heldTime)
    {
        // No release action for basic melee
    }

    public void OnUseCancelled(ItemData itemData, PlayerItemUseHandler handler)
    {
        // Handle interruption if needed
    }

    public void OnAimStarted(ItemData itemData, PlayerItemUseHandler handler)
    {
    }


    public void OnAimReleased(ItemData itemData, PlayerItemUseHandler handler)
    {

    }
}
