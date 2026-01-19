using Mirror;
using UnityEngine;
using Unity.Cinemachine;
using System.Collections;
using BSS.PoseBlender;

public class PlayerItemUseHandler : NetworkBehaviour
{
    [Header("References - Tham chiếu")]
    [SerializeField] private PlayerHoldingItem playerHoldingItem;
    [SerializeField] public PlayerCombat playerCombat;
    [SerializeField] private PlayerAnimator playerAnimator;
    [SerializeField] private PlayerStatManager playerStats;
    [SerializeField] private GameInput gameInput;
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerIKController ikController;
    [SerializeField] private PlayableAnimationBlender playableAnimationBlender;
    [SerializeField] private PlayerPlaySound playSound;
    [SerializeField] private PlayerCameraManager playerCameraManager;
    [SerializeField] private CrosshairManager crosshairManager;

    // Properties public
    public PlayerHoldingItem PlayerHoldingItem => playerHoldingItem;
    public PlayerCombat PlayerCombat => playerCombat;
    public PlayerAnimator PlayerAnimator => playerAnimator;

    // Biến private
    private IItemUseStrategy currentStrategy;
    private float useStartTime;
    private bool isUsing = false;
    private bool isAiming = false;
    private Transform currentArrowSpawnPoint;

    public bool isCharging = false;

    // Lưu target IK cho cung
    private Transform currentBowIKTarget;
    private Transform currentBowStringOverride;
    private Transform currentBowRightHintOverride;

    // ===========================================================
    // Khởi tạo cho local player
    // ===========================================================
    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();

        gameInput = FindObjectOfType<GameInput>();

        if (playerStats == null)
            playerStats = GetComponent<PlayerStatManager>();

        if (animator == null)
            animator = GetComponent<Animator>();

        if (ikController == null)
        {
            ikController = GetComponent<PlayerIKController>();
        }

        if (playerCameraManager == null)
        {
            playerCameraManager = GetComponent<PlayerCameraManager>();
            if (playerCameraManager == null)
            {
                Debug.LogError("[PlayerItemUseHandler] Không tìm thấy PlayerCameraManager!");
            }
        }

        if (crosshairManager == null)
        {
            crosshairManager = FindObjectOfType<CrosshairManager>();
            if (crosshairManager == null)
            {
                Debug.LogWarning("[PlayerItemUseHandler] Không tìm thấy CrosshairManager trong scene!");
            }
        }

        if (gameInput != null)
        {
            gameInput.gameObject.SetActive(true);

            gameInput.OnAttackStarted += HandleUseStarted;
            gameInput.OnAttackCanceled += HandleUseCanceled;
            gameInput.OnAimStarted += HandleAimStarted;
            gameInput.OnAimCanceled += HandleAimCanceled;
        }
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

    // ===========================================================
    // Update - Xử lý logic hàng frame
    // ===========================================================
    public void UpdatePlayerItemUse(float deltaTime)
    {
        if (!isLocalPlayer) return;

        // Nếu đang sử dụng item, gọi strategy OnUseHeld
        if (isUsing && currentStrategy != null)
        {
            float heldTime = Time.time - useStartTime;
            currentStrategy.OnUseHeld(playerHoldingItem.ItemData, this, heldTime);

            // Update crosshair dựa trên charge time
            UpdateCrosshairBasedOnCharge(heldTime);
        }
    }

    // Hàm update crosshair dựa trên thời gian charge
    private void UpdateCrosshairBasedOnCharge(float chargeTime)
    {
        // Thêm kiểm tra: phải đang aim VÀ đang charge
        if (crosshairManager == null || !isCharging || !isAiming) return;

        var itemData = playerHoldingItem.ItemData;
        if (itemData == null || itemData.weapon == null) return;

        // Chỉ update cho cung
        if (itemData.weapon.weaponType != WeaponType.Bow) return;

        // Lấy bow string controller để tính charge percent
        BowStringController bowString = playerHoldingItem.GetComponentInChildren<BowStringController>();
        if (bowString != null)
        {
            ProjectileData projectileData = bowString.GetProjectileData();
            if (projectileData != null)
            {
                // Sử dụng phương thức từ ProjectileData
                float chargePercent = projectileData.CalculateChargePercent(chargeTime);

                // Update crosshair
                crosshairManager.UpdateCharge(chargePercent);
            }
        }
    }

    // ===========================================================
    // EVENT HANDLERS - Xử lý các event từ input
    // ===========================================================

    private void HandleUseStarted(object sender, System.EventArgs e)
    {
        if (!isLocalPlayer) return;

        var itemData = playerHoldingItem.ItemData;
        if (itemData == null)
        {
            Debug.Log($"[{name}] Không có item trong tay");
            return;
        }

        currentStrategy = ItemUseStrategyFactory.GetStrategy(itemData);
        if (currentStrategy == null)
        {
            Debug.LogWarning($"[{name}] Không tìm thấy strategy cho {itemData.itemName}");
            return;
        }

        isUsing = true;
        isCharging = true;
        useStartTime = Time.time;

        // Chỉ hiển thị crosshair khi: Đang cầm cung + Đang aim + Bắt đầu charge
        if (crosshairManager != null &&
            itemData.weapon != null &&
            itemData.weapon.weaponType == WeaponType.Bow &&
            isAiming)
        {
            crosshairManager.ShowCrosshair();
            playSound.PlayArrowCharge();
            Debug.Log("[PlayerItemUseHandler] Crosshair displayed - Aiming + charging");
        }

        currentStrategy.OnUseStarted(itemData, this);
        Debug.Log($"[PlayerItemUseHandler] Started using {itemData.itemName}");
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

        // Ẩn crosshair khi thả (bắn hoặc cancel)
        if (crosshairManager != null && itemData.weapon.weaponType == WeaponType.Bow)
        {
            crosshairManager.HideCrosshair();
          
            Debug.Log("[PlayerItemUseHandler] Crosshair hidden - Released");
        }

        Debug.Log($"[PlayerItemUseHandler] Released use");
    }

    private void HandleAimStarted(object sender, System.EventArgs e)
    {
        if (!isLocalPlayer) return;

        var itemData = playerHoldingItem.ItemData;
        if (itemData == null || itemData.weapon == null) return;

        // Chỉ cho phép aim với vũ khí Bow
        if (itemData.weapon.weaponType != WeaponType.Bow) return;

        currentStrategy = ItemUseStrategyFactory.GetStrategy(itemData);
        if (currentStrategy == null) return;

        isAiming = true;

        // KHÔNG hiển thị crosshair khi chỉ aim - chỉ hiển thị khi BẮT ĐẦU CHARGE
        // Crosshair sẽ được hiển thị trong HandleUseStarted (khi nhấn attack)

        // Chuyển sang camera aim (priority-based, smooth blend)
      

        // Setup IK cho cung
        if (itemData.weapon.weaponType == WeaponType.Bow && currentBowStringOverride != null)
        {
            Debug.Log($"[PlayerItemUseHandler] Aiming with override: {currentBowStringOverride.name}");
        }

        currentStrategy.OnAimStarted(itemData, this);
        Debug.Log("[PlayerItemUseHandler] Started aiming (crosshair hidden until charge)");
    }

    private void HandleAimCanceled(object sender, System.EventArgs e)
    {
        if (!isLocalPlayer || !isAiming) return;

        var itemData = playerHoldingItem.ItemData;
        if (itemData == null || currentStrategy == null) return;

        // Chỉ xử lý aim cancel cho vũ khí Bow
        if (itemData.weapon == null || itemData.weapon.weaponType != WeaponType.Bow) return;

        isAiming = false;

        // Ẩn crosshair nếu đang hiển thị
        if (crosshairManager != null && crosshairManager.IsVisible())
        {
            crosshairManager.HideCrosshair();
            Debug.Log("[PlayerItemUseHandler] Crosshair hidden - Stopped aiming");
        }


        currentStrategy.OnAimReleased(itemData, this);

        if (ikController != null)
        {
            // Tắt IK
        }
    }

    // ===========================================================
    // AIMING HELPERS
    // ===========================================================

    public void SetBowIKTarget(Transform ikTarget)
    {
        if (ikTarget != null)
        {
            currentBowIKTarget = ikTarget;
            Debug.Log($"[PlayerItemUseHandler] Bow IK static target saved: {ikTarget.name}");
        }
    }

    public void SetBowStringOverride(Transform stringBone)
    {
        if (stringBone != null)
        {
            currentBowStringOverride = stringBone;
            Debug.Log($"[PlayerItemUseHandler] Bow string override saved: {stringBone.name}");
        }
    }

    public void SetBowRightHintOverride(Transform hintOverride)
    {
        if (hintOverride != null)
        {
            currentBowRightHintOverride = hintOverride;
            Debug.Log($"[PlayerItemUseHandler] Bow right hint override saved: {hintOverride.name}");
        }
    }

    public void OnItemSwitched()
    {
        if (isUsing || isAiming)
        {
            CancelCurrentAction();

           
        }

        // Ẩn crosshair khi đổi item
        if (crosshairManager != null && crosshairManager.IsVisible())
        {
            crosshairManager.HideCrosshair();
        }

        currentStrategy = null;
        isUsing = false;
        isAiming = false;
        isCharging = false;
        currentBowIKTarget = null;
        currentBowStringOverride = null;
        currentBowRightHintOverride = null;
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

        // Ẩn crosshair khi cancel
        if (crosshairManager != null && crosshairManager.IsVisible())
        {
            crosshairManager.HideCrosshair();
        }

        // Camera will smoothly blend back to normal
       

        isUsing = false;
        isAiming = false;
        isCharging = false;
    }

    // ===========================================================
    // NETWORK COMMANDS
    // ===========================================================

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
        }

        RpcPlayEatAnimation();
    }

    [Command]
    public void CmdFireArrow(int itemId, float chargePercent, Vector3 clientAimDirection, Vector3 clientSpawnPos)
    {
        ItemData itemData = ItemDatabase.Get(itemId);
        if (itemData == null || itemData.weapon == null) return;

        BowStringController bowString = playerHoldingItem.GetComponentInChildren<BowStringController>();
        ProjectileData projectileData = bowString != null ? bowString.GetProjectileData() : null;

        if (projectileData == null || projectileData.projectilePrefab == null)
        {
            return;
        }

        var stats = projectileData.GetProjectileStats(chargePercent);
        float damage = stats.damage;
        float speed = stats.speed;

        Vector3 spawnPos = clientSpawnPos;
        Vector3 shootDir = clientAimDirection.normalized;
        Quaternion spawnRot = Quaternion.LookRotation(shootDir) * Quaternion.Euler(0, 180f, 0);

        GameObject arrowObj = Instantiate(projectileData.projectilePrefab, spawnPos, spawnRot);
        NetworkServer.Spawn(arrowObj);

        Vector3 velocity = shootDir * speed;

        ArrowProjectile arrow = arrowObj.GetComponent<ArrowProjectile>();
        if (arrow != null)
        {
            arrow.Initialize(gameObject, velocity, chargePercent, itemId, projectileData);
        }
        else
        {
            NetworkServer.Destroy(arrowObj);
        }
        playSound.PlayArrowRelease();
        RpcPlayBowRelease();
    }

    public void SetUpGameInput(GameInput gameinput)
    {
        gameInput = gameinput;
    }

    public void SetCrosshairManager(CrosshairManager manager)
    {
        crosshairManager = manager;
        Debug.Log($"[PlayerItemUseHandler] CrosshairManager assigned: {manager.gameObject.name}");
    }

    // ===========================================================
    // CLIENT RPCs
    // ===========================================================

    [ClientRpc]
    private void RpcPlayEatAnimation() { }

    [ClientRpc]
    private void RpcPlayBowRelease()
    {
        playerAnimator.TriggerReleaseBow();
    }

    // ===========================================================
    // GETTERS & SETTERS
    // ===========================================================

    public bool IsAiming() => isAiming;

    public bool IsCharging() => isCharging;

    public void SetArrowSpawnPoint(Transform t)
    {
        currentArrowSpawnPoint = t;
    }
}