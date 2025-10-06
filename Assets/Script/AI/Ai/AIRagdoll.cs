using UnityEngine;

public class AIRagdoll : MonoBehaviour
{
    [Header("References")]
    public Rigidbody[] ragdollBodies;
    public Collider[] ragdollColliders;
    public Collider baseCol;
    private bool isRagdoll = false;

    void Awake()
    {
        foreach (var col in ragdollColliders)
        {
            Physics.IgnoreCollision(col, baseCol, true);
        }

        ragdollBodies = GetComponentsInChildren<Rigidbody>();
        ragdollColliders = GetComponentsInChildren<Collider>();

        // disable ragdoll at start
        SetRagdoll(false);
    }

    public void SetRagdoll(bool state)
    {
        isRagdoll = state;

        foreach (var rb in ragdollBodies)
        {
            if (rb == GetComponent<Rigidbody>()) continue; // skip root rigidbody
            rb.isKinematic = !state;
            rb.useGravity = state;
        }

        foreach (var cap in ragdollColliders)
        {
            if (cap == GetComponent<CapsuleCollider>()) continue;
            if (cap.gameObject == this.gameObject) continue; // skip root collider
            cap.enabled = state;
        }
    }

  
    
}
