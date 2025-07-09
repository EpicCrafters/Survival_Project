using UnityEngine;

public class PlayerAnimationEvents : MonoBehaviour
{
    [SerializeField] private PlayerHoldingItem holding;

    public void EnableHitbox()
    {
        
        GameObject heldItem = holding.GetCurrentHeldObject();
        if (heldItem != null)
        {
            ItemHitBox hitbox = heldItem.GetComponent<ItemHitBox>();
            if (hitbox != null)
            {
                hitbox.EnableHitbox();
            }
        }
    }

    public void DisableHitbox()
    {

        
        GameObject heldItem = holding.GetCurrentHeldObject();
        if (heldItem != null)
        {
            ItemHitBox hitbox = heldItem.GetComponent<ItemHitBox>();
            if (hitbox != null)
            {
                hitbox.DisableHitbox();
            }
        }
    }
}
