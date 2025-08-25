using UnityEngine;

[RequireComponent(typeof(Animator))]
public class AnimalAnimator : MonoBehaviour
{
    private Animator animator;
    private float targetState;
    public float State { get; private set; }
    public float blendSpeed = 2f;
    public float hitAnimationDuration = 2f;


    private void Awake()
    {
        animator = GetComponent<Animator>();
    }

    public void UpdateAnimation()
    {
        State = Mathf.MoveTowards(State, targetState, blendSpeed * Time.deltaTime);
        animator.SetFloat("State", State);
    }

    public void PlayIdleAnimation()
    {
        var animalData = GetComponent<AnimalAI>().animalData;
        if (animalData == null) return;

        if (animalData.idleCount > 1)
        {
            int r = Random.Range(1, animalData.idleCount + 1);
            animator.CrossFade("Idle_" + r, 0.25f);
        }
        else
        {
            animator.CrossFade("Idle_1", 0.25f);
        }

        targetState = 0f;
    }

    public void PlayMoveAnimation()
    {
        animator.CrossFade("Move", 0.25f);
        targetState = 1f;
    }

    public void PlayChaseAnimation()
    {
        animator.CrossFade("Move", 0.25f);
        targetState = 1.5f;
    }

    public void PlayHitAnimation()
    {
        animator.SetTrigger("Hit");
         targetState = 0f; 
    }

    public void PlayAttackAnimation()
    {
        animator.CrossFade("Attack", 0.1f);
        targetState = 0f;
    }
    public void DisableAnimator()
    {
        if (animator != null)
            animator.enabled = false;
    }

    public void ResetAllAnimations()
    {
        animator.Rebind();
        animator.Update(0f);
    }
    public void StopAnimation()
    {
        targetState = 0f;
    }
}
