using Mirror;
using Mirror.BouncyCastle.Security.Certificates;
using UnityEngine;

public class PlayerAnimator : NetworkBehaviour
{
    // Tên các parameter trong Animator
    
    public float charge;
    private const string IS_WALKING = "isWalking";
    private const string IS_JUMPING = "triggerJumping";
    private const string BLEND_SPEED = "blendSpeed";
    private const string IS_GROUNDED = "isGrounded";
    private const string IS_FALLING = "isFalling";
    private const string IS_SPRINTING = "isSprinting";
    private const string IS_MINING = "isMining";
    private const string IS_HOLDING = "isHolding";
    private const string COMBO_STEP = "ComboStep";
    private const string IS_WEAPON = "isWeapon";
    private const string IS_AIMING = "isAiming";
    private const string IS_RELEASED = "ReleaseArrow";
    private const string IS_RANGEDWEAPON = "isRangedWeapon";
    private const string AIM_BLEND_X = "AimBlendX"; // For strafe left/right
    private const string AIM_BLEND_Y = "AimBlendY"; // For forward/backward

    private const string IS_TREE = "isTree";
    private const string IS_ROCK = "isRock";
    private const string IS_ATTACKING = "Attack";
    private const string AIM_WEIGHT = "AimWeight";

    [Header("Player blend speed")]
    [SerializeField] private float playerSpeed;
    [SerializeField] private float AimBlendSpeed;
    private float currentAimWeight = 0f;

    [Header("Aiming Blend Settings")]
    [SerializeField] private float aimBlendSpeed = 5f; // Speed of blend transition
    private float currentAimBlendX = 0f;
    private float currentAimBlendY = 0f;

    [Header("Setting for movement")]
    [SerializeField] private float playerSpeedMin = 0f;
    [SerializeField] private float playerSpeedMax = 1f;
    [SerializeField] private float playerSpeedAcceleration = 3f;

    [Header("References")]
    [SerializeField] private Player player;
    [SerializeField] private PlayerInteract playerInteract;
    [SerializeField] private PlayerHoldingItem playerholdingItem;
    [SerializeField] private PlayerCombat playerCombat;
    [SerializeField] private PlayerItemUseHandler playerItemUseHandler;
    [SerializeField] private PlayableAnimationBlender playableAnimationBlender;

    [SyncVar(hook = nameof(OnMiningChanged))]
    private bool isMining;

    private Animator animator;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        if (animator == null)
            Debug.LogError("Animator chưa được gán trên PlayerAnimator!");

        if (player == null)
            player = GetComponent<Player>();
        if (player == null)
            Debug.LogError("Player chưa được gán trên PlayerAnimator!");

        if (playerInteract == null)
            playerInteract = GetComponent<PlayerInteract>();
        if (playerholdingItem == null)
            playerholdingItem = GetComponent<PlayerHoldingItem>();
        if (playerCombat == null)
            playerCombat = GetComponent<PlayerCombat>();
        if (playerItemUseHandler == null)
            playerItemUseHandler = GetComponent<PlayerItemUseHandler>();
    }

    private void Update()
    {

        if (!isLocalPlayer) return;

        playableAnimationBlender.SetOverlayWeight("Charging Pose", 1f);
        playableAnimationBlender.SetOverlayPlaybackTime("Charging Pose", charge);
        if (animator == null || player == null) return;

        bool isAiming = playerItemUseHandler != null && playerItemUseHandler.IsAiming();

        // Handle different blend modes based on aiming state
        if (isAiming)
        {
            HandleAimingBlend();
            // Don't update normal blend speed when aiming
        }
        else
        {
            HandleMoveAnimation();
            // Reset aiming blends when not aiming
            currentAimBlendX = 0f;
            currentAimBlendY = 0f;
        }

        // Cập nhật các trạng thái animation
        animator.SetBool(IS_FALLING, player.IsFalling());
        animator.SetBool(IS_WALKING, player.IsWalking());

        // Only set BLEND_SPEED when NOT aiming
        if (!isAiming)
        {
            animator.SetFloat(BLEND_SPEED, playerSpeed);
        }

        animator.SetBool(IS_GROUNDED, player.IsGrounded());
        animator.SetBool(IS_SPRINTING, player.IsSprinting());

        // Set aiming blend values
        animator.SetFloat(AIM_BLEND_X, currentAimBlendX);
        animator.SetFloat(AIM_BLEND_Y, currentAimBlendY);

        if (playerInteract != null)
        {
            CmdSetMining(playerInteract.IsMining());
            animator.SetBool(IS_ROCK, playerInteract.IsRock());
            animator.SetBool(IS_TREE, playerInteract.IsTree());
        }

        if (playerholdingItem != null)
        {
            animator.SetBool(IS_HOLDING, playerholdingItem.IsHolding());
            animator.SetBool(IS_WEAPON, playerholdingItem.IsAWeapon());
            animator.SetBool(IS_RANGEDWEAPON, playerholdingItem.IsRangedWeapon());
        }

        if (playerItemUseHandler != null)
        {
            bool Aiming = playerItemUseHandler.IsAiming();
            bool isCharging = playerItemUseHandler.isCharging; // ✅ Check if charging

            animator.SetBool(IS_AIMING, Aiming);

            // Smooth blend for aim weight
            float target = Aiming ? 1f : 0f;
            currentAimWeight = Mathf.MoveTowards(currentAimWeight, target, AimBlendSpeed * Time.deltaTime);
            animator.SetFloat(AIM_WEIGHT, currentAimWeight);

            if (playableAnimationBlender != null)
            {
                if (Aiming)
                {
                    playableAnimationBlender.BlendOverlay("Aim Pose", 1f, 0.2f);
                }
                else
                {
                    playableAnimationBlender.BlendOverlay("Aim Pose", 0f, 0.2f);
                }

                // ✅ Blend charge overlay if charging
                if (isCharging&&isAiming)
                {
                    playableAnimationBlender.BlendOverlay("Charge Pose", 1f, 1f);
                }
                else
                {
                    playableAnimationBlender.BlendOverlay("Charge Pose", 0f);
                    

                   
                }
            }
        }

    }

    // Handle aiming movement blend (W = forward +1, S = backward -1, A/D = strafe)
    private void HandleAimingBlend()
    {
        if (player == null) return;

        Vector2 input = player.GetMovementInput();

      
        float targetBlendX = input.x; // Left/Right strafe
        float targetBlendY = input.y; // Forward/Backward

        // Smoothly transition to target blend values
        currentAimBlendX = Mathf.MoveTowards(currentAimBlendX, targetBlendX, aimBlendSpeed * Time.deltaTime);
        currentAimBlendY = Mathf.MoveTowards(currentAimBlendY, targetBlendY, aimBlendSpeed * Time.deltaTime);

        // DO NOT modify playerSpeed when aiming - we use AimBlendX/Y instead
    }

    // Kích hoạt trigger nhảy
    public void TriggerJump()
    {
        if (animator != null)
            animator.SetTrigger(IS_JUMPING);
    }
    //Kich hoat animation tha cung
    public void TriggerReleaseBow()
    {
        if(animator!=null)
            animator.SetTrigger(IS_RELEASED);
    }
    // Reset trigger nhảy
    public void ResetJumpTrigger()
    {
        if (animator != null)
            animator.ResetTrigger(IS_JUMPING);
    }

    // Kích hoạt trigger tấn công
    public void TriggerAttack()
    {
        if (animator != null)
            animator.SetTrigger(IS_ATTACKING);
    }

    // Reset trigger tấn công
    public void ResetAttackTrigger()
    {
        if (animator != null)
            animator.ResetTrigger(IS_ATTACKING);
    }

    // Xử lý blend speed khi di chuyển bình thường (không aim)
    private void HandleMoveAnimation()
    {
        if (player == null) return;

        playerSpeedMax = player.IsSprinting() ? 1.5f : 1f;

        if (player.IsWalking())
            playerSpeed = Mathf.MoveTowards(playerSpeed, playerSpeedMax, playerSpeedAcceleration * Time.deltaTime);
        else
            playerSpeed = Mathf.MoveTowards(playerSpeed, playerSpeedMin, playerSpeedAcceleration * Time.deltaTime);
    }

    private void OnMiningChanged(bool oldValue, bool newValue)
    {
        animator.SetBool(IS_MINING, newValue);
    }

    [Command]
    public void CmdSetMining(bool value)
    {
        isMining = value;
    }
}