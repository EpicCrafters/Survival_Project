using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using Mirror;

public class Player : NetworkBehaviour
{
    [SerializeField] private PlayerInteract playerInteract;

    // Trạng thái di chuyển của nhân vật
    public enum MovementState { Idle, Walk, Sprint, Falling }
    public MovementState state;

    [Header("References")]
    [SerializeField] private GameInput gameInput;
    [SerializeField] private PlayerAnimator playerAnimator;

    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed;        // Tốc độ đi bộ
    [SerializeField] private float sprintSpeed;      // Tốc độ chạy nhanh
    [SerializeField] private float rotationSpeed;    // Tốc độ xoay nhân vật
    [SerializeField] private float acceleration;     // Độ tăng tốc khi di chuyển
    [SerializeField] private float jumpForce;        // Lực nhảy
    [SerializeField] private float jumpCooldown;     // Thời gian hồi nhảy
    [SerializeField] private float gravityMultiplier;// Hệ số trọng lực

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;  // Vị trí để kiểm tra tiếp đất
    [SerializeField] private float groundDistance;   // Bán kính kiểm tra mặt đất
    [SerializeField] private LayerMask groundMask;   // Layer nào được coi là mặt đất

    [Header("Slope Handling")]
    [SerializeField] private float maxSlopeAngle = 45f;      // Góc dốc tối đa mà nhân vật có thể đứng
    [SerializeField] private float slopeCheckDistance = 0.5f;// Khoảng cách raycast kiểm tra dốc
    [SerializeField] private float slopeForceDown = 8f;      // Lực kéo nhân vật xuống khi trên dốc
    [SerializeField] private bool useRaycastForSlopes = true;// Có dùng raycast để kiểm tra dốc hay không

    [Header("Debug Info")]
    [SerializeField] private bool showInventory; // Trạng thái mở/đóng túi đồ

    private CharacterController controller;
    [SerializeField] bool isGrounded;     // Nhân vật có đang đứng trên đất hay không
    private bool wasGrounded = false;     // Trạng thái trước đó có đứng trên đất hay không
    private bool isFalling;               // Đang rơi
    private bool isWalking;               // Đang đi bộ
    private bool isSprinting;             // Đang chạy nhanh
    private bool canJump = true;          // Có thể nhảy hay không
    public bool isOnSlope = false;        // Nhân vật có đang đứng trên dốc không

    private float currentSpeed;           // Tốc độ hiện tại
    private float maxSpeed;               // Tốc độ tối đa 
    private float verticalVelocity;       // Vận tốc theo trục Y 
    public float slopeAngle;             // Góc dốc hiện tại
    private Vector3 slopeNormal;          // Pháp tuyến của mặt dốc
    private Vector3 hitPointNormal;       // Normal từ va chạm của CharacterController

    private Vector3 moveDir;              // Hướng di chuyển
    private Vector2 inputVector;          // Input từ bàn phím/gamepad

    public bool isFrozen = false;         // Trạng thái đóng băng 

    // Cache để tiết kiệm hiệu năng
    private RaycastHit slopeHit;

    private void Start()
    {
        controller = GetComponent<CharacterController>();
        if (controller == null)
            Debug.LogError("Thiếu CharacterController trên Player!");

        if (playerInteract == null)
            playerInteract = GetComponent<PlayerInteract>();

        // Đăng ký sự kiện từ GameInput
        if (gameInput != null)
        {
            gameInput.OnSprintStarted += GameInput_OnSprintStarted;
            gameInput.OnSprintCanceled += GameInput_OnSprintCanceled;
            gameInput.OnJump += GameInput_OnJump;
            gameInput.OnShowInventory += GameInput_OnShowInventory;
        }
    }

    private void Update()
    {
        if (!isLocalPlayer) return; // Chỉ xử lý cho local player trong multiplayer

        // Lấy input từ GameInput
        inputVector = gameInput.GetMovementVector();

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

        // Kiểm tra trạng thái
        CheckIfGrounded();
        CheckSlope();
        CheckIfFalling();
        UpdateState();
        ApplyGravity();
        RotateTowardsCameraWhenInteracting();

        // Di chuyển nhân vật
        MovePlayer();

        //Debug.LogError(transform.position);
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
            //Dùng raycast để kiểm tra mặt đất/dốc
            Vector3 rayOrigin = transform.position + Vector3.up * 0.1f;

            if (Physics.Raycast(rayOrigin, Vector3.down, out slopeHit, slopeCheckDistance, groundMask))
            {
                slopeNormal = slopeHit.normal;
                slopeAngle = Vector3.Angle(Vector3.up, slopeNormal);

                // Nếu góc dốc nằm trong giới hạn → coi là đang đứng trên dốc
                if (slopeAngle > 0.1f && slopeAngle <= maxSlopeAngle)
                {
                    isOnSlope = true;
                }
            }
        }
        else
        {
            // Dùng thông tin va chạm của CharacterController
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

        // Debug vẽ ray để quan sát slopeNormal
        Debug.DrawRay(transform.position, slopeNormal * 2f, Color.red);
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        // Lưu lại normal của bề mặt va chạm để dùng cho tính toán slope
        hitPointNormal = hit.normal;
    }

    private void MovePlayer()
    {
        if (isFrozen)
        {
            controller.Move(Vector3.zero);
            return;
        }

        // Xác định tốc độ mục tiêu (chạy hay đi)
        float targetSpeed = isSprinting ? sprintSpeed : walkSpeed;
        currentSpeed = isWalking ? Mathf.MoveTowards(currentSpeed, targetSpeed, acceleration * Time.deltaTime) : 0f;

        Vector3 move;

        if (isOnSlope && isWalking)
        {
            // Chiếu hướng di chuyển lên mặt phẳng của dốc
            move = ProjectOnSlope(moveDir) * currentSpeed;

            // Nếu góc dốc lớn → thêm lực kéo xuống để nhân vật không bay
            if (slopeAngle > 15f)
            {
                move.y -= slopeForceDown * Time.deltaTime;
            }
        }
        else
        {
            // Di chuyển bình thường trên mặt phẳng
            move = moveDir * currentSpeed;
        }

        // Cộng thêm vận tốc theo trục Y 
        move.y += verticalVelocity;

        // Di chuyển bằng CharacterController
        controller.Move(move * Time.deltaTime);

        // Giữ nhân vật dính xuống mặt đất khi đi xuống dốc
        if (isOnSlope && isGrounded && verticalVelocity <= 0)
        {
            StickToGround();
        }
    }

    private Vector3 ProjectOnSlope(Vector3 direction)
    {
        // Chiếu vector hướng di chuyển lên mặt phẳng của dốc
        return Vector3.ProjectOnPlane(direction, slopeNormal).normalized;
    }

    private void StickToGround()
    {
        // Raycast xuống dưới để dính vào mặt đất/dốc
        float stickDistance = 0.3f;
        Vector3 rayOrigin = transform.position + controller.center;

        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit,
            controller.height / 2 + stickDistance, groundMask))
        {
            float distanceToGround = hit.distance - controller.height / 2;

            if (distanceToGround > 0.01f)
            {
                // Kéo nhân vật xuống mặt đất
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
            // Nếu đang trên dốc → dùng giá trị gravity nhỏ hơn
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
        // Chỉ nhảy nếu được phép và không đứng trên dốc quá dốc
        if (canJump && isGrounded && (!isOnSlope || slopeAngle <= maxSlopeAngle))
        {
            verticalVelocity = Mathf.Sqrt(jumpForce * -2f * Physics.gravity.y);

            playerAnimator.TriggerJump();
            CmdDoJump();
        }
    }


    [Command]
    private void CmdDoJump()
    {
        RpcPlayJump();
    }

    [ClientRpc]
    private void RpcPlayJump()
    {

        playerAnimator.TriggerJump();
    }


    private void CheckIfGrounded()
    {
        // Kiểm tra tiếp đất bằng hình cầu
        isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);

        // Nếu vừa chạm đất → reset khả năng nhảy sau 1 khoảng thời gian
        if (isGrounded && !wasGrounded)
        {
            StartCoroutine(ResetJumpAfterDelay());
        }

        wasGrounded = isGrounded;
    }

    private void CheckIfFalling()
    {
        // Xác định có đang rơi hay không
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

    private void RotateTowardsCameraWhenInteracting()
    {
        // Khi đang khai thác/mine → nhân vật quay theo hướng camera
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
        // Hồi lại khả năng nhảy sau jumpCooldown
        yield return new WaitForSeconds(jumpCooldown);
        canJump = true;
    }

    // Các event từ GameInput
    private void GameInput_OnSprintStarted(object sender, System.EventArgs e) => isSprinting = true;
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

    // Debug gizmo: vẽ hướng slope và hướng di chuyển
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
