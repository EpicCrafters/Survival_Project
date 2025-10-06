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
        SetSubState(new ChaseState(controller));
    }

    public override void Update()
    {
        subState.Update();

        if (!controller.HasTarget())
            controller.ChangeState(new NormalState(controller));
    }

    public void SetSubState(State newSub)
    {
        subState?.OnExit();
        subState = newSub;
        subState.OnEnter();
    }
}

// -------------------- Chase --------------------
public class ChaseState : State
{
    public ChaseState(HFSMController c) : base(c) { }
    public override void OnEnter()
    {
        controller.agent.speed = controller.animalData.fleeSpeed;
        controller.agent.stoppingDistance = 1f;
        controller.animator.PlayChaseAnimation();
    }

    public override void Update()
    {
        if (!controller.HasTarget()) return;

        Transform target = controller.GetTargetTransform();
        float dist = Vector3.Distance(controller.transform.position, target.position);

        if (dist > controller.attackRange)
        {
            controller.agent.isStopped = false;
            controller.agent.SetDestination(target.position);
            controller.animator.UpdateAnimation();
        }
        else
        {
            controller.agent.isStopped = true;
            controller.agent.ResetPath();
            controller.agent.velocity = Vector3.zero; // force stop
            (controller.CurrentState as CombatState)?.SetSubState(new AttackState(controller));
        }
    }
}

    // -------------------- Attack --------------------
    public class AttackState : State
    {
        private bool hasAttacked = false;
        private bool isFacingTarget = false;

        public AttackState(HFSMController controller) : base(controller) { }

        public override void OnEnter()
        {
            controller.agent.isStopped = true;
            controller.animator.PlayAttackAnimation();
            isFacingTarget = false;
        }

        public override void Update()
        {
            if (!controller.HasTarget()) return;

            Transform target = controller.GetTargetTransform();
            Vector3 dir = (target.position - controller.transform.position).normalized;
            dir.y = 0f;

            // Smoothly rotate toward target
            if (dir != Vector3.zero)
            {
                Quaternion targetRot = Quaternion.LookRotation(dir);
                controller.transform.rotation = Quaternion.Slerp(
                    controller.transform.rotation,
                    targetRot,
                    Time.deltaTime * controller.rotationSpeed
                );
            }

            // Check if facing target
            float angle = Vector3.Angle(controller.transform.forward, dir);
            if (angle <= controller.facingAngleThreshold)
            {
                if (!isFacingTarget)
                {
                    isFacingTarget = true;
                    // Start attack coroutine once facing
                    controller.StartCoroutine(DelayedAttack());
                }
            }
        }

        private IEnumerator DelayedAttack()
        {
            if (hasAttacked) yield break;
            hasAttacked = true;

            // Fake wind-up
            yield return new WaitForSeconds(0.4f);

            if (controller.HasTarget() && controller.IsCloseToTarget())
                DoHit();

            // Go to recovery
            (controller.CurrentState as CombatState)?.SetSubState(new RecoveryState(controller));
        }

        private void DoHit()
        {

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

        public RecoveryState(HFSMController controller, float duration = -1f) : base(controller)
        {
            timer = duration > 0f ? duration : 2f;
        }

        public override void OnEnter()
        {
            controller.agent.isStopped = true;
            controller.animator.PlayIdleAnimation();
            Debug.Log($"{controller.name} is recovering for {timer} seconds...");
        }

        public override void Update()
        {
            timer -= Time.deltaTime;

            if (timer <= 0f)
            {
                if (controller.HasTarget())
                    (controller.CurrentState as CombatState)?.SetSubState(new ChaseState(controller));
                else
                    controller.ChangeState(new NormalState(controller));
            }
        }

        public override void OnExit()
        {
            controller.agent.isStopped = false;
        }
    }

