using BSS.PoseBlender;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using Mirror;

[RequireComponent(typeof(Animator))]
[DefaultExecutionOrder(400)]  // Chạy SAU PlayableAnimationBlender (300)
public class PlayerIKController : NetworkBehaviour
{
    private Animator animator;

    [Header("Eye Control - Điều khiển mắt")]
    [Tooltip("Left eye transform")]
    public Transform leftEye;

    [Tooltip("Right eye transform")]
    public Transform rightEye;

    [Tooltip("Maximum horizontal rotation (left/right) in degrees")]
    [Range(0f, 90f)]
    public float horizontalRange = 30f;

    [Tooltip("Maximum vertical rotation (up/down) in degrees")]
    [Range(0f, 90f)]
    public float verticalRange = 20f;

    [Tooltip("Invert horizontal rotation direction")]
    public bool invertHorizontal = false;

    [Tooltip("Invert vertical rotation direction")]
    public bool invertVertical = false;

    [Tooltip("Normalized horizontal look direction (-1 = left, 1 = right)")]
    [Range(-1.5f, 1.5f)]
    public float horizontalLook = 0f;

    [Tooltip("Normalized vertical look direction (-1 = down, 1 = up)")]
    [Range(-1.5f, 1.5f)]
    public float verticalLook = 0f;

    [Tooltip("Smooth the eye movement")]
    public bool smoothEyeMovement = true;

    [Tooltip("Eyes follow the head look target")]
    public bool eyeFollowLookTarget = false;

    private Quaternion leftEyeInitialRotation;
    private Quaternion rightEyeInitialRotation;
    private Quaternion leftEyeCurrentRotation;
    private Quaternion rightEyeCurrentRotation;
    private bool eyeRotationsInitialized = false;

    [Header("Network Eye Sync - Mirror")]
    [Tooltip("How often to send eye position updates (updates per second)")]
    [Range(5f, 60f)]
    public float eyeSyncUpdateRate = 20f;

    [Tooltip("Interpolation speed for remote players' eyes")]
    [Range(1f, 30f)]
    public float eyeInterpolationSpeed = 15f;

    // SyncVar for eye look data with hook for smooth interpolation
    [SyncVar(hook = nameof(OnEyeLookChanged))]
    private Vector2 syncedEyeLook = Vector2.zero;

    private float lastEyeSyncTime;
    private Vector2 targetEyeLook;

    [Header("Head Look At - Nhìn mục tiêu")]
    public Transform lookTarget;
    public Transform cameraTransform;
    public MultiAimConstraint playerHead;
    public Transform pivotAim;
    public Transform aimTargetRotation;

    [Header("Bone Transforms - Xương nhân vật")]
    public Transform shoulderTransform;
    public Transform leftHandTransform;

    [Header("Foot IK - IK chân")]
    public bool enableFootIK = true;
    public float IK_LeftFootWeight = 1f;
    public float IK_RightFootWeight = 1f;
    public LayerMask groundMask;
    public float raycastDistance = 1.2f;
    public Vector3 footIkOffset = new Vector3(0, 0.1f, 0);
    public float smoothnessSpeed = 10f;

    [Header("Left Hand IK (Bow) - Tay trái cầm cung")]
    public bool enableLeftHandIK = false;
    public Transform leftHandIKTarget;

    [Range(0f, 1f)]
    public float leftHandIKWeight = 1f;
    public float handIkBlendSpeed = 25f;
    public Transform leftHandHint;

    [Header("Right Hand IK (Support) - Tay phải kéo dây cung")]
    public bool enableRightHandIK = false;
    public Transform rightHandIKTarget;
    public Transform rightHandOverride;
    public Transform rightHandPoleHint;
    public Transform rightHandPoleHintOverride;

    [Range(0f, 1f)]
    public float rightHandIKWeight = 1f;

    [Header("Debug Testing")]
    public bool forceAimingForTesting = false;

    // Biến private
    private float smoothLeftWeight = 0f;
    private float smoothRightWeight = 0f;
    private Transform leftFoot, rightFoot;
    private float currentLeftHandIKWeight = 0f;

    // Xương tay phải
    private Transform rightHand;
    private Transform rightLowerArm;
    private Transform rightUpperArm;

    // Xương tay trái
    private Transform leftHand;
    private Transform leftLowerArm;
    private Transform leftUpperArm;

    void Start()
    {
        animator = GetComponent<Animator>();

        // Lấy các xương từ Humanoid rig
        leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
        rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
        shoulderTransform = animator.GetBoneTransform(HumanBodyBones.LeftShoulder);
        leftHandTransform = animator.GetBoneTransform(HumanBodyBones.LeftHand);

        // Xương tay phải
        rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        rightLowerArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);

        // Xương tay trái
        leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        leftLowerArm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        leftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);

        // Lấy xương mắt
        if (leftEye == null)
            leftEye = animator.GetBoneTransform(HumanBodyBones.LeftEye);
        if (rightEye == null)
            rightEye = animator.GetBoneTransform(HumanBodyBones.RightEye);

        // ✅ Capture eye rotations IMMEDIATELY before any animation runs
        CaptureEyeInitialRotations();

        // Tắt IK ban đầu
        enableLeftHandIK = false;
        enableRightHandIK = false;
        leftHandIKWeight = 0f;
        rightHandIKWeight = 0f;
        currentLeftHandIKWeight = 0f;

        // Initialize target for remote clients
        targetEyeLook = syncedEyeLook;
    }

    /// <summary>
    /// Mirror hook callback when eye look data changes
    /// </summary>
    private void OnEyeLookChanged(Vector2 oldValue, Vector2 newValue)
    {
        targetEyeLook = newValue;
    }

    void CaptureEyeInitialRotations()
    {
        if (leftEye != null)
        {
            leftEyeInitialRotation = leftEye.localRotation;
            leftEyeCurrentRotation = leftEye.localRotation;
        }
        if (rightEye != null)
        {
            rightEyeInitialRotation = rightEye.localRotation;
            rightEyeCurrentRotation = rightEye.localRotation;
        }
        eyeRotationsInitialized = true;
    }

    public void UpdatePlayerIk(float deltaTime)
    {
        // Network eye sync logic
        if (isLocalPlayer)
        {
            // LOCAL PLAYER: Send eye data to server
            SyncEyeToServer();
        }
        else
        {
            // REMOTE PLAYER: Interpolate eye data from network
            InterpolateRemoteEyeData();
        }

        // Cập nhật hướng nhìn (only for local player)
        if (isLocalPlayer)
        {
            LookAtTarget();

            // ✅ Update eye look direction if following look target
            if (eyeFollowLookTarget && lookTarget != null && leftEye != null && eyeRotationsInitialized)
            {
                Vector3 directionToTarget = (lookTarget.position - leftEye.position).normalized;
                Vector3 localDirection = transform.InverseTransformDirection(directionToTarget);

                // Update look values based on target
                horizontalLook = Mathf.Clamp(localDirection.x * 2f, -1f, 1f);
                verticalLook = Mathf.Clamp(localDirection.y * 2f, -1f, 1f);
            }
        }

        float delta = Time.deltaTime;
        float targetWeight = 1f;

        // Tắt IK nếu animator yêu cầu
        if (animator.GetBool("disableIK"))
        {
            targetWeight = 0f;
        }

        // Blend mượt weight cho tay trái
        float targetLeftHandIKWeight = enableLeftHandIK ? leftHandIKWeight : 0f;
        currentLeftHandIKWeight = Mathf.MoveTowards(currentLeftHandIKWeight, targetLeftHandIKWeight, delta / 0.1f);

        // Cập nhật vị trí target tay phải
        UpdateRightHandIKTarget();
    }

    /// <summary>
    /// Sync local player's eye data to server (Mirror)
    /// </summary>
    private void SyncEyeToServer()
    {
        // Rate limiting
        if (Time.time - lastEyeSyncTime < 1f / eyeSyncUpdateRate)
            return;

        lastEyeSyncTime = Time.time;

        // Compress to 0-1 range for network efficiency
        Vector2 compressedEyeLook = new Vector2(
            (horizontalLook + 1f) * 0.5f, // Convert -1~1 to 0~1
            (verticalLook + 1f) * 0.5f
        );

        // Only send if changed significantly (reduce bandwidth)
        if (Vector2.Distance(syncedEyeLook, compressedEyeLook) > 0.01f)
        {
            CmdUpdateEyeLook(compressedEyeLook);
        }
    }

    /// <summary>
    /// Command to update eye look on server
    /// </summary>
    [Command]
    private void CmdUpdateEyeLook(Vector2 eyeLook)
    {
        syncedEyeLook = eyeLook;
    }

    /// <summary>
    /// Interpolate remote player's eye data
    /// </summary>
    private void InterpolateRemoteEyeData()
    {
        // Decompress from 0-1 back to -1~1
        float targetHorizontal = targetEyeLook.x * 2f - 1f;
        float targetVertical = targetEyeLook.y * 2f - 1f;

        // Smooth interpolation
        horizontalLook = Mathf.Lerp(horizontalLook, targetHorizontal, Time.deltaTime * eyeInterpolationSpeed);
        verticalLook = Mathf.Lerp(verticalLook, targetVertical, Time.deltaTime * eyeInterpolationSpeed);
    }

    private void UpdateRightHandIKTarget()
    {
        if (enableRightHandIK && rightHandIKTarget != null && rightHandOverride != null)
        {
            rightHandIKTarget.position = rightHandOverride.position;
            rightHandIKTarget.rotation = rightHandOverride.rotation;
        }
    }

    private void LookAtTarget()
    {
        // Kiểm tra null
        if (lookTarget == null || cameraTransform == null || playerHead == null)
            return;

        // Đặt lookTarget phía trước camera
        lookTarget.position = cameraTransform.position + cameraTransform.forward * 40f;

        // Tính góc giữa hướng nhân vật và camera
        float angle = Vector3.Angle(transform.forward, cameraTransform.forward);
        float maxLookAngle = 100f;

        // Blend weight dựa trên góc
        playerHead.weight = Mathf.Lerp(playerHead.weight, angle <= maxLookAngle ? 1f : 0f, Time.deltaTime * 10f);
    }

     public void LateUpdatePlayerIK(float deltaTime)
    {
        if (animator == null) return;

        // Cập nhật target tay phải
        UpdateRightHandIKTarget();

        // Áp dụng IK thủ công
        ApplyRightHandIKManual();
        ApplyLeftHandIKManual();

        // Áp dụng Foot IK
        if (enableFootIK)
            ApplyFootIKInLateUpdate();

        // ✅ Áp dụng Eye Control - chạy CUỐI CÙNG để override animation
        ApplyEyeControl();

        // Cập nhật pivot aim (cho head look) - only for local player
        if (isLocalPlayer)
        {
            if (pivotAim == null || shoulderTransform == null || lookTarget == null)
                return;

            Vector3 aimDir = lookTarget.position - pivotAim.position;
            pivotAim.position = shoulderTransform.position;
            pivotAim.rotation = Quaternion.LookRotation(aimDir);
        }
    }

    void OnAnimatorMove()
    {
        // Để trống - xử lý pivot aim trong LateUpdate
    }

    void OnAnimatorIK(int layerIndex)
    {
        // ✅ Force override eye bones in OnAnimatorIK to prevent animation control
        if (animator != null && leftEye != null && rightEye != null && eyeRotationsInitialized)
        {
            // Tell animator to use our rotation for eyes
            animator.SetBoneLocalRotation(HumanBodyBones.LeftEye, leftEyeCurrentRotation);
            animator.SetBoneLocalRotation(HumanBodyBones.RightEye, rightEyeCurrentRotation);
        }
    }

    /// <summary>
    /// ✅ Điều khiển mắt - chạy sau tất cả IK và animation
    /// Eye bone local space: Z = forward (look direction), X = right, Y = up
    /// </summary>
    private void ApplyEyeControl()
    {
        if (leftEye == null || rightEye == null) return;

        // Tính rotation dựa trên look values
        // In local space where Z is forward:
        // - Horizontal look (left/right) rotates around Y-axis (yaw)
        // - Vertical look (up/down) rotates around X-axis (pitch)
        float yRotation = horizontalLook * horizontalRange * (invertHorizontal ? -1f : 1f);
        float xRotation = -verticalLook * verticalRange * (invertVertical ? 1f : -1f);

        // Tạo rotation mục tiêu với trục đúng
        Quaternion deltaRotation = Quaternion.Euler(xRotation, yRotation, 0f);
        Quaternion targetRotation = leftEyeInitialRotation * deltaRotation;

        if (smoothEyeMovement)
        {
            // Smooth interpolation - blend from CURRENT to TARGET (ignore animation)
            leftEyeCurrentRotation = Quaternion.Slerp(
                leftEye.localRotation,
                targetRotation,
                Time.deltaTime * 10f
            );
            rightEyeCurrentRotation = Quaternion.Slerp(
                rightEye.localRotation,
                targetRotation,
                Time.deltaTime * 10f
            );
        }
        else
        {
            // Instant rotation - completely override animation
            leftEyeCurrentRotation = targetRotation;
            rightEyeCurrentRotation = targetRotation;
        }

        // Apply the rotations
        leftEye.localRotation = leftEyeCurrentRotation;
        rightEye.localRotation = rightEyeCurrentRotation;
    }

    private void ApplyRightHandIKManual()
    {
        if (rightHandIKTarget == null || rightHandIKWeight <= 0.01f)
            return;

        if (rightHand == null || rightLowerArm == null || rightUpperArm == null)
            return;

        Quaternion origUpper = rightUpperArm.rotation;
        Quaternion origLower = rightLowerArm.rotation;
        Quaternion origHand = rightHand.rotation;
        Vector3 origHandPos = rightHand.position;

        Vector3 targetPos = rightHandIKTarget.position;
        Quaternion targetRot = rightHandIKTarget.rotation;

        Vector3 polePos;
        if (rightHandPoleHintOverride != null)
        {
            polePos = rightHandPoleHintOverride.position;
        }
        else if (rightHandPoleHint != null)
        {
            polePos = rightHandPoleHint.position;
        }
        else
        {
            Vector3 shoulderToTarget = targetPos - rightUpperArm.position;
            float armLength = Vector3.Distance(rightUpperArm.position, rightLowerArm.position) +
                             Vector3.Distance(rightLowerArm.position, rightHand.position);

            Vector3 bendDirection = transform.right;
            Vector3 targetDirection = shoulderToTarget.normalized;
            bendDirection = Vector3.ProjectOnPlane(bendDirection, targetDirection).normalized;

            if (bendDirection.magnitude < 0.001f)
            {
                bendDirection = Vector3.ProjectOnPlane(transform.up, targetDirection).normalized;
            }

            Vector3 midPoint = rightUpperArm.position + shoulderToTarget * 0.5f;
            polePos = midPoint + bendDirection * (armLength * 0.4f);
        }

        TwoBoneIKSolver.Solve(
            rightUpperArm,
            rightLowerArm,
            rightHand,
            targetPos,
            polePos,
            1f,
            true
        );

        rightHand.rotation = targetRot;

        if (rightHandIKWeight < 1f)
        {
            rightUpperArm.rotation = Quaternion.Slerp(origUpper, rightUpperArm.rotation, rightHandIKWeight);
            rightLowerArm.rotation = Quaternion.Slerp(origLower, rightLowerArm.rotation, rightHandIKWeight);
            rightHand.rotation = Quaternion.Slerp(origHand, targetRot, rightHandIKWeight);
            rightHand.position = Vector3.Lerp(origHandPos, targetPos, rightHandIKWeight);
        }
    }

    private void ApplyLeftHandIKManual()
    {
        if (!enableLeftHandIK || leftHandIKTarget == null || currentLeftHandIKWeight <= 0.01f)
            return;

        if (leftHand == null || leftLowerArm == null || leftUpperArm == null)
            return;

        Quaternion origUpper = leftUpperArm.rotation;
        Quaternion origLower = leftLowerArm.rotation;
        Quaternion origHand = leftHand.rotation;

        Vector3 polePos;
        if (leftHandHint != null)
        {
            polePos = leftHandHint.position;
        }
        else
        {
            Vector3 shoulderToTarget = leftHandIKTarget.position - leftUpperArm.position;
            float armLength = Vector3.Distance(leftUpperArm.position, leftLowerArm.position) +
                             Vector3.Distance(leftLowerArm.position, leftHand.position);

            Vector3 bendDirection = -transform.right;
            Vector3 targetDirection = shoulderToTarget.normalized;
            bendDirection = Vector3.ProjectOnPlane(bendDirection, targetDirection).normalized;

            if (bendDirection.magnitude < 0.001f)
            {
                bendDirection = Vector3.ProjectOnPlane(transform.up, targetDirection).normalized;
            }

            Vector3 midPoint = leftUpperArm.position + shoulderToTarget * 0.5f;
            polePos = midPoint + bendDirection * (armLength * 0.4f);
        }

        TwoBoneIKSolver.Solve(
            leftUpperArm,
            leftLowerArm,
            leftHand,
            leftHandIKTarget.position,
            polePos,
            1f,
            false
        );

        leftHand.rotation = leftHandIKTarget.rotation;

        leftUpperArm.rotation = Quaternion.Slerp(origUpper, leftUpperArm.rotation, currentLeftHandIKWeight);
        leftLowerArm.rotation = Quaternion.Slerp(origLower, leftLowerArm.rotation, currentLeftHandIKWeight);
        leftHand.rotation = Quaternion.Slerp(origHand, leftHandIKTarget.rotation, currentLeftHandIKWeight);
    }

    private void ApplyFootIKInLateUpdate()
    {
        smoothLeftWeight = Mathf.Lerp(smoothLeftWeight, IK_LeftFootWeight, Time.deltaTime * smoothnessSpeed);
        smoothRightWeight = Mathf.Lerp(smoothRightWeight, IK_RightFootWeight, Time.deltaTime * smoothnessSpeed);

        ApplyFootIKDirect(leftFoot, smoothLeftWeight);
        ApplyFootIKDirect(rightFoot, smoothRightWeight);
    }

    private void ApplyFootIKDirect(Transform footTransform, float ikWeight)
    {
        if (!footTransform || ikWeight <= 0.01f) return;

        Vector3 start = footTransform.position + Vector3.up * 0.3f;

        if (Physics.Raycast(start, Vector3.down, out RaycastHit hit, raycastDistance, groundMask))
        {
            Vector3 pos = hit.point + footIkOffset;
            Quaternion rot = Quaternion.LookRotation(
                Vector3.ProjectOnPlane(transform.forward, hit.normal),
                hit.normal
            );

            footTransform.position = Vector3.Lerp(footTransform.position, pos, ikWeight);
            footTransform.rotation = Quaternion.Slerp(footTransform.rotation, rot, ikWeight);
        }
    }

    // ========================================================================
    // PUBLIC METHODS - Eye Control
    // ========================================================================

    /// <summary>
    /// Set eye look direction with values from -1 to 1
    /// </summary>
    public void SetEyeLookDirection(float horizontal, float vertical)
    {
        horizontalLook = Mathf.Clamp(horizontal, -1f, 1f);
        verticalLook = Mathf.Clamp(vertical, -1f, 1f);
    }

    /// <summary>
    /// Look at a specific world position with eyes
    /// </summary>
    public void LookAtPositionWithEyes(Vector3 worldPosition)
    {
        if (leftEye == null) return;

        Vector3 directionToTarget = (worldPosition - leftEye.position).normalized;
        Vector3 localDirection = transform.InverseTransformDirection(directionToTarget);

        horizontalLook = Mathf.Clamp(localDirection.x * 2f, -1f, 1f);
        verticalLook = Mathf.Clamp(localDirection.y * 2f, -1f, 1f);
    }

    /// <summary>
    /// Reset eyes to center position
    /// </summary>
    public void ResetEyes()
    {
        horizontalLook = 0f;
        verticalLook = 0f;
    }

    // ========================================================================
    // PUBLIC METHODS - Bow IK
    // ========================================================================

    public void SetupBowIK(GameObject bowObject)
    {
        if (bowObject == null) return;

        BowStringController bowController = bowObject.GetComponentInChildren<BowStringController>();
        if (bowController == null) return;

        if (bowController.rightHandIKTarget != null)
        {
            SetLeftHandIKTarget(bowController.rightHandIKTarget);
            enableLeftHandIK = true;
            leftHandIKWeight = 1f;
        }
    }

    public void ClearBowIK()
    {
        enableLeftHandIK = false;
        leftHandIKTarget = null;
        enableRightHandIK = false;
        rightHandOverride = null;
        rightHandPoleHint = null;
        rightHandPoleHintOverride = null;
    }

    // ========================================================================
    // LEFT HAND IK SETTERS
    // ========================================================================

    public void SetLeftHandIKTarget(Transform target)
    {
        leftHandIKTarget = target;
    }

    public void SetLeftHandIKEnabled(bool enabled)
    {
        enableLeftHandIK = enabled;
    }

    public void SetLeftHandIKWeight(float weight)
    {
        leftHandIKWeight = Mathf.Clamp01(weight);
    }

    // ========================================================================
    // RIGHT HAND IK SETTERS
    // ========================================================================

 

    // ========================================================================
    // GETTERS
    // ========================================================================

    public bool IsHandIKActive()
    {
        return enableLeftHandIK && currentLeftHandIKWeight > 0.01f && leftHandIKTarget;
    }

    public float GetHandIKWeight()
    {
        return currentLeftHandIKWeight;
    }

    // ========================================================================
    // OTHER SETTERS
    // ========================================================================

    public void SetFootIKEnabled(bool enabled)
    {
        enableFootIK = enabled;
    }

    public void SetAimTargetIK(Transform target)
    {
        lookTarget = target;
    }
}