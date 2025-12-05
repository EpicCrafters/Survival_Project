using BSS.PoseBlender;
using UnityEngine;
using UnityEngine.Animations.Rigging;

[RequireComponent(typeof(Animator))]
[DefaultExecutionOrder(400)]  // Chạy SAU PlayableAnimationBlender (300)
public class PlayerIKController : MonoBehaviour
{
    private Animator animator;

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
    public Transform rightHandOverride;  // Transform động từ dây cung
    public Transform rightHandPoleHint;  // Hint position mặc định
    public Transform rightHandPoleHintOverride;  // ✅ Hint position động từ dây cung

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
        // Lấy component Animator
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

        // Tắt IK ban đầu
        enableLeftHandIK = false;
        enableRightHandIK = false;
        leftHandIKWeight = 0f;
        rightHandIKWeight = 0f;
        currentLeftHandIKWeight = 0f;
    }

    void Update()
    {
        // Cập nhật hướng nhìn
        LookAtTarget();

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
    /// Cập nhật vị trí target tay phải từ override (dây cung động)
    /// </summary>
    private void UpdateRightHandIKTarget()
    {
        if (enableRightHandIK && rightHandIKTarget != null && rightHandOverride != null)
        {
            rightHandIKTarget.position = rightHandOverride.position;
            rightHandIKTarget.rotation = rightHandOverride.rotation;
        }
    }

    /// <summary>
    /// Xử lý nhìn mục tiêu (head look at)
    /// </summary>
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

    void LateUpdate()
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

        // Cập nhật pivot aim (cho head look)
        if (pivotAim == null || shoulderTransform == null || lookTarget == null)
            return;

        Vector3 aimDir = lookTarget.position - pivotAim.position;
        pivotAim.position = shoulderTransform.position;
        pivotAim.rotation = Quaternion.LookRotation(aimDir);
    }

    void OnAnimatorMove()
    {
        // Để trống - xử lý pivot aim trong LateUpdate
    }

    void OnAnimatorIK(int layerIndex)
    {
        // Để trống - xử lý IK thủ công trong LateUpdate
    }

    /// <summary>
    /// Áp dụng IK tay phải (kéo dây cung)
    /// </summary>
    private void ApplyRightHandIKManual()
    {
        // Kiểm tra điều kiện
        if (rightHandIKTarget == null || rightHandIKWeight <= 0.01f)
            return;

        if (rightHand == null || rightLowerArm == null || rightUpperArm == null)
            return;

        // Lưu rotation gốc
        Quaternion origUpper = rightUpperArm.rotation;
        Quaternion origLower = rightLowerArm.rotation;
        Quaternion origHand = rightHand.rotation;
        Vector3 origHandPos = rightHand.position;

        // Lấy target position và rotation
        Vector3 targetPos = rightHandIKTarget.position;
        Quaternion targetRot = rightHandIKTarget.rotation;

        // ✅ Tính pole position (hint) - ƯU TIÊN override từ dây cung
        Vector3 polePos;
        if (rightHandPoleHintOverride != null)
        {
            // Sử dụng hint động từ dây cung (ưu tiên)
            polePos = rightHandPoleHintOverride.position;
        }
        else if (rightHandPoleHint != null)
        {
            // Sử dụng hint tĩnh
            polePos = rightHandPoleHint.position;
        }
        else
        {
            // Tính toán tự động nếu không có hint
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

        // Giải Two-Bone IK
        TwoBoneIKSolver.Solve(
            rightUpperArm,
            rightLowerArm,
            rightHand,
            targetPos,
            polePos,
            1f,
            true
        );

        // Áp dụng rotation target
        rightHand.rotation = targetRot;

        // Blend với weight nếu < 1
        if (rightHandIKWeight < 1f)
        {
            rightUpperArm.rotation = Quaternion.Slerp(origUpper, rightUpperArm.rotation, rightHandIKWeight);
            rightLowerArm.rotation = Quaternion.Slerp(origLower, rightLowerArm.rotation, rightHandIKWeight);
            rightHand.rotation = Quaternion.Slerp(origHand, targetRot, rightHandIKWeight);
            rightHand.position = Vector3.Lerp(origHandPos, targetPos, rightHandIKWeight);
        }
    }

    /// <summary>
    /// Áp dụng IK tay trái (cầm cung)
    /// </summary>
    private void ApplyLeftHandIKManual()
    {
        // Kiểm tra điều kiện
        if (!enableLeftHandIK || leftHandIKTarget == null || currentLeftHandIKWeight <= 0.01f)
            return;

        if (leftHand == null || leftLowerArm == null || leftUpperArm == null)
            return;

        // Lưu rotation gốc
        Quaternion origUpper = leftUpperArm.rotation;
        Quaternion origLower = leftLowerArm.rotation;
        Quaternion origHand = leftHand.rotation;

        // Tính pole position (hint)
        Vector3 polePos;
        if (leftHandHint != null)
        {
            polePos = leftHandHint.position;
        }
        else
        {
            // Tính toán tự động
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

        // Giải Two-Bone IK
        TwoBoneIKSolver.Solve(
            leftUpperArm,
            leftLowerArm,
            leftHand,
            leftHandIKTarget.position,
            polePos,
            1f,
            false
        );

        // Áp dụng rotation target
        leftHand.rotation = leftHandIKTarget.rotation;

        // Blend với weight
        leftUpperArm.rotation = Quaternion.Slerp(origUpper, leftUpperArm.rotation, currentLeftHandIKWeight);
        leftLowerArm.rotation = Quaternion.Slerp(origLower, leftLowerArm.rotation, currentLeftHandIKWeight);
        leftHand.rotation = Quaternion.Slerp(origHand, leftHandIKTarget.rotation, currentLeftHandIKWeight);
    }

    /// <summary>
    /// Áp dụng Foot IK trong LateUpdate
    /// </summary>
    private void ApplyFootIKInLateUpdate()
    {
        // Blend mượt weight
        smoothLeftWeight = Mathf.Lerp(smoothLeftWeight, IK_LeftFootWeight, Time.deltaTime * smoothnessSpeed);
        smoothRightWeight = Mathf.Lerp(smoothRightWeight, IK_RightFootWeight, Time.deltaTime * smoothnessSpeed);

        // Áp dụng cho từng chân
        ApplyFootIKDirect(leftFoot, smoothLeftWeight);
        ApplyFootIKDirect(rightFoot, smoothRightWeight);
    }

    /// <summary>
    /// Áp dụng IK trực tiếp cho 1 chân
    /// </summary>
    private void ApplyFootIKDirect(Transform footTransform, float ikWeight)
    {
        if (!footTransform || ikWeight <= 0.01f) return;

        // Raycast từ trên xuống
        Vector3 start = footTransform.position + Vector3.up * 0.3f;

        if (Physics.Raycast(start, Vector3.down, out RaycastHit hit, raycastDistance, groundMask))
        {
            // Tính vị trí và rotation mới
            Vector3 pos = hit.point + footIkOffset;
            Quaternion rot = Quaternion.LookRotation(
                Vector3.ProjectOnPlane(transform.forward, hit.normal),
                hit.normal
            );

            // Blend với weight
            footTransform.position = Vector3.Lerp(footTransform.position, pos, ikWeight);
            footTransform.rotation = Quaternion.Slerp(footTransform.rotation, rot, ikWeight);
        }
    }

    // ========================================================================
    // PUBLIC METHODS - Các phương thức công khai để setup/control IK
    // ========================================================================

    /// <summary>
    /// Setup IK cho cung (bow)
    /// </summary>
    public void SetupBowIK(GameObject bowObject)
    {
        if (bowObject == null) return;

        BowStringController bowController = bowObject.GetComponentInChildren<BowStringController>();
        if (bowController == null) return;

        // Setup tay trái (cầm cung)
        if (bowController.rightHandIKTarget != null)
        {
            SetLeftHandIKTarget(bowController.rightHandIKTarget);
            enableLeftHandIK = true;
            leftHandIKWeight = 1f;
        }
    }

    /// <summary>
    /// Xóa IK của cung
    /// </summary>
    public void ClearBowIK()
    {
        enableLeftHandIK = false;
        leftHandIKTarget = null;
        enableRightHandIK = false;
        rightHandOverride = null;
        rightHandPoleHint = null;
        rightHandPoleHintOverride = null;  // ✅ Clear cả override
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

    /// <summary>
    /// Set target động cho tay phải (transform di chuyển theo dây cung)
    /// </summary>
    public void SetRightHandIKTarget(Transform target)
    {
        rightHandOverride = target;
    }

    /// <summary>
    /// Set target trực tiếp (không qua override)
    /// </summary>
    public void SetRightHandIKTargetDirect(Transform target)
    {
        rightHandIKTarget = target;
    }

    /// <summary>
    /// ✅ Set hint tĩnh cho tay phải
    /// </summary>
    public void SetRightHandPoleHint(Transform poleHint)
    {
        rightHandPoleHint = poleHint;
    }

    /// <summary>
    /// ✅ Set hint động cho tay phải (override từ dây cung)
    /// </summary>
    public void SetRightHandPoleHintOverride(Transform poleHintOverride)
    {
        rightHandPoleHintOverride = poleHintOverride;
    }

    public void SetRightHandIKEnabled(bool enabled)
    {
        enableRightHandIK = enabled;
    }

    public void SetRightHandIKWeight(float weight)
    {
        rightHandIKWeight = Mathf.Clamp01(weight);
    }

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