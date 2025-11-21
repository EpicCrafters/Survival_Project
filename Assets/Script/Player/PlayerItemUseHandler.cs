using Mirror;
using UnityEngine;
using Unity.Cinemachine;

public class PlayerItemUseHandler : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerHoldingItem playerHoldingItem;
    [SerializeField] private PlayerCombat playerCombat;
    [SerializeField] private PlayerAnimator playerAnimator;
    [SerializeField] private PlayerStatManager playerStats;
    [SerializeField] private GameInput gameInput;
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerIKController ikController; // ✅ Add this
    [SerializeField] private PlayableAnimationBlender playableAnimationBlender;

    [Header("Bow Aiming")]
    [SerializeField] private CinemachineVirtualCamera aimCamera;
    [SerializeField] private GameObject crosshair;
    [SerializeField] private float normalFOV = 60f;
    [SerializeField] private float aimFOV = 40f;

    public PlayerHoldingItem PlayerHoldingItem => playerHoldingItem;
    public PlayerCombat PlayerCombat => playerCombat;
    public PlayerAnimator PlayerAnimator => playerAnimator;

    private IItemUseStrategy currentStrategy;
    private float useStartTime;
    private bool isUsing = false;
    private bool isAiming = false;
    private Transform currentArrowSpawnPoint;

    public bool isCharging { get; private set; } = false;
    private Transform currentBowIKTarget;

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();

        if (playerStats == null)
            playerStats = GetComponent<PlayerStatManager>();

        if (animator == null)
            animator = GetComponent<Animator>();

        // ✅ Get IK Controller
        if (ikController == null)
        {
            ikController = GetComponent<PlayerIKController>();
            if (ikController == null)
            {
                Debug.LogError("[PlayerItemUseHandler] PlayerIKController not found!");
            }
            else
            {
                Debug.Log("[PlayerItemUseHandler] ✅ IK Controller found");
            }
        }

        gameInput = GetComponentInChildren<GameInput>(true);
        if (gameInput != null)
        {
            gameInput.gameObject.SetActive(true);
            gameInput.OnAttackStarted += HandleUseStarted;
            gameInput.OnAttackCanceled += HandleUseCanceled;
            gameInput.OnAimStarted += HandleAimStarted;
            gameInput.OnAimCanceled += HandleAimCanceled;
        }

        if (crosshair != null)
            crosshair.SetActive(false);
    }

    public override void OnStopLocalPlayer()
    {
        if (gameInput != null)
        {
            gameInput.OnAttackStarted -= HandleUseStarted;
            gameInput.OnAttackCanceled -= HandleUseCanceled;
            gameInput.OnAimStarted -= HandleAimStarted;
            gameInput.OnAimCanceled -= HandleAimCanceled;
        }
    }

    private void Update()
    {
        if (!isLocalPlayer) return;

        if (isUsing && currentStrategy != null)
        {
            float heldTime = Time.time - useStartTime;
            currentStrategy.OnUseHeld(playerHoldingItem.ItemData, this, heldTime);
        }
    }

    // ✅ REMOVED OnAnimatorIK - now handled by PlayerIKController

    private void HandleUseStarted(object sender, System.EventArgs e)
    {
        if (!isLocalPlayer) return;

        var itemData = playerHoldingItem.ItemData;
        if (itemData == null)
        {
            Debug.Log($"[{name}] No item held");
            return;
        }

        currentStrategy = ItemUseStrategyFactory.GetStrategy(itemData);
        if (currentStrategy == null)
        {
            Debug.LogWarning($"[{name}] No strategy found for {itemData.itemName}");
            return;
        }

        isUsing = true;
        isCharging = true;
        useStartTime = Time.time;
        currentStrategy.OnUseStarted(itemData, this);
        Debug.Log($"[PlayerItemUseHandler] Use started for {itemData.itemName}");
    }

    private void HandleUseCanceled(object sender, System.EventArgs e)
    {
        if (!isLocalPlayer || !isUsing) return;

        var itemData = playerHoldingItem.ItemData;
        if (itemData == null || currentStrategy == null) return;
        isCharging = false;
        float heldTime = Time.time - useStartTime;
        currentStrategy.OnUseReleased(itemData, this, heldTime);
        isUsing = false;
        Debug.Log($"[PlayerItemUseHandler] Use released");
    }

    private void HandleAimStarted(object sender, System.EventArgs e)
    {
        if (!isLocalPlayer) return;

        var itemData = playerHoldingItem.ItemData;
        if (itemData == null || itemData.weapon == null) return;

        currentStrategy = ItemUseStrategyFactory.GetStrategy(itemData);
        if (currentStrategy == null) return;

        isAiming = true;

        // ✅ Enable Right Hand IK for bow
        if (itemData.weapon.weaponType == WeaponType.Bow && currentBowIKTarget != null)
        {
            ikController.SetRightHandIKTarget(currentBowIKTarget);
            ikController.SetRightHandIKEnabled(true);
        }

        currentStrategy.OnAimStarted(itemData, this);
        EnableBowAiming(true);
    }

    private void HandleAimCanceled(object sender, System.EventArgs e)
    {
        if (!isLocalPlayer || !isAiming) return;

        var itemData = playerHoldingItem.ItemData;
        if (itemData == null || currentStrategy == null) return;

        isAiming = false;
        currentStrategy.OnAimReleased(itemData, this);

        if (ikController != null)
            ikController.SetRightHandIKEnabled(false);

        EnableBowAiming(false);
    }

    public void EnableBowAiming(bool enable)
    {
        if (aimCamera != null)
        {
            aimCamera.Priority = enable ? 11 : 9;
            aimCamera.m_Lens.FieldOfView = enable ? aimFOV : normalFOV;
        }

        if (crosshair != null)
            crosshair.SetActive(enable);

        //Debug.Log($"[Bow Aiming] {(enable ? "Enabled" : "Disabled")}");
    }

    public void SetBowIKTarget(Transform ikTarget)
    {
        if (ikTarget != null)
        {
            currentBowIKTarget = ikTarget;
            //Debug.Log($"[PlayerItemUseHandler] ✅ Bow IK target stored: {ikTarget.name}");
        }
        else
        {
            //Debug.LogError("[PlayerItemUseHandler] SetBowIKTarget called with NULL!");
        }
    }

    public void OnItemSwitched()
    {
        //Debug.Log("[PlayerItemUseHandler] OnItemSwitched called");

        if (isUsing || isAiming)
        {
            CancelCurrentAction();
        }

        currentStrategy = null;
        isUsing = false;
        isAiming = false;
        isCharging = false;
        currentBowIKTarget = null;

        // ✅ Clear IK via controller
        if (ikController != null)
        {
            // ikController.ClearHandIK();
        }

        EnableBowAiming(false);
    }

    public void CancelCurrentAction()
    {
        if (currentStrategy != null)
        {
            var itemData = playerHoldingItem.ItemData;
            if (itemData != null)
            {
                currentStrategy.OnUseCancelled(itemData, this);
            }
        }

        isUsing = false;
        isAiming = false;
        isCharging = false;
        // ✅ Clear IK via controller
        if (ikController != null)
        {
            //ikController.ClearHandIK();
        }
    }

    [Command]
    public void CmdConsumeFood(int itemId)
    {
        ItemData itemData = ItemDatabase.Get(itemId);
        if (itemData == null || itemData.consumable == null) return;

        PlayerStatManager stats = GetComponent<PlayerStatManager>();
        if (stats != null)
        {
            float hungerRestore = itemData.consumable.hungerRestoreAmount;
            stats.ChangeHunger(hungerRestore);

            if (itemData.consumable.healAmount > 0)
                stats.Heal(itemData.consumable.healAmount);

            //Debug.Log($"[Server] {name} consumed {itemData.itemName}");
        }

        RpcPlayEatAnimation();
    }
    [Command]
    public void CmdFireArrow(int itemId, float chargePercent)
    {
        ItemData itemData = ItemDatabase.Get(itemId);
        if (itemData == null || itemData.weapon == null) return;

        float baseDamage = itemData.weapon.damage;
        float damage = baseDamage * (0.5f + chargePercent * 0.5f);
        float speed = 20f + (chargePercent * 10f);

        GameObject arrowPrefab = itemData.weapon.arrowProjectilePrefab;
        if (arrowPrefab == null)
        {
            Debug.LogError($"[CmdFireArrow] No arrow prefab assigned to {itemData.itemName}!");
            return;
        }

        // -----------------------------------
        // USE the arrow spawn point if assigned
        // -----------------------------------
        Vector3 spawnPos;
        Quaternion spawnRot;

        if (currentArrowSpawnPoint != null)
        {
            spawnPos = currentArrowSpawnPoint.position;
            spawnRot = currentArrowSpawnPoint.rotation;
        }
        else
        {
            spawnPos = transform.position + transform.forward * 1.2f;
            spawnRot = transform.rotation;
        }

        // Spawn arrow on server
        GameObject arrowObj = Instantiate(arrowPrefab, spawnPos, spawnRot);
        NetworkServer.Spawn(arrowObj);

        // Shoot direction = local Y axis of arrowSpawnPoint
        Vector3 shootDir = spawnRot * Vector3.up;

        // Apply speed
        Vector3 velocity = shootDir * speed;

        ArrowProjectile arrow = arrowObj.GetComponent<ArrowProjectile>();
        if (arrow != null)
        {
            arrow.Initialize(gameObject, velocity, damage, itemId);
        }
        else
        {
            Debug.LogError("[CmdFireArrow] ArrowProjectile not found!");
            NetworkServer.Destroy(arrowObj);
        }

        RpcPlayBowRelease();
    }


    [ClientRpc]
    private void RpcPlayEatAnimation() { }

    [ClientRpc]
    private void RpcPlayBowRelease() { playableAnimationBlender.PlayAction("ArrowRelease", 0f); }

    public bool IsAiming() => isAiming;
    public void SetArrowSpawnPoint(Transform t)
    {
        currentArrowSpawnPoint = t;
    }
}