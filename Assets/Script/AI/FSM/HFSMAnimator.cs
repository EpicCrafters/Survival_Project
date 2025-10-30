using UnityEngine;
using Mirror;

[RequireComponent(typeof(Animator))]
public class HFSMAnimator : NetworkBehaviour
{
    public HFSMController controller;
    public Animator animator;

    [Header("Settings")]
    public float blendSpeed = 2f;

    // Network synced animation parameters
    [SyncVar(hook = nameof(OnMoveStateChanged))]
    private float syncedMoveState;

    [SyncVar(hook = nameof(OnIsMovingChanged))]
    private bool syncedIsMoving;

    [SyncVar(hook = nameof(OnIdleVariantChanged))]
    private int syncedIdleVariant;

    private float currentBlendValue;
    private float targetBlendValue;

    // Track disabled state
    private bool isAnimatorManuallyDisabled = false;

    private void Awake()
    {
        animator = GetComponent<Animator>();
    }

    // ✅ ĐƯỢC GỌI TỪ AIEntity.TickAI()
    public void UpdateAnimation()
    {
        // Don't update if animator is disabled
        if (animator == null || !animator.enabled || isAnimatorManuallyDisabled)
            return;

        // Smooth blend for locomotion (runs on all clients)
        currentBlendValue = Mathf.Lerp(currentBlendValue, targetBlendValue, Time.deltaTime * blendSpeed);

        if (isServer)
        {
            syncedMoveState = targetBlendValue;
        }

        animator.SetFloat("MoveState", currentBlendValue);
    }

    // -------------------------
    // Idle animations
    // -------------------------
    public void PlayIdleAnimation()
    {
        if (animator == null || !animator.enabled) return;

        if (isServer)
        {
            syncedIsMoving = false;
            int r = Random.Range(1, controller.animalData.idleCount + 1);
            syncedIdleVariant = r;

            if (isClient)
            {
                animator.SetBool("isMoving", false);
                animator.SetInteger("IdleVariant", r);
            }
        }
        else
        {
            animator.SetBool("isMoving", false);
            int r = Random.Range(1, controller.animalData.idleCount + 1);
            animator.SetInteger("IdleVariant", r);
        }
    }

    public void PlayWanderAnimation()
    {
        if (animator == null || !animator.enabled) return;

        if (isServer)
        {
            syncedIsMoving = true;
            targetBlendValue = 0.5f;
        }
        else
        {
            animator.SetBool("isMoving", true);
            targetBlendValue = 0.5f;
        }
    }

    public void PlayChaseAnimation()
    {
        if (animator == null || !animator.enabled) return;

        if (isServer)
        {
            syncedIsMoving = true;
            targetBlendValue = 1.5f;
        }
        else
        {
            animator.SetBool("isMoving", true);
            targetBlendValue = 1.5f;
        }
    }

    // -------------------------
    // Action animations
    // -------------------------
    public void PlayAttackAnimation()
    {
        if (animator == null || !animator.enabled) return;

        if (isServer)
        {
            syncedIsMoving = false;
            RpcPlayAttackAnimation();
        }
        else
        {
            animator.SetBool("isMoving", false);
            animator.SetTrigger("Attack");
        }
    }

    public void PlayHitAnimation()
    {
        if (animator == null || !animator.enabled) return;

        if (isServer)
        {
            RpcPlayHitAnimation();
        }
        else
        {
            animator.SetTrigger("Hit");
        }
    }
    //Ragdoll setting
    public void DisableAnimatorForRagdoll()
    {
        if (animator!=null) {
            animator.enabled = false;
        }
    }

    // -------------------------
    // 💤 SLEEP/WAKE CONTROL - SIMPLIFIED (NO DISABLE)
    // -------------------------
    public void DisableAnimator()
    {
        // Don't actually disable - just mark as sleeping
        isAnimatorManuallyDisabled = true;

        // Set to idle/frozen state
        if (animator != null)
        {
            animator.SetBool("isMoving", false);
            animator.SetFloat("MoveState", 0f);
            targetBlendValue = 0f;
            currentBlendValue = 0f;
        }

        Debug.Log($"[HFSMAnimator] 💤 Animator sleeping (but still enabled) for {gameObject.name}");
    }

    public void EnableAnimator()
    {
        // Just mark as awake - animator stays enabled
        isAnimatorManuallyDisabled = false;

        // Force re-sync animation state
        if (animator != null)
        {
            ResyncAnimationState();
        }

        Debug.Log($"[HFSMAnimator] ⏰ Animator awake for {gameObject.name}");
    }

    /// <summary>
    /// Force re-sync all animation parameters after waking up
    /// </summary>
    private void ResyncAnimationState()
    {
        if (animator == null || !animator.enabled) return;

        Debug.Log($"[HFSMAnimator] 🔄 Resyncing state - isMoving: {syncedIsMoving}, MoveState: {syncedMoveState}, IdleVariant: {syncedIdleVariant}");

        // Re-apply all synced parameters
        animator.SetBool("isMoving", syncedIsMoving);
        animator.SetInteger("IdleVariant", syncedIdleVariant);
        animator.SetFloat("MoveState", syncedMoveState);

        // Update target blend value
        targetBlendValue = syncedMoveState;
        currentBlendValue = syncedMoveState;

        // Force an immediate animation update
        if (animator.enabled)
        {
            animator.Update(0f);
        }

        Debug.Log($"[HFSMAnimator] ✅ State resynced successfully");
    }

    // -------------------------
    // SyncVar Hooks
    // -------------------------
    private void OnMoveStateChanged(float oldValue, float newValue)
    {
        targetBlendValue = newValue;
    }

    private void OnIsMovingChanged(bool oldValue, bool newValue)
    {
        if (animator != null && animator.enabled)
            animator.SetBool("isMoving", newValue);
    }

    private void OnIdleVariantChanged(int oldValue, int newValue)
    {
        if (animator != null && animator.enabled)
            animator.SetInteger("IdleVariant", newValue);
    }

    // -------------------------
    // RPCs for triggers
    // -------------------------
    [ClientRpc]
    private void RpcPlayAttackAnimation()
    {
        if (animator != null && animator.enabled)
        {
            animator.SetBool("isMoving", false);
            animator.SetTrigger("Attack");
        }
    }

    [ClientRpc]
    private void RpcPlayHitAnimation()
    {
        if (animator != null && animator.enabled)
        {
            animator.SetTrigger("Hit");
        }
    }

    // -------------------------
    // Debug Helper
    // -------------------------
    [ContextMenu("Print Animator State")]
    private void PrintAnimatorState()
    {
        if (animator != null)
        {
            Debug.Log($"=== ANIMATOR STATE: {gameObject.name} ===");
            Debug.Log($"Enabled: {animator.enabled}");
            Debug.Log($"Manually Disabled: {isAnimatorManuallyDisabled}");
            Debug.Log($"IsMoving: {syncedIsMoving}");
            Debug.Log($"MoveState: {syncedMoveState}");
            Debug.Log($"Current Blend: {currentBlendValue}");
            Debug.Log($"Target Blend: {targetBlendValue}");

            if (animator.enabled)
            {
                var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
                Debug.Log($"Current Animation State: {stateInfo.shortNameHash}");
                Debug.Log($"Normalized Time: {stateInfo.normalizedTime}");
            }
        }
    }
}