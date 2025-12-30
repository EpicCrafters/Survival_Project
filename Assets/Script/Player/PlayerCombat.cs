using Mirror;
using UnityEngine;

public class PlayerCombat : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private GameInput gameInput;
    [SerializeField] private PlayerAnimator playerAnimator;
    [SerializeField] private PlayerHoldingItem playerHoldingItem;
    [SerializeField] private WeaponAnimatorHandler weaponHandler;

    [Header("Combat Settings")]
    [Tooltip("Time after attack starts before player can switch items")]
    [SerializeField] private float itemSwitchLockoutTime = 0.5f;

    // Combat state
    public bool isAttacking = false;
    public bool isReadyToAttack = true;
    private float attackStartTime = 0f;
    public bool canSwitchItems = true;

    // Combo variables (commented for future use)
    // private int comboStep = 0;
    // private bool canCombo = false;
    // private bool queuedAttack = false;

    public override void OnStartClient()
    {
        base.OnStartClient();
    }



    /// <summary>
    /// Attempt to perform an attack. Called by input handler.
    /// </summary>
    public void TryAttack()
    {
        if (!isLocalPlayer) return;
        if (!playerHoldingItem.IsAWeapon()) return;

        // Only attack if ready (prevents animation reset)
        if (isReadyToAttack)
        {
            PerformAttack();
        }
        else
        {
            Debug.Log($"[{name}] Attack ignored - still attacking");
        }
    }

    /// <summary>
    /// Execute the attack
    /// </summary>
    private void PerformAttack()
    {
        isReadyToAttack = false;
        isAttacking = true;
        attackStartTime = Time.time;
        canSwitchItems = false;

        CmdDoAttack();

        Debug.Log($"[{name}] Performing attack");
    }



    /// <summary>
    /// Check if player can currently switch items
    /// </summary>
    public bool CanSwitchItems()
    {
        return canSwitchItems;
    }

    /// <summary>
    /// Check if player is currently attacking
    /// </summary>
    public bool IsAttacking()
    {
        return isAttacking;
    }

    // ------------------ Mirror Networking ------------------
    [Command]
    private void CmdDoAttack()
    {
        RpcPlayAttack();
    }

    [ClientRpc]
    private void RpcPlayAttack()
    {
        playerAnimator.TriggerAttack();
    }

    // ------------------ Damage Commands ------------------
    [Command(requiresAuthority = false)]
    public void CmdDealDamage(uint targetNetId, int damage, Vector3 hitPoint, Vector3 hitNormal, int itemId,
        float knockbackHorizontal, float boneSearchRadius, bool enableKnockback)
    {
        if (!NetworkServer.spawned.TryGetValue(targetNetId, out NetworkIdentity targetIdentity))
        {
            Debug.LogWarning($"[Server] Target netId {targetNetId} not found in spawned objects");
            return;
        }

        IDamageable target = targetIdentity.GetComponent<IDamageable>();
        if (target == null)
            target = targetIdentity.GetComponentInChildren<IDamageable>();

        if (target == null)
        {
            Debug.LogWarning($"[Server] No IDamageable found on {targetIdentity.name}");
            return;
        }

        ItemData itemData = ItemDatabase.Get(itemId);
        if (itemData == null)
        {
            Debug.LogWarning($"[Server] ItemData with id {itemId} not found");
            return;
        }

        Vector3 hitDirection = (targetIdentity.transform.position - transform.position).normalized;
        HitInfo hit = new HitInfo(hitPoint, hitNormal, hitDirection, gameObject, itemData);

        target.Damage(damage, hit);

        if (target is HFSMController aiController && enableKnockback && aiController.IsDead())
        {
            aiController.RpcApplyDeathKnockback(hitPoint, hitDirection, knockbackHorizontal, boneSearchRadius);
        }

        Debug.Log($"[Server] {name} dealt {damage} damage to {targetIdentity.name} with {itemData.itemName}");
    }

    [Command]
    public void CmdDamageResource(string uniqueId, int damage, int toolId, ResourceType resourceType)
    {
        if (string.IsNullOrEmpty(uniqueId)) return;

        ResourceManager rm = ResourceManager.GetManagerForUniqueId(uniqueId);
        if (rm == null)
        {
            Debug.LogWarning($"[PlayerCombat] No ResourceManager found for uniqueId: {uniqueId}");
            return;
        }

        if (rm.core.TryGetRecord(uniqueId, out var record))
        {
            int newHealth = record.curHealth - damage;

            if (newHealth <= 0)
            {
                GiveMiningRewards(connectionToClient.identity, resourceType, toolId);
                rm.ApplyResourceStateChange(uniqueId, true, 0, ResourceChangeSource.Network);
            }
            else
            {
                rm.ApplyResourceStateChange(uniqueId, record.isChopped, newHealth, ResourceChangeSource.Network);
            }

            Debug.Log($"[PlayerCombat] Resource {uniqueId} damaged: {damage} -> health {newHealth}");

            ResourceManagerRouter router = FindObjectOfType<ResourceManagerRouter>();
            if (router != null)
            {
                router.BroadcastResourceChange(uniqueId, record.isChopped, newHealth);
            }
            else
            {
                Debug.LogWarning("[PlayerCombat] No ResourceManagerRouter found to broadcast resource change!");
            }
        }
        else
        {
            Debug.LogWarning($"[PlayerCombat] No record found for uniqueId: {uniqueId}");
        }
    }

    [Command]
    public void CmdDamageNonPersistent(uint targetNetId, int damage, Vector3 hitPoint, Vector3 hitNormal, int itemId)
    {
        if (!NetworkServer.spawned.TryGetValue(targetNetId, out NetworkIdentity targetIdentity))
        {
            Debug.LogWarning($"[Server] Target netId {targetNetId} not found in spawned objects");
            return;
        }

        IDamageable target = targetIdentity.GetComponent<IDamageable>();
        if (target == null)
            target = targetIdentity.GetComponentInChildren<IDamageable>();

        if (target == null)
        {
            Debug.LogWarning($"[Server] No IDamageable found on {targetIdentity.name}");
            return;
        }

        ItemData itemData = ItemDatabase.Get(itemId);
        if (itemData == null)
        {
            Debug.LogWarning($"[Server] ItemData with id {itemId} not found");
            return;
        }

        Vector3 hitDirection = (targetIdentity.transform.position - transform.position).normalized;
        HitInfo hit = new HitInfo(hitPoint, hitNormal, hitDirection, gameObject, itemData);

        target.Damage(damage, hit);

        Debug.Log($"[Server] {name} dealt {damage} damage to non-persistent resource {targetIdentity.name}");
    }

    private void GiveMiningRewards(NetworkIdentity player, ResourceType resourceType, int toolId)
    {
        Debug.Log($"[PlayerCombat] Granting rewards for mining {resourceType} with tool {toolId}");
        // Add your reward logic here
    }

    // ------------------ Animation Event Callbacks ------------------

    public void DebugEvent()
    {
        Debug.Log("Event call from player combat work");
    }

    public void AttackStarted()
    {
        canSwitchItems = false;
    }
    /// <summary>
    /// Called by animation event - marks end of attack sequence
    /// MUST BE PUBLIC for animation events to call it
    /// </summary>
    public void EndAttack()
    {
        isAttacking = false;
        isReadyToAttack = true;
        canSwitchItems = true;

        Debug.Log($"[PlayerCombat] EndAttack called - Ready for new attack");
    }

    /// <summary>
    /// Called by animation event - allows item switching mid-attack
    /// MUST BE PUBLIC for animation events to call it
    /// </summary>
    public void AllowItemSwitch()
    {
        canSwitchItems = true;
        Debug.Log($"[PlayerCombat] AllowItemSwitch called - Can switch items now");
    }

    // Legacy support (if your old animation events use these names)
    public void OpenComboWindow() { }
    public void CloseComboWindow() { }
    public void EndCombo()
    {

    }

    // ========== COMBO SYSTEM (For future implementation) ==========
    /*
    public void OpenComboWindow()
    {
        canCombo = true;
        Debug.Log($"[{name}] Combo window opened");
        
        if (queuedAttack)
        {
            queuedAttack = false;
            if (isLocalPlayer)
            {
                Debug.Log($"[{name}] Executing queued attack");
                PerformAttack();
            }
        }
    }

    public void CloseComboWindow()
    {
        canCombo = false;
        Debug.Log($"[{name}] Combo window closed");
    }

    public void EndCombo()
    {
        comboStep = 0;
        isAttacking = false;
        isReadyToAttack = true;
        canCombo = false;
        queuedAttack = false;
        canSwitchItems = true;
        
        Debug.Log($"[{name}] Combo ended - Ready for new attack");
    }
    */
}