using Mirror;
using UnityEngine;

public class PlayerAnimator : NetworkBehaviour
{
    // Tên các parameter trong Animator
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

    private const string IS_TREE = "isTree";
    private const string IS_ROCK = "isRock";
    private const string IS_ATTACKING = "Attack";

    [Header("Player blend speed")]
    [SerializeField] private float playerSpeed;

    [Header("Setting for movement")]
    [SerializeField] private float playerSpeedMin = 0f;
    [SerializeField] private float playerSpeedMax = 1f;
    [SerializeField] private float playerSpeedAcceleration = 3f;

    [Header("References")]
    [SerializeField] private Player player;
    [SerializeField] private PlayerInteract playerInteract;
    [SerializeField] private PlayerHoldingItem playerholdingItem;
    [SerializeField] private PlayerCombat playerCombat;

    [SyncVar(hook = nameof(OnMiningChanged))]
    private bool isMining;

    private Animator animator;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        if (animator == null)
            Debug.LogError("Animator chưa được gán trên PlayerAnimator!");

        // Kiểm tra các reference, cảnh báo nếu chưa gán
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
    }

    private void Update()
    {

        if (!isLocalPlayer) return;
        if (animator == null || player == null) return; // Nếu thiếu component quan trọng thì bỏ qua

        HandleMoveAnimation();

        // Cập nhật các trạng thái animation với null check
        animator.SetBool(IS_FALLING, player.IsFalling());
        animator.SetBool(IS_WALKING, player.IsWalking());
        animator.SetFloat(BLEND_SPEED, playerSpeed);
        animator.SetBool(IS_GROUNDED, player.IsGrounded());
        animator.SetBool(IS_SPRINTING, player.IsSprinting());


        if (playerInteract != null)
        {
            CmdSetMining(playerInteract.IsMining());
            animator.SetBool(IS_ROCK, playerInteract.IsRock());
            animator.SetBool(IS_TREE, playerInteract.IsTree());
        }

        if (playerholdingItem != null) {
            animator.SetBool(IS_HOLDING, playerholdingItem.IsHolding());
            animator.SetBool(IS_WEAPON,playerholdingItem.IsAWeapon());
                }

        //if (playerCombat != null)
        //    animator.SetInteger(COMBO_STEP, playerCombat.CurrentCombo());
    }

    // Kích hoạt trigger nhảy
    public void TriggerJump()
    {
        if (animator != null)
            animator.SetTrigger(IS_JUMPING);
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

    // Xử lý blend speed khi di chuyển
    private void HandleMoveAnimation()
    {
        if (player == null) return;

        // Nếu đang chạy, tăng tốc độ blend speed
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
