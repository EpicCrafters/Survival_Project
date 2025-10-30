using System.Collections;
using UnityEngine;
using UnityEngine.AI;

// CombatState manages sub-states: Chase, Attack, Recovery
public class CombatState : State
{
    private State subState;

    public CombatState(HFSMController controller) : base(controller) { }

    public override void OnEnter()
    {
        //Debug.Log("Enter Combat State");
        SetSubState(new ChaseState(controller));
    }

    public override void Update()
    {
        subState?.Update();

        if (!controller.HasTarget())
            controller.ChangeState(new NormalState(controller));
    }

    public void SetSubState(State newSub)
    {
        subState?.OnExit();
        subState = newSub;
        subState?.OnEnter();
    }
}

// -------------------- Chase --------------------
public class ChaseState : State
{
    public ChaseState(HFSMController c) : base(c) { }

    public override void OnEnter()
    {
        //Debug.Log("Enter Chase State");
        controller.agent.speed = controller.animalData.fleeSpeed;
        controller.agent.stoppingDistance = 1f;
        controller.agent.acceleration = 12f; // Faster acceleration
        controller.animator.PlayChaseAnimation();
    }

    public override void Update()
    {
        if (!controller.HasTarget()) return;

        Transform target = controller.GetTargetTransform();
        float dist = Vector3.Distance(controller.transform.position, target.position);
        controller.animator.UpdateAnimation();

        if (dist > controller.attackRange)
        {
            controller.agent.isStopped = false;
            controller.agent.SetDestination(target.position);
        }
        else
        {
            // Instant stop for snappier feel
            controller.agent.isStopped = true;
            controller.agent.ResetPath();
            controller.agent.velocity = Vector3.zero;
            (controller.CurrentState as CombatState)?.SetSubState(new AttackState(controller));
        }
    }
}

// -------------------- Attack --------------------
public class AttackState : State
{
    private bool hasAttacked = false;
    private bool isFacingTarget = false;
    private float rotationTimer = 0f;
    private const float MAX_ROTATION_TIME = 0.3f; // Reduced timeout - don't wait forever
    private const float FAST_ROTATION_MULTIPLIER = 3f; // 3x faster rotation

    public AttackState(HFSMController controller) : base(controller) { }

    public override void OnEnter()
    {
        //Debug.Log("Enter Attack State");
        controller.agent.isStopped = true;
        controller.animator.PlayAttackAnimation();
        isFacingTarget = false;
        rotationTimer = 0f;
        hasAttacked = false;
    }

    public override void Update()
    {
        if (!controller.HasTarget()) return;

        Transform target = controller.GetTargetTransform();
        Vector3 dir = (target.position - controller.transform.position).normalized;
        dir.y = 0f;

        // Much faster rotation during attack
        if (dir != Vector3.zero)
        {
            Quaternion targetRot = Quaternion.LookRotation(dir);
            controller.transform.rotation = Quaternion.Slerp(
                controller.transform.rotation,
                targetRot,
                Time.deltaTime * controller.rotationSpeed * FAST_ROTATION_MULTIPLIER
            );
        }

        rotationTimer += Time.deltaTime;

        // Check if facing target (more lenient angle)
        float angle = Vector3.Angle(controller.transform.forward, dir);

        // Attack if facing OR timeout (prevents getting stuck)
        if ((angle <= controller.facingAngleThreshold * 1.5f || rotationTimer >= MAX_ROTATION_TIME) && !isFacingTarget)
        {
            isFacingTarget = true;
            controller.StartCoroutine(DelayedAttack());
        }
    }

    private IEnumerator DelayedAttack()
    {
        if (hasAttacked) yield break;
        hasAttacked = true;

        // Minimal wind-up for snappier attacks
        yield return new WaitForSeconds(0.15f);

        if (controller.HasTarget() && controller.IsCloseToTarget())
            DoHit();

        // 2 second recovery as requested
        (controller.CurrentState as CombatState)?.SetSubState(new RecoveryState(controller, 2f));
    }

    private void DoHit()
    {
        // Your damage logic here
        //Debug.Log($"{controller.name} performed attack!");
    }

    public override void OnExit()
    {
        controller.agent.isStopped = false;
    }
}

// -------------------- Recovery --------------------
public class RecoveryState : State
{
    private float timer;
    private bool canChaseEarly = false;
    private const float EARLY_CHASE_THRESHOLD = 0.5f; // Start chasing after 0.5s even if cooling down

    public RecoveryState(HFSMController controller, float duration = -1f) : base(controller)
    {
        timer = duration > 0f ? duration : 2f; // 2 seconds default
    }

    public override void OnEnter()
    {
        //Debug.Log($"Enter Recovery State ({timer}s cooldown)");
        controller.agent.isStopped = true;
        controller.animator.PlayIdleAnimation();
        canChaseEarly = false;
    }

    public override void Update()
    {
        timer -= Time.deltaTime;

        // Allow chasing after partial cooldown (more responsive)
        if (timer <= (2f - EARLY_CHASE_THRESHOLD))
        {
            canChaseEarly = true;
        }

        // Full cooldown done - can attack again
        if (timer <= 0f)
        {
            if (controller.HasTarget())
                (controller.CurrentState as CombatState)?.SetSubState(new ChaseState(controller));
            else
                controller.ChangeState(new NormalState(controller));
        }
        // If target moves far during recovery, start chasing early
        else if (canChaseEarly && controller.HasTarget())
        {
            Transform target = controller.GetTargetTransform();
            float dist = Vector3.Distance(controller.transform.position, target.position);

            // If target is running away, chase them (but can't attack yet)
            if (dist > controller.attackRange * 1.5f)
            {
                (controller.CurrentState as CombatState)?.SetSubState(new ChaseState(controller));
            }
        }
    }

    public override void OnExit()
    {
        controller.agent.isStopped = false;
    }
}