using UnityEngine;

public interface IItemUseStrategy
{
    void OnUseStarted(ItemData itemData, PlayerItemUseHandler handler);
    void OnUseHeld(ItemData itemData, PlayerItemUseHandler handler, float heldTime);
    void OnUseReleased(ItemData itemData, PlayerItemUseHandler handler, float heldTime);
    void OnUseCancelled(ItemData itemData, PlayerItemUseHandler handler);

    void OnAimStarted(ItemData itemData, PlayerItemUseHandler handler);
    void OnAimReleased(ItemData itemData, PlayerItemUseHandler handler);
}