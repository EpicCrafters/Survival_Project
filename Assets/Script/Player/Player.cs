using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class Player : MonoBehaviour
{
    public static Player Instance { get; private set; }

    [SerializeField] private PlayerInteract playerInteract;

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

    private CharacterController controller;
    [SerializeField] bool isGrounded;
    private bool wasGrounded = false;
    private bool isFalling;
    private bool isWalking;
    private bool isSprinting;
    private bool canJump = true;
    public bool isOnSlope = false;

    private float currentSpeed;
    private float maxSpeed;
    private float verticalVelocity;
   

    private Vector3 moveDir;
    private Vector2 inputVector;

    private void Start()
    {
        playerInteract = GetComponent<PlayerInteract>();
        controller = GetComponent<CharacterController>();
        maxSpeed = walkSpeed;

        gameInput.OnSprintStarted += GameInput_OnSprintStarted;
        gameInput.OnSprintCanceled += GameInput_OnSprintCanceled;
        gameInput.OnJump += GameInput_OnJump;
        gameInput.OnShowInventory += GameInput_OnShowInventory;
        gameInput.OnAttack += GameInput_OnAttack;
    }

    private void GameInput_OnAttack(object sender, System.EventArgs e) => playerAnimator.TriggerAttack();

    private void Update()
    {
        Instance = this;

        inputVector = gameInput.GetMovementVector();

        Transform cam = Camera.main.transform;
        Vector3 camForward = cam.forward;
        Vector3 camRight = cam.right;
        camForward.y = 0f;
        camRight.y = 0f;

        moveDir = (camForward * inputVector.y + camRight * inputVector.x).normalized;
        isWalking = moveDir != Vector3.zero;

        if (isWalking && isGrounded)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime * 100f);
        }

        CheckIfGrounded();
        CheckIfFalling();
        UpdateState();
        ApplyGravity();
        RotateTowardsCameraWhenInteracting();

        MovePlayer();
    }

    private void RotateTowardsCameraWhenInteracting()
    {
        if (playerInteract != null && playerInteract.IsMining())
        {
            Vector3 cameraForward = Camera.main.transform.forward;
            cameraForward.y = 0;

            if (cameraForward.sqrMagnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(cameraForward);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime * 100f);
            }
        }
    }

    private void GameInput_OnSprintStarted(object sender, System.EventArgs e) => isSprinting = true;
    private void GameInput_OnSprintCanceled(object sender, System.EventArgs e) => isSprinting = false;
    private void GameInput_OnJump(object sender, System.EventArgs e) => TriggerJump();
    private void GameInput_OnShowInventory(object sender, System.EventArgs e) => showInventory = !showInventory;

    private void MovePlayer()
    {
        float targetSpeed = isSprinting ? sprintSpeed : walkSpeed;
        currentSpeed = isWalking ? Mathf.MoveTowards(currentSpeed, targetSpeed, acceleration * Time.deltaTime) : 0f;

        Vector3 move = moveDir * currentSpeed;
        move.y = verticalVelocity;
        controller.Move(move * Time.deltaTime);
    }

    private void TriggerJump()
    {
        if (canJump && isGrounded)
        {
            verticalVelocity = Mathf.Sqrt(jumpForce * -2f * Physics.gravity.y);
            playerAnimator.TriggerJump();
        }
    }

    private void ApplyGravity()
    {
        if (isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }
        else
        {
            verticalVelocity += Physics.gravity.y * gravityMultiplier * Time.deltaTime;
        }
    }

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
        isFalling = verticalVelocity < -0.1f && !isGrounded;

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

    IEnumerator ResetJumpAfterDelay()
    {
        yield return new WaitForSeconds(jumpCooldown);
        canJump = true;
    }

    public bool IsSprinting() => isSprinting;
    public bool IsFalling() => isFalling;
    public bool IsWalking() => isWalking;
    public bool IsGrounded() => isGrounded;
    public bool IsShowInventory() => showInventory;
}
