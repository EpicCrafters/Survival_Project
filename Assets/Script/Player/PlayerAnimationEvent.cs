using UnityEngine;

public class PlayerAnimationEvents : MonoBehaviour
{
    [SerializeField] private PlayerHoldingItem holding;
    [SerializeField] private PlayerCombat combat;
   

    private void Awake()
    {
        // Auto-find components if not assigned - SEARCH IN PARENT
        if (holding == null)
            holding = GetComponentInParent<PlayerHoldingItem>();

        if (combat == null)
            combat = GetComponentInParent<PlayerCombat>();  // ✅ Changed to GetComponentInParent

        // Verify references
        if (holding == null)
            Debug.LogError("[PlayerAnimationEvents] PlayerHoldingItem not found!");
        if (combat == null)
            Debug.LogError("[PlayerAnimationEvents] PlayerCombat not found!");
    }

   
    public void EnableHitbox()
    {
        Debug.Log("[PlayerAnimationEvents] EnableHitbox called");

        if (holding == null)
        {
            Debug.LogError("[PlayerAnimationEvents] holding is NULL!");
            return;
        }

        GameObject heldItem = holding.GetCurrentHeldObject();
        if (heldItem != null)
        {
            ItemHitBox hitbox = heldItem.GetComponentInChildren<ItemHitBox>();
            if (hitbox != null)
            {
                hitbox.EnableHitbox();
                Debug.Log("[PlayerAnimationEvents] ✅ Hitbox enabled");
            }
            else
            {
                Debug.LogWarning("[PlayerAnimationEvents] ⚠️ No ItemHitBox found on held item");
            }
        }
        else
        {
            Debug.LogWarning("[PlayerAnimationEvents] ⚠️ No held item to enable hitbox on");
        }
    }

  
    public void DisableHitbox()
    {
        Debug.Log("[PlayerAnimationEvents] DisableHitbox called");

        if (holding == null)
        {
            Debug.LogError("[PlayerAnimationEvents] holding is NULL!");
            return;
        }

        GameObject heldItem = holding.GetCurrentHeldObject();
        if (heldItem != null)
        {
            ItemHitBox hitbox = heldItem.GetComponentInChildren<ItemHitBox>();
            if (hitbox != null)
            {
                hitbox.DisableHitbox();
                Debug.Log("[PlayerAnimationEvents] ✅ Hitbox disabled");
            }
        }
    }


   

    /// </summary>
    public void EndAttack()
    {
        Debug.Log("[PlayerAnimationEvents] EndAttack called");

        if (combat == null)
        {
            Debug.LogError("[PlayerAnimationEvents] combat is NULL!");
            return;
        }

        combat.EndAttack();
    }


    public void AttackStarted()
    {
        combat.AttackStarted();
    }
    public void AllowItemSwitch()
    {
        Debug.Log("[PlayerAnimationEvents] AllowItemSwitch called");

        if (combat == null)
        {
            Debug.LogError("[PlayerAnimationEvents] combat is NULL!");
            return;
        }
        combat.EndAttack();
        combat.AllowItemSwitch();
    }

    public void DebugEvent()
    {
        combat.DebugEvent();
    }
    // ========== Legacy Support (if old animation events exist) ==========
    public void OpenComboWindow()
    {
        Debug.Log("[PlayerAnimationEvents] OpenComboWindow called (legacy - does nothing)");
        if (combat != null) combat.OpenComboWindow();
    }

    public void CloseComboWindow()
    {
        Debug.Log("[PlayerAnimationEvents] CloseComboWindow called (legacy - does nothing)");
        if (combat != null) combat.CloseComboWindow();
    }

    public void EndCombo()
    {
        Debug.Log("[PlayerAnimationEvents] EndCombo called (legacy - redirecting to EndAttack)");
        if (combat != null) combat.EndCombo();
    }
}