using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using Mirror;

public class Player : NetworkBehaviour
{
    [SerializeField] private PlayerInteract playerInteract;
    [SerializeField] private PlayerItemUseHandler playerItemUseHandler;

    // Trạng thái di chuyển của nhân vật
    public enum MovementState { Idle, Walk, Sprint, Falling }
    public MovementState state;

    [Header("References")]
    [SerializeField] private GameInput gameInput;
    [SerializeField] private PlayerAnimator playerAnimator;
    [SerializeField] PlayerStatManager playerStatManager;
    [SerializeField] PlayerItemUseHandler itemUseHandler;



    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed;
    [SerializeField] private float sprintSpeed;
    [SerializeField] private float rotationSpeed;
    [SerializeField] private float acceleration;
    [SerializeField] private float jumpDelayTimer;
    [SerializeField] private float jumpForce;
    [SerializeField] private float jumpCooldown;
    [SerializeField] private float gravityMultiplier;

    [Header("Aiming Movement")]
    [SerializeField] private float aimWalkSpeed = 2f; // Slower speed when aiming
    [SerializeField] private float aimRotationSpeed = 360f; // Faster rotation to camera

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundDistance;
    [SerializeField] private LayerMask groundMask;

    [Header("Slope Handling")]
    [SerializeField] private float maxSlopeAngle = 45f;
    [SerializeField] private float slopeCheckDistance = 0.5f;
    [SerializeField] private float slopeForceDown = 8f;
    [SerializeField] private bool useRaycastForSlopes = true;

    [Header("Debug Info")]
    [SerializeField] private bool showInventory;
    [Header("Stamina")]
    [SerializeField] private float sprintStaminaDrainRate; // Stamina per second while sprinting
    [SerializeField] private float jumpStaminaDranRate;

    private CharacterController controller;
    [SerializeField] bool isGrounded;
    private bool wasGrounded = false;
    private bool isFalling;
    private bool isWalking;
    private bool isSprinting;
    private bool canJump = true;
    public bool isOnSlope = false;
    public Transform aimTarget;
    private float currentSpeed;
    private float maxSpeed;
    private float verticalVelocity;
    public float slopeAngle;
    private Vector3 slopeNormal;
    private Vector3 hitPointNormal;

    private Vector3 moveDir;
    private Vector2 inputVector;

    public bool isFrozen = false;

    // Cache để tiết kiệm hiệu năng
    private RaycastHit slopeHit;
    public override void OnStartLocalPlayer()
    {
        controller = GetComponent<CharacterController>();
        if (controller == null)
            Debug.LogError("Thiếu CharacterController trên Player!");

        if (playerInteract == null)
            playerInteract = GetComponent<PlayerInteract>();

        if (playerItemUseHandler == null)
            playerItemUseHandler = GetComponent<PlayerItemUseHandler>();

        // Đăng ký sự kiện từ GameInput
        if (gameInput != null)
        {
            gameInput.OnSprintStarted += GameInput_OnSprintStarted;
            gameInput.OnSprintCanceled += GameInput_OnSprintCanceled;
            gameInput.OnJump += GameInput_OnJump;
            gameInput.OnShowInventory += GameInput_OnShowInventory;
        }
        if (playerStatManager != null)
        {
            playerStatManager.OnStaminaDepleted += () => isSprinting = false;
        }
    }
    //private void Start()
    //{
    //    controller = GetComponent<CharacterController>();
    //    if (controller == null)
    //        Debug.LogError("Thiếu CharacterController trên Player!");

    //    if (playerInteract == null)
    //        playerInteract = GetComponent<PlayerInteract>();

    //    if (playerItemUseHandler == null)
    //        playerItemUseHandler = GetComponent<PlayerItemUseHandler>();

    //    // Đăng ký sự kiện từ GameInput
    //    if (gameInput != null)
    //    {
    //        gameInput.OnSprintStarted += GameInput_OnSprintStarted;
    //        gameInput.OnSprintCanceled += GameInput_OnSprintCanceled;
    //        gameInput.OnJump += GameInput_OnJump;
    //        gameInput.OnShowInventory += GameInput_OnShowInventory;
    //    }
    //}

    private void Update()
    {
        if (!isLocalPlayer) return;
        if (isSprinting && isWalking && isGrounded)
        {
            if (isServer)
            {
                playerStatManager.UseStamina(sprintStaminaDrainRate * Time.deltaTime);
            }
            else
            {
                CmdUseStamina(sprintStaminaDrainRate * Time.deltaTime);
            }

            // Stop sprinting if out of stamina
            if (playerStatManager.CurrentStamina <= 0)
            {
                isSprinting = false;
            }
        }
        // Lấy input từ GameInput
        inputVector = gameInput.GetMovementVector();

        // Check if player is aiming
        bool isAiming = playerItemUseHandler != null && playerItemUseHandler.IsAiming();

        if (isAiming)
        {
            HandleAimingMovement();
        }
        else
        {
            HandleNormalMovement();
        }

        // Kiểm tra trạng thái
        UpdateAimTarget();
        CheckIfGrounded();
        CheckSlope();
        CheckIfFalling();
        UpdateState();
        ApplyGravity();

        if (!isAiming)
        {
            RotateTowardsCameraWhenInteracting();
        }

        // Di chuyển nhân vật
        MovePlayer();
    }
    private void UpdateAimTarget()
    {
        if (aimTarget == null) return;

        Transform cam = Camera.main.transform;
        Vector3 followPoint = cam.position + cam.forward * 40f;   // default follow position

        // If not aiming → aimTarget simply follows camera
        if (!itemUseHandler.IsAiming())
        {
            aimTarget.position = followPoint;
            return;
        }

        // If aiming → try to find a valid hit
        Ray ray = new Ray(cam.position, cam.forward);
        RaycastHit[] hits = Physics.RaycastAll(ray, 200f);

        RaycastHit validHit = default;
        bool foundHit = false;
        float closestDist = Mathf.Infinity;

        foreach (RaycastHit hit in hits)
        {
            // Ignore player's own body
            if (hit.transform.IsChildOf(transform))
                continue;

            if (hit.distance < closestDist)
            {
                closestDist = hit.distance;
                validHit = hit;
                foundHit = true;
            }
        }

        // If a surface was hit, aim there. Otherwise aim far.
        Vector3 targetPos = foundHit ? validHit.point : followPoint;

        aimTarget.position = targetPos;
    }

    private void HandleNormalMovement()
    {
        // Lấy hướng camera để nhân vật di chuyển theo
        Transform cam = Camera.main.transform;
        Vector3 camForward = cam.forward;
        Vector3 camRight = cam.right;
        camForward.y = 0f;
        camRight.y = 0f;

        // Xác định hướng di chuyển dựa trên input và camera
        moveDir = (camForward * inputVector.y + camRight * inputVector.x).normalized;
        isWalking = moveDir != Vector3.zero;

        // Nếu đang di chuyển và đứng trên đất → xoay nhân vật theo hướng di chuyển
        if (isWalking && isGrounded)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime * 100f);
        }
    }

    private void HandleAimingMovement()
    {
        // When aiming, player always faces camera direction
        Transform cam = Camera.main.transform;
        Vector3 camForward = cam.forward;
        camForward.y = 0f;

        if (camForward.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(camForward);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, aimRotationSpeed * Time.deltaTime);
        }

        // Calculate strafe movement (relative to camera)
        Vector3 camRight = cam.right;
        camRight.y = 0f;
        camForward.Normalize();
        camRight.Normalize();

        // Move relative to camera direction
        moveDir = (camForward * inputVector.y + camRight * inputVector.x).normalized;
        isWalking = moveDir != Vector3.zero;
    }

    private void CheckSlope()
    {
        // Reset biến liên quan đến dốc
        isOnSlope = false;
        slopeAngle = 0f;
        slopeNormal = Vector3.up;

        if (!isGrounded) return;

        if (useRaycastForSlopes)
        {
            Vector3 rayOrigin = transform.position + Vector3.up * 0.1f;

            if (Physics.Raycast(rayOrigin, Vector3.down, out slopeHit, slopeCheckDistance, groundMask))
            {
                slopeNormal = slopeHit.normal;
                slopeAngle = Vector3.Angle(Vector3.up, slopeNormal);

                if (slopeAngle > 0.1f && slopeAngle <= maxSlopeAngle)
                {
                    isOnSlope = true;
                }
            }
        }
        else
        {
            if (hitPointNormal != Vector3.zero)
            {
                slopeNormal = hitPointNormal;
                slopeAngle = Vector3.Angle(Vector3.up, slopeNormal);

                if (slopeAngle > 0.1f && slopeAngle <= maxSlopeAngle)
                {
                    isOnSlope = true;
                }
            }
        }

        Debug.DrawRay(transform.position, slopeNormal * 2f, Color.red);
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        hitPointNormal = hit.normal;
    }

    private void MovePlayer()
    {
        if (isFrozen)
        {
            controller.Move(Vector3.zero);
            return;
        }

        bool isAiming = playerItemUseHandler != null && playerItemUseHandler.IsAiming();

        // Xác định tốc độ mục tiêu
        float targetSpeed;
        if (isAiming)
        {
            targetSpeed = aimWalkSpeed; // Use slower aiming speed
        }
        else
        {
            targetSpeed = isSprinting ? sprintSpeed : walkSpeed;
        }

        currentSpeed = isWalking ? Mathf.MoveTowards(currentSpeed, targetSpeed, acceleration * Time.deltaTime) : 0f;

        Vector3 move;

        if (isOnSlope && isWalking)
        {
            move = ProjectOnSlope(moveDir) * currentSpeed;

            if (slopeAngle > 15f)
            {
                move.y -= slopeForceDown * Time.deltaTime;
            }
        }
        else
        {
            move = moveDir * currentSpeed;
        }

        move.y += verticalVelocity;
        if (!controller || !controller.enabled) return;
        controller.Move(move * Time.deltaTime);

        if (isOnSlope && isGrounded && verticalVelocity <= 0)
        {
            StickToGround();
        }
    }

    private Vector3 ProjectOnSlope(Vector3 direction)
    {
        return Vector3.ProjectOnPlane(direction, slopeNormal).normalized;
    }

    private void StickToGround()
    {
        float stickDistance = 0.3f;
        Vector3 rayOrigin = transform.position + controller.center;

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit,
            controller.height / 2 + stickDistance, groundMask))
        {
            float distanceToGround = hit.distance - controller.height / 2;

            if (distanceToGround > 0.01f)
            {
                Vector3 stickMove = Vector3.down * distanceToGround;
                controller.Move(stickMove);
            }
        }
    }

    private void ApplyGravity()
    {
        if (isFrozen) return;

        if (isGrounded && verticalVelocity < 0f)
        {
            if (isOnSlope)
            {
                verticalVelocity = -1f;
            }
            else
            {
                verticalVelocity = -2f;
            }
        }
        else
        {
            verticalVelocity += Physics.gravity.y * gravityMultiplier * Time.deltaTime;
        }
    }

    private void TriggerJump()
    {
        if (playerStatManager != null && playerStatManager.CurrentStamina < 20f)
        {
            return; // Not enough stamina, can't jump
        }
        StartCoroutine(JumpDelay(jumpDelayTimer));
        playerAnimator.TriggerJump();
        CmdDoJump();
    }


    IEnumerator JumpDelay(float delayTime)
    {
        yield return new WaitForSeconds(delayTime);
        // Check if player has enough stamina to jump


        if (canJump && isGrounded && (!isOnSlope || slopeAngle <= maxSlopeAngle))
        {
            verticalVelocity = Mathf.Sqrt(jumpForce * -2f * Physics.gravity.y);

            // Consume stamina for jumping
            if (isServer)
            {
                playerStatManager.UseStamina(jumpStaminaDranRate);
            }
            else
            {
                CmdUseStaminaForJump(jumpStaminaDranRate);
            }
        }
    }

    [Command]
    private void CmdDoJump()
    {
        RpcPlayJump();
    }
    [Command]
    private void CmdUseStamina(float amount)
    {
        playerStatManager.UseStamina(amount);
    }
    [Command]
    private void CmdUseStaminaForJump(float amount)
    {
        playerStatManager.UseStamina(amount);
    }
    [ClientRpc]
    private void RpcPlayJump()
    {
        playerAnimator.TriggerJump();
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
    public void SetAimTarget(Transform target)
    {
        aimTarget = target;
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

    IEnumerator ResetJumpAfterDelay()
    {
        yield return new WaitForSeconds(jumpCooldown);
        canJump = true;
    }

    // Các event từ GameInput
    private void GameInput_OnSprintStarted(object sender, System.EventArgs e)
    {

        if (playerStatManager != null && playerStatManager.CurrentStamina > 5f)
        {
            isSprinting = true;
        }
    }
    private void GameInput_OnSprintCanceled(object sender, System.EventArgs e) => isSprinting = false;
    private void GameInput_OnJump(object sender, System.EventArgs e) => TriggerJump();
    private void GameInput_OnShowInventory(object sender, System.EventArgs e)
    {
        showInventory = !showInventory;
        UIManager.Instance.ToggleInventory(showInventory);
    }

    // Getter public để lấy trạng thái
    public bool IsSprinting() => isSprinting;
    public bool IsFalling() => isFalling;
    public bool IsWalking() => isWalking;
    public bool IsGrounded() => isGrounded;
    public bool IsShowInventory() => showInventory;
    public float GetSlopeAngle() => slopeAngle;
    public bool IsOnSlope() => isOnSlope;

    // New getter for aiming movement input
    public Vector2 GetMovementInput() => inputVector;

    private void OnDrawGizmosSelected()
    {
        if (isOnSlope)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawRay(transform.position, slopeNormal * 2f);

            Gizmos.color = Color.green;
            Vector3 projectedMove = ProjectOnSlope(moveDir);
            Gizmos.DrawRay(transform.position, projectedMove * 2f);
        }
    }
}