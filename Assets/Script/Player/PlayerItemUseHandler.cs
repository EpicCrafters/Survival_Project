using Mirror;
using UnityEngine;
using Unity.Cinemachine;
using System.Collections;
using BSS.PoseBlender;

public class PlayerItemUseHandler : NetworkBehaviour
{
    [Header("References - Tham chiếu")]
    [SerializeField] private PlayerHoldingItem playerHoldingItem;
    [SerializeField] private PlayerCombat playerCombat;
    [SerializeField] private PlayerAnimator playerAnimator;
    [SerializeField] private PlayerStatManager playerStats;
    [SerializeField] private GameInput gameInput;
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerIKController ikController;
    [SerializeField] private PlayableAnimationBlender playableAnimationBlender;

    [Header("Bow Aiming - Ngắm cung")]
    [SerializeField] private CinemachineVirtualCamera aimCamera;
    [SerializeField] private GameObject crosshair;
    [SerializeField] private float normalFOV = 60f;
    [SerializeField] private float aimFOV = 40f;

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

    public bool isCharging { get; private set; } = false;

    // Lưu CẢ target tĩnh VÀ override động
    private Transform currentBowIKTarget;          // Target tĩnh (tay trái - cầm cung)
    private Transform currentBowStringOverride;    // Override động (tay phải - kéo dây)
    private Transform currentBowRightHintOverride; // ✅ Hint động cho tay phải (hướng khuỷu tay)

    // ===========================================================
    // Khởi tạo cho local player
    // ===========================================================
    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();

        if (playerStats == null)
            playerStats = GetComponent<PlayerStatManager>();

        if (animator == null)
            animator = GetComponent<Animator>();

        if (ikController == null)
        {
            ikController = GetComponent<PlayerIKController>();
            if (ikController == null)
            {
                ikController.SetLeftHandIKEnabled(false);
            }
        }

        gameInput = GetComponentInChildren<GameInput>(true);
        if (gameInput != null)
        {
            gameInput.gameObject.SetActive(true);

            // Subscribe các event từ input
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

    // ===========================================================
    // Update - Xử lý logic hàng frame
    // ===========================================================
    private void Update()
    {
        if (!isLocalPlayer) return;

        // Nếu đang sử dụng item, gọi strategy OnUseHeld
        if (isUsing && currentStrategy != null)
        {
            float heldTime = Time.time - useStartTime;
            currentStrategy.OnUseHeld(playerHoldingItem.ItemData, this, heldTime);
        }
    }

    // ===========================================================
    // EVENT HANDLERS - Xử lý các event từ input
    // ===========================================================

    /// <summary>
    /// Xử lý khi bắt đầu sử dụng item (nhấn attack)
    /// </summary>
    private void HandleUseStarted(object sender, System.EventArgs e)
    {
        if (!isLocalPlayer) return;

        var itemData = playerHoldingItem.ItemData;
        if (itemData == null)
        {
            Debug.Log($"[{name}] Không có item trong tay");
            return;
        }

        // Lấy strategy phù hợp với item type
        currentStrategy = ItemUseStrategyFactory.GetStrategy(itemData);
        if (currentStrategy == null)
        {
            Debug.LogWarning($"[{name}] Không tìm thấy strategy cho {itemData.itemName}");
            return;
        }

        isUsing = true;
        isCharging = true;
        useStartTime = Time.time;
        currentStrategy.OnUseStarted(itemData, this);
        Debug.Log($"[PlayerItemUseHandler] Bắt đầu sử dụng {itemData.itemName}");
    }

    /// <summary>
    /// Xử lý khi thả attack (release)
    /// </summary>
    private void HandleUseCanceled(object sender, System.EventArgs e)
    {
        if (!isLocalPlayer || !isUsing) return;

        var itemData = playerHoldingItem.ItemData;
        if (itemData == null || currentStrategy == null) return;

        isCharging = false;
        float heldTime = Time.time - useStartTime;
        currentStrategy.OnUseReleased(itemData, this, heldTime);
        isUsing = false;
        Debug.Log($"[PlayerItemUseHandler] Đã thả sử dụng");
    }

    /// <summary>
    /// Xử lý khi bắt đầu aim (chuột phải)
    /// </summary>
    private void HandleAimStarted(object sender, System.EventArgs e)
    {
        if (!isLocalPlayer) return;

        var itemData = playerHoldingItem.ItemData;
        if (itemData == null || itemData.weapon == null) return;

        currentStrategy = ItemUseStrategyFactory.GetStrategy(itemData);
        if (currentStrategy == null) return;

        isAiming = true;

        // Sử dụng override ĐỘNG từ dây cung cho bow, không phải target tĩnh
        if (itemData.weapon.weaponType == WeaponType.Bow && currentBowStringOverride != null)
        {
            // Enable left hand IK (cầm cung)
            ikController.SetLeftHandIKEnabled(true);
            ikController.SetLeftHandIKWeight(1f);

            // Enable right hand IK với override động (kéo dây)
            ikController.SetRightHandIKTarget(currentBowStringOverride);
            ikController.SetRightHandIKWeight(1f);
            ikController.SetRightHandIKEnabled(true);

            // ✅ Setup right hand pole hint override (hướng khuỷu tay)
           
                ikController.SetRightHandPoleHintOverride(currentBowRightHintOverride);
             
            

            Debug.Log($"[PlayerItemUseHandler] ✅ Aiming với override: {currentBowStringOverride.name}");
        }

        currentStrategy.OnAimStarted(itemData, this);
        EnableBowAiming(true);
    }

    /// <summary>
    /// Xử lý khi ngừng aim
    /// </summary>
    private void HandleAimCanceled(object sender, System.EventArgs e)
    {
        if (!isLocalPlayer || !isAiming) return;

        var itemData = playerHoldingItem.ItemData;
        if (itemData == null || currentStrategy == null) return;

        // Tắt left hand IK
        ikController.SetLeftHandIKEnabled(false);
        ikController.SetLeftHandIKWeight(0f);

        isAiming = false;
        currentStrategy.OnAimReleased(itemData, this);

        // Tắt right hand IK
        if (ikController != null)
        {
            ikController.SetRightHandIKEnabled(false);
            ikController.SetRightHandIKWeight(0f);
            //ikController.SetRightHandPoleHintOverride(null); // ✅ Clear hint override
        }

        EnableBowAiming(false);
    }

    // ===========================================================
    // AIMING HELPERS
    // ===========================================================

    /// <summary>
    /// Bật/tắt chế độ ngắm cung
    /// </summary>
    public void EnableBowAiming(bool enable)
    {
        if (aimCamera != null)
        {
            aimCamera.Priority = enable ? 11 : 9;
            aimCamera.m_Lens.FieldOfView = enable ? aimFOV : normalFOV;
        }

        if (crosshair != null)
            crosshair.SetActive(enable);
    }

    // ===========================================================
    // BOW IK SETTERS - Lưu các transform từ cung
    // ===========================================================

    /// <summary>
    /// Set IK target tĩnh cho cung (tay trái cầm cung)
    /// </summary>
    public void SetBowIKTarget(Transform ikTarget)
    {
        if (ikTarget != null)
        {
            currentBowIKTarget = ikTarget;
            Debug.Log($"[PlayerItemUseHandler] ✅ Bow IK static target đã lưu: {ikTarget.name}");
        }
    }

    /// <summary>
    /// Set override động cho dây cung (tay phải kéo dây)
    /// </summary>
    public void SetBowStringOverride(Transform stringBone)
    {
        if (stringBone != null)
        {
            currentBowStringOverride = stringBone;
            Debug.Log($"[PlayerItemUseHandler] ✅ Bow string override đã lưu: {stringBone.name}");
        }
    }

    /// <summary>
    /// ✅ Set hint override động cho tay phải (hướng khuỷu tay)
    /// </summary>
    public void SetBowRightHintOverride(Transform hintOverride)
    {
        if (hintOverride != null)
        {
            currentBowRightHintOverride = hintOverride;
            Debug.Log($"[PlayerItemUseHandler] ✅ Bow right hint override đã lưu: {hintOverride.name}");
        }
    }

    /// <summary>
    /// Được gọi khi đổi item - reset tất cả state
    /// </summary>
    public void OnItemSwitched()
    {
        if (isUsing || isAiming)
        {
            CancelCurrentAction();
        }

        currentStrategy = null;
        isUsing = false;
        isAiming = false;
        isCharging = false;
        currentBowIKTarget = null;
        currentBowStringOverride = null;
        currentBowRightHintOverride = null; // ✅ Clear hint override

        EnableBowAiming(false);
    }

    /// <summary>
    /// Hủy action hiện tại
    /// </summary>
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
    }

    // ===========================================================
    // NETWORK COMMANDS - Gửi lên server
    // ===========================================================

    /// <summary>
    /// Command để tiêu thụ thức ăn
    /// </summary>
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

    /// <summary>
    /// Command để bắn mũi tên
    /// </summary>
    [Command]
    public void CmdFireArrow(int itemId, float chargePercent, Vector3 clientAimDirection, Vector3 clientSpawnPos)
    {
        // Tắt IK tay phải tạm thời khi bắn
        if (isLocalPlayer)
            StartCoroutine(DisableRightHandIKTemporarily(1f));

        ItemData itemData = ItemDatabase.Get(itemId);
        if (itemData == null || itemData.weapon == null) return;

        // Lấy bow string controller để truy cập projectile data
        BowStringController bowString = playerHoldingItem.GetComponentInChildren<BowStringController>();
        ProjectileData projectileData = bowString != null ? bowString.GetProjectileData() : null;

        if (projectileData == null || projectileData.projectilePrefab == null)
        {
            return;
        }

        // Tính stats dựa trên charge percent từ ProjectileData
        var stats = projectileData.GetProjectileStats(chargePercent);
        float damage = stats.damage;
        float speed = stats.speed;

        // Sử dụng dữ liệu aim từ client (đã được tính toán sẵn ở client)
        Vector3 spawnPos = clientSpawnPos;
        Vector3 shootDir = clientAimDirection.normalized;
        Quaternion spawnRot = Quaternion.LookRotation(shootDir) * Quaternion.Euler(0, 180f, 0);

        // Spawn projectile prefab từ ProjectileData
        GameObject arrowObj = Instantiate(projectileData.projectilePrefab, spawnPos, spawnRot);
        NetworkServer.Spawn(arrowObj);

        Vector3 velocity = shootDir * speed;

        // Khởi tạo arrow với charge percent và projectile data
        ArrowProjectile arrow = arrowObj.GetComponent<ArrowProjectile>();
        if (arrow != null)
        {
            arrow.Initialize(gameObject, velocity, chargePercent, itemId, projectileData);
        }
        else
        {
            NetworkServer.Destroy(arrowObj);
        }

        RpcPlayBowRelease();
    }

    /// <summary>
    /// Coroutine để tắt right hand IK tạm thời (khi bắn)
    /// </summary>
    private IEnumerator DisableRightHandIKTemporarily(float duration)
    {
        // Tắt Aim Pose blend
        if (playableAnimationBlender != null)
        {
            playableAnimationBlender.BlendOverlay("Aim Pose", 0f);
        }

        // Tắt right hand IK
        if (ikController != null)
        {
            ikController.SetRightHandIKWeight(0f);
            ikController.SetRightHandIKEnabled(false);
        }

        yield return new WaitForSeconds(duration);

        // Bật lại nếu vẫn đang aim
        if (ikController != null && isAiming)
        {
            ikController.SetRightHandIKWeight(1f);
            ikController.SetRightHandIKEnabled(true);
        }
    }

    // ===========================================================
    // CLIENT RPCs - Server gọi tới tất cả client
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

    public void SetArrowSpawnPoint(Transform t)
    {
        currentArrowSpawnPoint = t;
    }
}