using UnityEngine;
using UnityEngine.Animations.Rigging;

[RequireComponent(typeof(Animator))]
public class PlayerIKController : MonoBehaviour
{
    private Animator animator;

    public Transform lookTarget;
    public Transform cameraTransform;


    public MultiAimConstraint playerHead;

    // Giá trị lấy từ animation 
    public float IK_LeftFootWeight;
    public float IK_RightFootWeight;


    public LayerMask groundMask;


    public float raycastDistance = 1.2f;

    public Vector3 footIkOffset = new Vector3(0, 0.1f, 0);


    public float smotthnessSpeed = 10f;

    // Lưu giá trị weight đã được làm mượt
    private float smoothLeftWeight = 0f;
    private float smoothRightWeight = 0f;

    // Transform của xương bàn chân
    private Transform leftFoot, rightFoot;


    public void Update()
    {
        LookAtTarget();
    }
    void Start()
    {
        animator = GetComponent<Animator>();
        leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
        rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
    }

    private void LookAtTarget()
    {
        if (lookTarget == null || cameraTransform == null || playerHead == null)
            return;

        //Di chuyen Object theo tam nhin cua camera
        lookTarget.position = cameraTransform.position + cameraTransform.forward * 40f;

        //Huong nhin cua Cameara va Player
        Vector3 cameraDir = cameraTransform.forward;
        Vector3 playerForward = transform.forward;


        //Tinh Goc giua huong nhin cua camera va nhan vat 
        float angle = Vector3.Angle(playerForward, cameraDir);


        //Gioi han goc nhin
        float maxLookAngle = 100f;

        if (angle <= maxLookAngle)
        {
            //Neu Camera o phia truoc nhan vat 
            playerHead.weight = Mathf.Lerp(playerHead.weight, 1f, Time.deltaTime * 10f);
        }
        else
        {
            //Neu Camera o phia sau nhan vat 
            playerHead.weight = Mathf.Lerp(playerHead.weight, 0f, Time.deltaTime * 10f);
        }
    }


    void OnAnimatorIK(int layerIndex)
    {
        // Lấy giá trị IK weight từ animation curve
        float targetLeftWeight = IK_LeftFootWeight;
        float targetRightWeight = IK_RightFootWeight;

        // Làm mượt 
        smoothLeftWeight = Mathf.Lerp(smoothLeftWeight, targetLeftWeight, Time.deltaTime * smotthnessSpeed);
        smoothRightWeight = Mathf.Lerp(smoothRightWeight, targetRightWeight, Time.deltaTime * smotthnessSpeed);

        // Áp dụng IK cho chân
        ApplyFootIK(AvatarIKGoal.LeftFoot, leftFoot, smoothLeftWeight);
        ApplyFootIK(AvatarIKGoal.RightFoot, rightFoot, smoothRightWeight);
    }

    // Hàm xử lý đặt chân xuống mặt đất bằng IK
    void ApplyFootIK(AvatarIKGoal foot, Transform footTransform, float ikWeight)
    {
        Vector3 footPos = footTransform.position;
        Vector3 rayStart = footPos + Vector3.up * 0.3f;

        // Raycast
        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, raycastDistance, groundMask))
        {
            // Vị trí và hướng xoay của chân 
            Vector3 ikPosition = hit.point + footIkOffset;
            Quaternion ikRotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(transform.forward, hit.normal), hit.normal);

            // Thiết lập IK 
            animator.SetIKPositionWeight(foot, ikWeight);
            animator.SetIKRotationWeight(foot, ikWeight);
            animator.SetIKPosition(foot, ikPosition);
            animator.SetIKRotation(foot, ikRotation);
        }
        else
        {
            // tắt IK cho chân 
            animator.SetIKPositionWeight(foot, 0f);
            animator.SetIKRotationWeight(foot, 0f);
        }
    }
}
