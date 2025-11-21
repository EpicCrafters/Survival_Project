using BSS.PoseBlender;
using UnityEngine;
using UnityEngine.Animations.Rigging;

[RequireComponent(typeof(Animator))]
[DefaultExecutionOrder(100)]
public class PlayerIKController : MonoBehaviour
{
    private Animator animator;

    [Header("Head Look At")]
    public Transform lookTarget;
    public Transform cameraTransform;
    public MultiAimConstraint playerHead;

    [Header("Foot IK")]
    public bool enableFootIK = true;
    public float IK_LeftFootWeight = 1f;
    public float IK_RightFootWeight = 1f;
    public LayerMask groundMask;
    public float raycastDistance = 1.2f;
    public Vector3 footIkOffset = new Vector3(0, 0.1f, 0);
    public float smoothnessSpeed = 10f;

    [Header("Right Hand IK (Bow)")]
    public bool enableRightHandIK = false;
    public Transform rightHandIKTarget;

    [Range(0f, 1f)]
    public float rightHandIKWeight = 1f;
    public float handIkBlendSpeed = 25f;
    public bool maintainHandRotation = false;

    [Header("IK Debug & Fixes")]
    public bool clampTargetDistance = true;
    public float maxReachDistance = 0f;
    public bool stabilizeIK = true;

    [Range(0f, 0.1f)]
    public float stabilizationThreshold = 0.001f;

    private float smoothLeftWeight = 0f;
    private float smoothRightWeight = 0f;
    private Transform leftFoot, rightFoot;

    private float currentRightHandIKWeight = 0f;
    private float targetRightHandIKWeight = 0f;

    void Start()
    {
        animator = GetComponent<Animator>();
        leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
        rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
    }

    void Update()
    {
        LookAtTarget();
        targetRightHandIKWeight = enableRightHandIK ? rightHandIKWeight : 0f;
        currentRightHandIKWeight = Mathf.Lerp(currentRightHandIKWeight, targetRightHandIKWeight, handIkBlendSpeed * Time.deltaTime);
    }

    private void LookAtTarget()
    {
        if (lookTarget == null || cameraTransform == null || playerHead == null)
            return;

        lookTarget.position = cameraTransform.position + cameraTransform.forward * 40f;

        float angle = Vector3.Angle(transform.forward, cameraTransform.forward);
        float maxLookAngle = 100f;

        playerHead.weight = Mathf.Lerp(playerHead.weight, angle <= maxLookAngle ? 1f : 0f, Time.deltaTime * 10f);
    }

    void LateUpdate()
    {
        if (animator == null) return;

        ApplyRightHandIK();

        if (enableFootIK)
            ApplyFootIKInLateUpdate();
    }

    private void ApplyRightHandIK()
    {
        if (!enableRightHandIK || rightHandIKTarget == null || currentRightHandIKWeight <= 0.01f)
            return;

        Transform upperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        Transform lowerArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);

        Quaternion originalUpper = upperArm.rotation;
        Quaternion originalLower = lowerArm.rotation;
        Quaternion originalHand = hand.rotation;

        // Automatically calculate pole position (no more rightHandPoleTarget)
        Vector3 shoulderToElbow = lowerArm.position - upperArm.position;
        Vector3 shoulderToHand = rightHandIKTarget.position - upperArm.position;

        Vector3 bendNormal = Vector3.Cross(shoulderToElbow, shoulderToHand).normalized;
        if (bendNormal.magnitude < 0.001f)
            bendNormal = -transform.right;

        Vector3 mid = upperArm.position + shoulderToHand * 0.5f;
        Vector3 poleDir = Vector3.Cross(shoulderToHand, bendNormal).normalized;

        float poleDist = shoulderToHand.magnitude * 0.3f;
        Vector3 polePos = mid + poleDir * poleDist;

        // Full solve
        TwoBoneIKSolver.Solve(
            upperArm,
            lowerArm,
            hand,
            rightHandIKTarget.position,
            polePos,
            1f,
            maintainHandRotation
        );

        // Blend
        upperArm.rotation = Quaternion.Slerp(originalUpper, upperArm.rotation, currentRightHandIKWeight);
        lowerArm.rotation = Quaternion.Slerp(originalLower, lowerArm.rotation, currentRightHandIKWeight);

        if (!maintainHandRotation)
            hand.rotation = Quaternion.Slerp(originalHand, rightHandIKTarget.rotation, currentRightHandIKWeight);
        else
            hand.rotation = Quaternion.Slerp(originalHand, hand.rotation, currentRightHandIKWeight);
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

    public void SetupBowIK(GameObject bowObject)
    {
        if (bowObject == null) return;

        BowStringController bowController = bowObject.GetComponentInChildren<BowStringController>();
        if (bowController == null) return;

        if (bowController.rightHandIKTarget != null)
        {
            SetRightHandIKTarget(bowController.rightHandIKTarget);
            enableRightHandIK = true;
            rightHandIKWeight = 1f;
        }
    }

    public void ClearBowIK()
    {
        enableRightHandIK = false;
        rightHandIKTarget = null;
    }

    public void SetRightHandIKTarget(Transform target)
    {
        rightHandIKTarget = target;
    }

    public void SetRightHandIKEnabled(bool enabled)
    {
        enableRightHandIK = enabled;
    }

    public void SetRightHandIKWeight(float weight)
    {
        rightHandIKWeight = Mathf.Clamp01(weight);
    }

    public bool IsHandIKActive()
    {
        return enableRightHandIK && currentRightHandIKWeight > 0.01f && rightHandIKTarget;
    }

    public float GetHandIKWeight()
    {
        return currentRightHandIKWeight;
    }

    public void SetFootIKEnabled(bool enabled)
    {
        enableFootIK = enabled;
    }
}
