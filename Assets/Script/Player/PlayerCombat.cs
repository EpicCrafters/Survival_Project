using Mirror;
using UnityEngine;

public class PlayerCombat : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private GameInput gameInput;
    [SerializeField] private PlayerAnimator playerAnimator;
    [SerializeField] private PlayerHoldingItem playerHoldingItem;
    [SerializeField] private WeaponAnimatorHandler weaponHandler;

    private bool isAttacking = false;
    private bool isReadyToAttack = true;
    private int comboStep = 0;
    private bool canCombo = false;
    private bool queuedAttack = false;

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();

        gameInput = GetComponentInChildren<GameInput>(true);
        if (gameInput != null)
        {
            gameInput.gameObject.SetActive(true);
            gameInput.OnAttack += HandleAttackInput;
            Debug.Log($"[{name}] LocalPlayer input enabled.");
        }
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        Debug.Log($"OnStartClient: name={gameObject.name} netId={netId} owner={isLocalPlayer} animator={playerAnimator?.gameObject.name}");
    }

    public override void OnStopLocalPlayer()
    {
        if (gameInput != null)
            gameInput.OnAttack -= HandleAttackInput;
    }

    private void HandleAttackInput(object sender, System.EventArgs e)
    {
        if (!isLocalPlayer) return;
        if (!isReadyToAttack) return;
        if (playerHoldingItem.IsAWeapon())
        {
            CmdDoAttack();
        }
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

    // ------------------ Damage Command (Called by ItemHitBox) ------------------
    [Command(requiresAuthority = false)]
    public void CmdDealDamage(uint targetNetId, int damage, Vector3 hitPoint, Vector3 hitNormal, int itemId,
        float knockbackHorizontal, float boneSearchRadius, bool enableKnockback)
    {
        // Use Mirror's NetworkServer to find spawned objects
        if (!NetworkServer.spawned.TryGetValue(targetNetId, out NetworkIdentity targetIdentity))
        {
            Debug.LogWarning($"[Server] Target netId {targetNetId} not found in spawned objects");
            return;
        }

        // Find the IDamageable component
        IDamageable target = targetIdentity.GetComponent<IDamageable>();
        if (target == null)
            target = targetIdentity.GetComponentInChildren<IDamageable>();

        if (target == null)
        {
            Debug.LogWarning($"[Server] No IDamageable found on {targetIdentity.name}");
            return;
        }

        // Get item data for HitInfo
        ItemData itemData = ItemDatabase.Get(itemId);
        if (itemData == null)
        {
            Debug.LogWarning($"[Server] ItemData with id {itemId} not found");
            return;
        }

        // Create HitInfo with knockback data
        Vector3 hitDirection = (targetIdentity.transform.position - transform.position).normalized;
        HitInfo hit = new HitInfo(hitPoint, hitNormal, hitDirection, gameObject, itemData);

        // Apply damage
        target.Damage(damage, hit);

        // If target will die and is HFSMController, send knockback via RPC
        if (target is HFSMController aiController && enableKnockback && aiController.IsDead())
        {
            aiController.RpcApplyDeathKnockback(hitPoint, hitDirection, knockbackHorizontal,  boneSearchRadius);
        }

        Debug.Log($"[Server] {name} dealt {damage} damage to {targetIdentity.name} with {itemData.itemName}");
    }

    // ------------------ Animation event callbacks ------------------
    public void OpenComboWindow()
    {
        canCombo = true;
        if (queuedAttack)
        {
            queuedAttack = false;
            if (isLocalPlayer)
            {
                Debug.Log($"[{name}] Queued attack executed.");
                CmdDoAttack();
            }
        }
    }

    public void CloseComboWindow() => canCombo = false;

    public void EndCombo()
    {
        comboStep = 0;
        isAttacking = false;
        isReadyToAttack = true;
        canCombo = false;
        queuedAttack = false;
        Debug.Log($"[{name}] Combo ended.");
    }
}