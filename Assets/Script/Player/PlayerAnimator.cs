using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

public class PlayerAnimator : MonoBehaviour
{
    private const string IS_WALKING = "isWalking";
    private const string IS_JUMPING = "triggerJumping";
    private const string blendSpeed = "blendSpeed";
    private const string IS_GROUNDED = "isGrounded";
    private const string IS_FALLING = "isFalling";
    private const string IS_SPRINTING = "isSprinting";

    private const string IS_ATTACKING = "Attack";
        
    private const string PLAYER = "Player";
    [Header("Player blend speed")]
    [SerializeField] private float playerSpeed;

    [Header("Setting for movement")]
    [SerializeField] private float playerSpeedMin=0f;
    [SerializeField] private float playerSpeedMax=1f;
    [SerializeField] private float playerSpeedAcceleration = 3f;


    [SerializeField] private Player player;
    private Animator animator;

    private void Awake()
    {
        animator = GetComponent<Animator>();    
    }

    private void Update()
    {

        HandleMoveAnimation();
        animator.SetBool(IS_FALLING, player.IsFalling());
        animator.SetBool(IS_WALKING, player.IsWalking());
        animator.SetFloat(blendSpeed,playerSpeed);
        animator.SetBool(IS_GROUNDED, player.IsGrounded());
        animator.SetBool(IS_SPRINTING, player.IsSprinting());

        


    }

    public void TriggerJump()
    {
        animator.SetTrigger(IS_JUMPING);
    }
    public void ResetJumpTrigger()
    {
        animator.ResetTrigger(IS_JUMPING);
    }

    public void TriggerAttack()
    {
        animator.SetTrigger(IS_ATTACKING);
    }
    public void ResetAttackTrigger()
    {
        animator.ResetTrigger(IS_ATTACKING);
    }

    private void HandleMoveAnimation()
    {

        if (player.IsSprinting())
        {
            playerSpeedMax = 1.5f;
        }
        else
        {
            playerSpeedMax = 1f;
        }

        if (player.IsWalking())
        {
            playerSpeed = Mathf.MoveTowards(playerSpeed,playerSpeedMax,playerSpeedAcceleration*Time.deltaTime);
        }
        else
        {
            playerSpeed = Mathf.MoveTowards(playerSpeed, playerSpeedMin, playerSpeedAcceleration * Time.deltaTime);
        }
    }
 

}
