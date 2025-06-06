using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class Player : MonoBehaviour
{
    public static Player Instance { get; private set; }




    
    public enum MovementState { Idle, Walk, Sprint, Falling }
    public MovementState state;

    [Header("References")]
    [SerializeField] private GameInput gameInput;
    [SerializeField] private PlayerAnimator playerAnimator;

    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed;
    [SerializeField] private float sprintSpeed;
    [SerializeField] private float rotationSpeed;
    [SerializeField] private float acceleration;
    [SerializeField] private float jumpForce;
    [SerializeField] private float jumpCooldown;
    [SerializeField] private float gravityMultiplier;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundDistance;
    [SerializeField] private LayerMask groundMask;

    [Header("Slope Handling")]
    [SerializeField] private float maxSlopeAngle;

    [Header("Debug Info")]
    [SerializeField] private bool showInventory;

    private Rigidbody rb;
    private bool isGrounded;
    private bool wasGrounded = false;
    private bool isFalling;
    private bool isWalking;
    private bool isSprinting;
    private bool canJump = true;
    public bool isOnSlope = false;

    private bool isChopping = false;

    private float currentSpeed;
    private float maxSpeed;
    private float playerHeight = 1f;

    private Vector3 moveDir;
    private RaycastHit slopeHit;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        maxSpeed = walkSpeed;

        gameInput.OnSprintStarted += GameInput_OnSprintStarted;
        gameInput.OnSprintCanceled += GameInput_OnSprintCanceled;
        gameInput.OnJump += GameInput_OnJump;
        gameInput.OnShowInventory += GameInput_OnShowInventory;
        gameInput.OnAttack += GameInput_OnAttack;


        gameInput.OnInteractStarted += GameInput_OnInteractStarted;

        gameInput.OnInteractFinished += GameInput_OnInteractFinished;

        gameInput.OnDropItem += GameInput_OnDropItem;
        
    }

    private void GameInput_OnDropItem(object sender, System.EventArgs e)
    {
        
    }

    private void GameInput_OnInteractFinished(object sender, System.EventArgs e)
    {
        isChopping = false;
    }

    private void GameInput_OnInteractStarted(object sender, System.EventArgs e)
    {
        isChopping = true;
    }

    private void GameInput_OnAttack(object sender, System.EventArgs e)
    {
        playerAnimator.TriggerAttack();
    }

    private void Update()
    {
        Instance = this;

        HandleMovement();
        CheckIfGrounded();
        CheckIfFalling();
        UpdateState();
        GravityAdjustment();

        
    }

    // ─────────────────────────────────────────────
    // Input Events
    private void GameInput_OnSprintStarted(object sender, System.EventArgs e) => isSprinting = true;
    private void GameInput_OnSprintCanceled(object sender, System.EventArgs e) => isSprinting = false;
    private void GameInput_OnJump(object sender, System.EventArgs e) => TriggerJump();
   
    private void GameInput_OnShowInventory(object sender, System.EventArgs e) => showInventory = !showInventory;


    
   
    // ─────────────────────────────────────────────
    // Movement
    private void HandleMovement()
    {
        Vector2 inputVector = gameInput.GetMovementVector();

        Transform cam = Camera.main.transform;
        Vector3 camForward = cam.forward;
        Vector3 camRight = cam.right;
        camForward.y = 0f;
        camRight.y = 0f;

        moveDir = (camForward * inputVector.y + camRight * inputVector.x).normalized;
        isWalking = moveDir != Vector3.zero;

        if (isWalking)
        {
            currentSpeed = Mathf.MoveTowards(currentSpeed, maxSpeed, acceleration * Time.deltaTime);
            transform.forward = Vector3.Slerp(transform.forward, moveDir, rotationSpeed * Time.deltaTime);
        }
        else
        {
            currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, acceleration * Time.deltaTime);
        }

        MovePlayer();
    }

    private void MovePlayer()
    {
        Vector3 moveDirection = isGrounded && CheckSlope() ? GetSlopeMoveDirection() : moveDir;
        isOnSlope = CheckSlope();

        rb.MovePosition(rb.position + moveDirection * currentSpeed * Time.deltaTime);
    }

    private void TriggerJump()
    {
        if (canJump && isGrounded)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpForce, rb.linearVelocity.z);
            playerAnimator.TriggerJump();
        }
    }

    private void GravityAdjustment()
    {
        rb.AddForce(Vector3.up * Physics.gravity.y * (gravityMultiplier - 1f), ForceMode.Acceleration);
    }

    // ─────────────────────────────────────────────
    // State Checks
    private void CheckIfGrounded()
    {
        isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);

        if (isGrounded && !wasGrounded)
        {
            StartCoroutine(ResetJumpAfterDelay());
        }

        wasGrounded = isGrounded;
    }

    private void CheckIfFalling()
    {
        isFalling = rb.linearVelocity.y < -0.1f && !isGrounded;

        if (!isGrounded)
        {
            playerAnimator.ResetJumpTrigger();
            canJump = false;
        }
    }

    private void UpdateState()
    {
        if (isFalling)
        {
            state = MovementState.Falling;
            playerHeight = 1f;
            return;
        }

        if (isSprinting && isWalking)
        {
            state = MovementState.Sprint;
            maxSpeed = sprintSpeed;
        }
        else if (isWalking)
        {
            state = MovementState.Walk;
            maxSpeed = walkSpeed;
        }
        else
        {
            state = MovementState.Idle;
        }
    }

    // ─────────────────────────────────────────────
    // Slope Handling
    private bool CheckSlope()
    {
       
        if (Physics.Raycast(groundCheck.position, Vector3.down, out slopeHit, playerHeight * 0.5f+0.3f, groundMask))
        {
            
            float angle = Vector3.Angle(slopeHit.normal, Vector3.up);
            return angle > 0f && angle <= maxSlopeAngle;
        }

        return false;
    }

    private Vector3 GetSlopeMoveDirection()
    {
        return Vector3.ProjectOnPlane(moveDir, slopeHit.normal).normalized;
    }

    // ─────────────────────────────────────────────
    // Utilities
    IEnumerator ResetJumpAfterDelay()
    {
        yield return new WaitForSeconds(jumpCooldown);
        canJump = true;
    }

    // ─────────────────────────────────────────────
    // Public Getters
    public bool IsSprinting() => isSprinting;
    public bool IsFalling() => isFalling;
    public bool IsWalking() => isWalking;
    public bool IsGrounded() => isGrounded;
    public bool IsShowInventory() => showInventory;


    public bool IsChopping()=> isChopping;
}
