using UnityEngine;

public class PlayerAnimationEvents : MonoBehaviour
{
    [SerializeField] private PlayerHoldingItem holding;
    [SerializeField] private PlayerCombat combat;


    public void EnableHitbox()
    {
        Debug.Log("Animation Event: Enable Hitbox called");
        GameObject heldItem = holding.GetCurrentHeldObject();
        if (heldItem != null)
        {
            ItemHitBox hitbox = heldItem.GetComponentInChildren<ItemHitBox>();
            if (hitbox != null)
            {
                hitbox.EnableHitbox();
            }
         
            
        }
    }

    public void DisableHitbox()
    {

        Debug.Log("Animation Event: Disable Hitbox called");
        GameObject heldItem = holding.GetCurrentHeldObject();
        if (heldItem != null)
        {
            ItemHitBox hitbox = heldItem.GetComponentInChildren<ItemHitBox>();
            if (hitbox != null)
            {
                hitbox.DisableHitbox();
            }
        }
    }
    public void OpenComboWindow() => combat.OpenComboWindow();
    public void CloseComboWindow() => combat.CloseComboWindow();
    public void EndCombo() => combat.EndCombo();
}
