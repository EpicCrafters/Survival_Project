using UnityEngine;
using Mirror;
using System.Collections.Generic;

[ExecuteInEditMode]
public class PlayerRagdoll : NetworkBehaviour
{
    [Header("Body Parts")]
    public List<BodyPart> bodyParts = new List<BodyPart>();

    [Header("References")]
    public Rigidbody[] ragdollBodies;      // Các rigidbody của ragdoll
    public Collider[] ragdollColliders;    // Các collider của ragdoll
    public CharacterController characterController; // CharacterController bình thường
    public PlayableAnimationBlender animationBlender; // Blend animation
    public Animator animator;               // Animator của player
    public Rigidbody baseRigidbody;
    [Header("Settings")]
    [SerializeField] private bool showGizmos = true; // Hiện gizmos trong Scene
    [SerializeField] private float getUpDelay = 2f;   // Thời gian delay trước khi đứng dậy

    private PlayerStatManager statManager;
    private bool isRagdoll = false;                // Trạng thái ragdoll
    private Vector3 storedVelocity;                // Tốc độ lưu trước khi chuyển sang ragdoll
    private Vector3 lastRootPosition;              // Vị trí root trước khi ragdoll

    [System.Serializable]
    public class BodyPart
    {
        public string name;               // Tên bộ phận
        public BodyPartType type;         // Loại bộ phận
        public Transform bone;            // Bone transform
        public Rigidbody rigidbody;       // Rigidbody của bone
        public Collider collider;         // Collider của bone
        public float damageMultiplier = 1f; // Hệ số sát thương
    }

    public enum BodyPartType
    {
        Head, Neck, Chest, Spine, Pelvis,
        LeftUpperArm, LeftForearm, RightUpperArm, RightForearm,
        LeftThigh, LeftCalf, RightThigh, RightCalf
    }

    private void Awake()
    {
        statManager = GetComponent<PlayerStatManager>();

        if (characterController == null)
            characterController = GetComponent<CharacterController>();

        if (animator == null)
            animator = GetComponent<Animator>();

        // Lấy toàn bộ rigidbody & collider trong ragdoll
        ragdollBodies = GetComponentsInChildren<Rigidbody>();
        ragdollColliders = GetComponentsInChildren<Collider>();

        // Setup layer để player có nhiều collider nhưng chỉ ragdoll bị bắn trúng
        SetupLayers();

        // Ignore collision giữa ragdoll và CharacterController
        if (characterController != null)
        {
            Collider ccCollider = characterController.GetComponent<Collider>();
            if (ccCollider != null)
            {
                foreach (var col in ragdollColliders)
                {
                    if (col != ccCollider && col != null)
                        Physics.IgnoreCollision(col, ccCollider, true);
                }
            }
        }

        lastRootPosition = transform.position;

        // Tắt ragdoll khi start
        SetRagdoll(false);
    }

    private void SetupLayers()
    {
        // Lấy layer “PlayerDamageable”
        int playerDamageableLayer = LayerMask.NameToLayer("PlayerDamageable");

        // CharacterController đặt vào Default để tránh bị bắn trúng
        int ignoreRaycastLayer = LayerMask.NameToLayer("PlayerMain");

        Collider ccCollider = null;
        if (characterController != null)
        {
            ccCollider = characterController.GetComponent<Collider>();
        }

        // CharacterController → layer Default
        if (ccCollider != null)
        {
            ccCollider.gameObject.layer = ignoreRaycastLayer;
        }

        // Toàn bộ ragdoll → PlayerDamageable
        foreach (var col in ragdollColliders)
        {
            if (col == null) continue;
            if (col == ccCollider) continue;

            col.gameObject.layer = playerDamageableLayer;
        }
    }

    // Auto setup bone trong edit mode
    [ContextMenu("Auto Setup Body Parts (Edit Mode)")]
    private void AutoSetupBodyPartsEditMode()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("Run Auto Setup in Edit Mode, not Play Mode.");
            return;
        }

        bodyParts.Clear();
        ragdollBodies = GetComponentsInChildren<Rigidbody>();
        ragdollColliders = GetComponentsInChildren<Collider>();

        if (animator == null)
            animator = GetComponent<Animator>();

        if (animator == null)
        {
            Debug.LogError("No Animator found!");
            return;
        }

        // Tự động thêm các body part
        AddBodyPart("Head", BodyPartType.Head, HumanBodyBones.Head, 2.5f);
        AddBodyPart("Neck", BodyPartType.Neck, HumanBodyBones.Neck, 2f);
        AddBodyPart("Chest", BodyPartType.Chest, HumanBodyBones.Chest, 1f);
        AddBodyPart("Spine", BodyPartType.Spine, HumanBodyBones.Spine, 1f);
        AddBodyPart("Pelvis", BodyPartType.Pelvis, HumanBodyBones.Hips, 0.9f);

        AddBodyPart("Left Upper Arm", BodyPartType.LeftUpperArm, HumanBodyBones.LeftUpperArm, 0.7f);
        AddBodyPart("Left Forearm", BodyPartType.LeftForearm, HumanBodyBones.LeftLowerArm, 0.6f);
        AddBodyPart("Right Upper Arm", BodyPartType.RightUpperArm, HumanBodyBones.RightUpperArm, 0.7f);
        AddBodyPart("Right Forearm", BodyPartType.RightForearm, HumanBodyBones.RightLowerArm, 0.6f);

        AddBodyPart("Left Thigh", BodyPartType.LeftThigh, HumanBodyBones.LeftUpperLeg, 0.8f);
        AddBodyPart("Left Calf", BodyPartType.LeftCalf, HumanBodyBones.LeftLowerLeg, 0.7f);
        AddBodyPart("Right Thigh", BodyPartType.RightThigh, HumanBodyBones.RightUpperLeg, 0.8f);
        AddBodyPart("Right Calf", BodyPartType.RightCalf, HumanBodyBones.RightLowerLeg, 0.7f);

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    private void AddBodyPart(string name, BodyPartType type, HumanBodyBones bone, float damageMultiplier)
    {
        Transform boneTransform = animator.GetBoneTransform(bone);

        if (boneTransform == null)
        {
            Debug.LogWarning($"⚠️ Bone {bone} not found!");
            return;
        }

        Rigidbody rb = boneTransform.GetComponent<Rigidbody>();
        Collider col = boneTransform.GetComponent<Collider>();

        if (rb == null || col == null)
        {
            Debug.LogWarning($"⚠️ Missing Rigidbody or Collider on {boneTransform.name}");
            return;
        }

        // Thêm body part
        bodyParts.Add(new BodyPart
        {
            name = name,
            type = type,
            bone = boneTransform,
            rigidbody = rb,
            collider = col,
            damageMultiplier = damageMultiplier
        });
    }

    // Bật / tắt ragdoll
    public void SetRagdoll(bool state)
    {
        if (state && isRagdoll) return;
        if (!state && !isRagdoll) return;
        isRagdoll = state;

        if (state)
            EnableRagdoll();
        else
            DisableRagdoll();
    }

    private void EnableRagdoll()
    {
        // Lưu vị trí trước khi ragdoll
        lastRootPosition = transform.position;

        // Lưu velocity để khi ngã thì ragdoll có quán tính
        CharacterController cc = GetComponent<CharacterController>();
        if (cc != null)
            storedVelocity = cc.velocity;

        // Bật physics cho từng bone
        foreach (var rb in ragdollBodies)
        {
            if (rb == null) continue;

            rb.isKinematic = false;
            rb.useGravity = true;
            rb.linearDamping = 0.5f;
            rb.angularDamping = 0.5f;
            rb.linearVelocity = storedVelocity;
        }
        if(baseRigidbody!= null)
        {
            baseRigidbody.useGravity = true;
            baseRigidbody.isKinematic=false;
        }
        // Bật collider vật lý
        foreach (var col in ragdollColliders)
        {
            if (col == null) continue;

            if (characterController != null && col == characterController.GetComponent<Collider>())
                continue;

            col.enabled = true;
            col.isTrigger = false;
        }

        // Tắt CharacterController + Animator
        if (characterController != null) characterController.enabled = false;
        if (animationBlender != null) animationBlender.enabled = false;
        if (animator != null) animator.enabled = false;
    }

    private void DisableRagdoll()
    {
        // Tắt physics
        foreach (var rb in ragdollBodies)
        {
            if (rb == null) continue;

            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        // Collider về trigger để nhận đòn đánh
        foreach (var col in ragdollColliders)
        {
            if (col == null) continue;

            if (characterController != null && col == characterController.GetComponent<Collider>())
                continue;

            col.enabled = true;
            col.isTrigger = true;
        }

        // Bật lại controller + animator
        if (characterController != null) characterController.enabled = true;
        if (animator != null) animator.enabled = true;
    }

    public void ApplyForce(Vector3 force, Vector3 hitPoint, float radius = 2f)
    {
        if (!isRagdoll)
        {
            Debug.LogWarning("Không thể apply force khi chưa ragdoll!");
            return;
        }

        // Tìm bone gần vị trí bị đánh nhất
        Rigidbody closestPart = GetClosestBodyPart(hitPoint, radius);

        if (closestPart != null)
        {
            closestPart.AddForceAtPosition(force, hitPoint, ForceMode.Impulse);
        }
        else
        {
            // Nếu không tìm thấy -> apply vào pelvis
            var pelvis = bodyParts.Find(p => p.type == BodyPartType.Pelvis);
            pelvis?.rigidbody?.AddForce(force, ForceMode.Impulse);
        }
    }

    // Tìm rigidbody gần vị trí hit nhất
    public Rigidbody GetClosestBodyPart(Vector3 hitPoint, float searchRadius = 2f)
    {
        Rigidbody closest = null;
        float closestDist = float.MaxValue;

        foreach (var part in bodyParts)
        {
            if (part.rigidbody == null || part.bone == null) continue;

            float dist = Vector3.Distance(part.bone.position, hitPoint);

            if (dist < closestDist && (searchRadius < 0 || dist <= searchRadius))
            {
                closestDist = dist;
                closest = part.rigidbody;
            }
        }

        return closest;
    }

    // Lấy loại body part dựa theo điểm bị bắn
    public BodyPartType GetBodyPartTypeFromHitPoint(Vector3 hitPoint, float searchRadius = 2f)
    {
        BodyPart closestPart = null;
        float closestDist = float.MaxValue;

        foreach (var part in bodyParts)
        {
            if (part.bone == null) continue;

            float dist = Vector3.Distance(part.bone.position, hitPoint);

            if (dist < closestDist && (searchRadius < 0 || dist <= searchRadius))
            {
                closestDist = dist;
                closestPart = part;
            }
        }

        return closestPart != null ? closestPart.type : BodyPartType.Chest;
    }

    // Lấy damage multiplier dựa theo chỗ bị đánh
    public float GetDamageMultiplier(Vector3 hitPoint, float searchRadius = 2f)
    {
        BodyPartType hitType = GetBodyPartTypeFromHitPoint(hitPoint, searchRadius);
        var bodyPart = bodyParts.Find(x => x.type == hitType);
        return bodyPart != null ? bodyPart.damageMultiplier : 1f;
    }

    // Lấy rigidbody của pelvis
    public Rigidbody GetPelvisRigidbody()
    {
        var pelvis = bodyParts.Find(p => p.type == BodyPartType.Pelvis);
        return pelvis?.rigidbody;
    }

    // Kiểm tra ragdoll
    public bool IsRagdollActive() => isRagdoll;

    // Gizmos để debug trong Scene
    private void OnDrawGizmosSelected()
    {
        if (!showGizmos || bodyParts == null || bodyParts.Count == 0) return;

        foreach (var part in bodyParts)
        {
            if (part.bone == null) continue;

            // Màu theo damage multiplier
            if (part.damageMultiplier >= 2f)
                Gizmos.color = Color.red;
            else if (part.damageMultiplier >= 1f)
                Gizmos.color = Color.yellow;
            else
                Gizmos.color = Color.green;

            Gizmos.DrawWireSphere(part.bone.position, 0.08f);

            if (part.type == BodyPartType.Pelvis)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(part.bone.position, 0.15f);
            }
        }
    }
}
