using UnityEngine;
using Mirror;

[RequireComponent(typeof(Animator))]
public class HFSMAnimator : NetworkBehaviour
{
    public HFSMController controller;
    private Animator animator;

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

    private void Awake()
    {
        animator = GetComponent<Animator>();

    }

    private void Update()
    {
        // Smooth blend for locomotion (runs on all clients)
        currentBlendValue = Mathf.Lerp(currentBlendValue, targetBlendValue, Time.deltaTime * blendSpeed);

        if (isServer)
        {
            syncedMoveState = currentBlendValue;
        }

        animator.SetFloat("MoveState", currentBlendValue);
    }

    public void UpdateAnimation()
    {
        // This is called from states, already server-only
    }

    // -------------------------
    // Idle animations
    // -------------------------
    public void PlayIdleAnimation()
    {
        if (isServer)
        {
            syncedIsMoving = false;
            int r = Random.Range(1, controller.animalData.idleCount + 1);
            syncedIdleVariant = r;
        }
        else
        {
            // Apply locally if not server
            animator.SetBool("isMoving", false);
            int r = Random.Range(1, controller.animalData.idleCount + 1);
            animator.SetInteger("IdleVariant", r);
        }
    }

    public void PlayWanderAnimation()
    {
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
        if (isServer)
        {
            RpcPlayHitAnimation();
        }
        else
        {
            animator.SetTrigger("Hit");
        }
    }

    public void DisableAnimator()
    {
        if (animator != null)
            animator.enabled = false;
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
        animator.SetBool("isMoving", newValue);
    }

    private void OnIdleVariantChanged(int oldValue, int newValue)
    {
        animator.SetInteger("IdleVariant", newValue);
    }

    // -------------------------
    // RPCs for triggers
    // -------------------------
    [ClientRpc]
    private void RpcPlayAttackAnimation()
    {
        animator.SetBool("isMoving", false);
        animator.SetTrigger("Attack");
    }

    [ClientRpc]
    private void RpcPlayHitAnimation()
    {
        animator.SetTrigger("Hit");
    }
}