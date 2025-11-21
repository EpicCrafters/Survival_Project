public class WeaponMeleeStrategy : IItemUseStrategy
{
    public void OnUseStarted(ItemData itemData, PlayerItemUseHandler handler)
    {
        handler.PlayerCombat?.TryAttack();
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