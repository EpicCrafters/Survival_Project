using UnityEngine;
using System.Collections.Generic;

public class AIRagdoll : MonoBehaviour
{
    [Header("References")]
    public Rigidbody[] ragdollBodies;
    public Collider[] ragdollColliders;
    public Collider baseCol;

    [Header("Body Parts Setup")]
    public List<AIBodyPart> bodyParts = new List<AIBodyPart>();

    [Header("Settings")]
    [SerializeField] private bool showGizmos = true;

    private bool isRagdoll = false;
    private Rigidbody baseRigidbody;

    [System.Serializable]
    public class AIBodyPart
    {
        public string name;
        public Transform bone;
        public Rigidbody rigidbody;
        public Collider collider;
        public float damageMultiplier = 1f;
    }

    void Awake()
    {
   
       

        // Thiết lập layers - AI sử dụng layer AIDamageable
        SetupLayers();

        // Bỏ qua va chạm giữa ragdoll và base collider
        if (baseCol != null)
        {
            foreach (var col in ragdollColliders)
            {
                if (col != baseCol && col != null)
                    Physics.IgnoreCollision(col, baseCol, true);
            }
        }

        SetRagdoll(false);
    }

  
    // Thiết lập layers - CHỈ các body parts ở layer AIDamageable
    // Base collider ở layer Default/Ignore Raycast để tránh mũi tên dính vào root
    
    private void SetupLayers()
    {
        int aiDamageableLayer = LayerMask.NameToLayer("AIDamageable");
        int ignoreRaycastLayer = LayerMask.NameToLayer("AIMain");

        // Base collider → layer Ignore Raycast (không bị hit bởi arrows khi còn sống)
        if (baseCol != null)
        {
            baseCol.gameObject.layer = ignoreRaycastLayer;
            baseCol.isTrigger = false; // Ban đầu là collider thật, không phải trigger
            Debug.Log($"[AIRagdoll] Base collider '{baseCol.name}' đã set sang Ignore Raycast layer");
        }

        // CHỈ các ragdoll body parts → layer AIDamageable
        foreach (var col in ragdollColliders)
        {
            if (col == baseCol) continue; // Bỏ qua base collider
            if (col == null) continue;

            col.gameObject.layer = aiDamageableLayer;
            Debug.Log($"[AIRagdoll] Body part '{col.name}' đã set sang AIDamageable layer");
        }

        Debug.Log($"[AIRagdoll] Thiết lập hoàn tất - {ragdollColliders.Length - 1} body parts ở AIDamageable layer");
    }

   
    // Bật/tắt chế độ ragdoll
   
    public void SetRagdoll(bool state)
    {
        isRagdoll = state;

        // Cấu hình rigidbodies
        foreach (var rb in ragdollBodies)
        {
            if (rb == baseRigidbody) continue;
            if (rb == null) continue;

            rb.isKinematic = !state;
            rb.useGravity = state;

            if (state)
            {
                rb.linearDamping = 0.5f;
                rb.angularDamping = 0.5f;
            }
        }

        // Cấu hình colliders - Luôn bật, chỉ toggle trạng thái trigger
        foreach (var col in ragdollColliders)
        {
            if (col == baseCol) continue;
            if (col == null) continue;

            col.enabled = true;
            col.isTrigger = !state; // Trigger khi còn sống, solid khi chết
        }

        // Base collider chỉ chuyển thành trigger KHI VÀO chế độ ragdoll (chết)
        // Khi còn sống, giữ nguyên collider thật để AI có thể va chạm bình thường
        if (baseCol != null)
        {
            baseCol.isTrigger = state; // true khi ragdoll (chết), false khi sống
        }
    }

   
    // Tìm ragdoll bone gần nhất với điểm va chạm
   
    public Rigidbody GetClosestBodyPart(Vector3 hitPoint, float searchRadius = 2f)
    {
        Rigidbody closest = null;
        float closestDist = float.MaxValue;

        foreach (var rb in ragdollBodies)
        {
            if (rb == baseRigidbody) continue;
            if (rb == null) continue;

            float dist = Vector3.Distance(rb.position, hitPoint);

            if (dist < closestDist)
            {
                if (searchRadius < 0 || dist <= searchRadius)
                {
                    closestDist = dist;
                    closest = rb;
                }
            }
        }

        return closest;
    }

  
    // Lấy hệ số sát thương dựa trên vị trí trúng đòn

    public float GetDamageMultiplier(Vector3 hitPoint, float searchRadius = 2f)
    {
        AIBodyPart closestPart = null;
        float closestDist = float.MaxValue;

        foreach (var part in bodyParts)
        {
            if (part.bone == null) continue;

            float dist = Vector3.Distance(part.bone.position, hitPoint);

            if (dist < closestDist)
            {
                if (searchRadius < 0 || dist <= searchRadius)
                {
                    closestDist = dist;
                    closestPart = part;
                }
            }
        }

        return closestPart != null ? closestPart.damageMultiplier : 1f;
    }

   
    // Lấy tên bộ phận cơ thể bị trúng đòn

    public string GetHitBodyPartName(Vector3 hitPoint, float searchRadius = 2f)
    {
        AIBodyPart closestPart = null;
        float closestDist = float.MaxValue;

        foreach (var part in bodyParts)
        {
            if (part.bone == null) continue;

            float dist = Vector3.Distance(part.bone.position, hitPoint);

            if (dist < closestDist)
            {
                if (searchRadius < 0 || dist <= searchRadius)
                {
                    closestDist = dist;
                    closestPart = part;
                }
            }
        }

        return closestPart != null ? closestPart.name : "Unknown";
    }

    public bool IsRagdollActive() => isRagdoll;

    // ==========================================================
    // HIỂN THỊ DEBUG
    // ==========================================================
    private void OnDrawGizmosSelected()
    {
        if (!showGizmos || bodyParts == null || bodyParts.Count == 0) return;

        foreach (var part in bodyParts)
        {
            if (part.bone == null) continue;

            // Màu sắc dựa trên hệ số sát thương
            if (part.damageMultiplier >= 2f)
                Gizmos.color = Color.red;
            else if (part.damageMultiplier >= 1f)
                Gizmos.color = Color.yellow;
            else
                Gizmos.color = Color.green;

            Gizmos.DrawWireSphere(part.bone.position, 0.1f);
        }
    }

    // ==========================================================
    // TIỆN ÍCH EDITOR
    // ==========================================================
    [ContextMenu("Setup Default Body Parts")]
    private void SetupDefaultBodyParts()
    {
        bodyParts.Clear();
        Transform[] allTransforms = GetComponentsInChildren<Transform>();

        foreach (var t in allTransforms)
        {
            string lowerName = t.name.ToLower();
            float multiplier = 1f;
            string partName = t.name;

            if (lowerName.Contains("head"))
            {
                multiplier = 2.5f;
                partName = "Head";
            }
            else if (lowerName.Contains("neck"))
            {
                multiplier = 2f;
                partName = "Neck";
            }
            else if (lowerName.Contains("chest") || lowerName.Contains("spine") || lowerName.Contains("upper"))
            {
                multiplier = 1f;
                partName = lowerName.Contains("chest") ? "Chest" : "Spine";
            }
            else if (lowerName.Contains("pelvis") || lowerName.Contains("hips"))
            {
                multiplier = 0.9f;
                partName = "Pelvis";
            }
            else if (lowerName.Contains("arm") || lowerName.Contains("forearm") ||
                     lowerName.Contains("hand") || lowerName.Contains("leg") ||
                     lowerName.Contains("calf") || lowerName.Contains("foot"))
            {
                multiplier = 0.7f;
                partName = t.name;
            }
            else
            {
                continue;
            }

            Rigidbody rb = t.GetComponent<Rigidbody>();
            Collider col = t.GetComponent<Collider>();

            if (rb != null && rb != baseRigidbody && col != null)
            {
                bodyParts.Add(new AIBodyPart
                {
                    name = partName,
                    bone = t,
                    rigidbody = rb,
                    collider = col,
                    damageMultiplier = multiplier
                });
            }
        }

        Debug.Log($"✅ Đã thiết lập {bodyParts.Count} body parts cho {gameObject.name}");
    }
}