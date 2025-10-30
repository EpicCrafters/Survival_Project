using UnityEngine;
using UnityEngine.AI;

public class NormalState : State
{
    private State subState;

    public NormalState(HFSMController controller) : base(controller) { }

    public override void OnEnter()
    {
        //Debug.Log($"[{controller.name}] Enter Normal State");
        SetSubState(new IdleState(controller));
    }

    public override void Update()
    {
        // Update substate first (this is important!)
        subState?.Update();

        // Then check for state transitions
        // Predators hunt targets
        if (controller.isPredator && controller.HasTarget())
        {
            //Debug.Log($"[{controller.name}] Predator found target, switching to Combat");
            controller.ChangeState(new CombatState(controller));
            return;
        }

        // Prey flees from threats
        else if (!controller.isPredator && controller.HasTarget())
        {
            //Debug.Log($"[{controller.name}] Prey spotted threat, switching to Survival");
            controller.ChangeState(new SurvivalState(controller));
            return;
        }

        // No targets - stay in normal state with Idle/Wander
    }

    public void SetSubState(State newSub)
    {
        subState?.OnExit();
        subState = newSub;
        subState?.OnEnter();
    }

    public override void OnExit()
    {
        //Debug.Log($"[{controller.name}] Exit Normal State");
        subState?.OnExit();
        subState = null;
    }
}

public class IdleState : State
{
    private float timer;
    private float initialTime;

    public IdleState(HFSMController controller) : base(controller) { }

    public override void OnEnter()
    {
        initialTime = Random.Range(2f, 5f);
        timer = initialTime;
        //Debug.Log($"[{controller.name}] Enter Idle State (will idle for {initialTime:F2}s)");

        controller.animator.PlayIdleAnimation();

        if (controller.agent != null&& controller.agent.isOnNavMesh)
        {
            controller.agent.ResetPath();
            controller.agent.isStopped = true;
        }
    }

    public override void Update()
    {
        timer -= Time.deltaTime;

        // Debug every second
        if (Mathf.FloorToInt(timer) != Mathf.FloorToInt(timer + Time.deltaTime))
        {
            //Debug.Log($"[{controller.name}] Idle timer: {timer:F1}s remaining");
        }

        if (timer <= 0f)
        {
           // Debug.Log($"[{controller.name}] Idle finished after {initialTime:F2}s, switching to Wander");

            NormalState normalState = controller.CurrentState as NormalState;
            if (normalState != null)
            {
                normalState.SetSubState(new WanderState(controller));
            }
            else
            {
                //Debug.LogWarning($"[{controller.name}] Parent state is not NormalState! Current: {controller.CurrentState?.GetType().Name}");
            }
        }
    }

    public override void OnExit()
    {
        if (controller.agent != null)
            controller.agent.isStopped = false;

        float elapsed = initialTime - timer;
        //Debug.Log($"[{controller.name}] Exit Idle after {elapsed:F2}s (was supposed to be {initialTime:F2}s)");
    }
}

public class WanderState : State
{
    private Vector3 wanderTarget;

    public WanderState(HFSMController controller) : base(controller) { }

    public override void OnEnter()
    {
        //Debug.Log($"[{controller.name}] Enter Wander State");

        controller.agent.speed = controller.animalData.moveSpeed * Random.Range(0.9f, 1.1f);
        controller.animator.PlayWanderAnimation();

        wanderTarget = GetRandomPoint();

        if (wanderTarget != Vector3.zero)
        {
            //Debug.Log($"[{controller.name}] Wandering to {wanderTarget} (distance: {Vector3.Distance(controller.transform.position, wanderTarget):F1}m)");
            controller.agent.isStopped = false;
            controller.agent.SetDestination(wanderTarget);
        }
        else
        {
            //Debug.LogWarning($"[{controller.name}] Failed to find wander target, returning to Idle");
            NormalState normalState = controller.CurrentState as NormalState;
            if (normalState != null)
            {
                normalState.SetSubState(new IdleState(controller));
            }
        }
    }

    private Vector3 GetRandomPoint()
    {
        for (int i = 0; i < 10; i++)
        {
            Vector3 randomDir = Random.insideUnitSphere * 30f;
            Vector3 randomPoint = controller.transform.position + randomDir;

            if (NavMesh.SamplePosition(randomPoint, out var hit, 30f, NavMesh.AllAreas))
            {
                return hit.position;
            }
        }

        //Debug.LogWarning($"[{controller.name}] Could not find valid NavMesh position after 10 attempts");
        return Vector3.zero;
    }

    public override void Update()
    {
        if (controller.agent == null) return;

        controller.animator.UpdateAnimation();

        // Check if reached destination
        if (!controller.agent.pathPending &&
            controller.agent.remainingDistance <= controller.agent.stoppingDistance + 0.2f)
        {
            //Debug.Log($"[{controller.name}] Reached wander destination, returning to Idle");
            controller.agent.ResetPath();

            NormalState normalState = controller.CurrentState as NormalState;
            if (normalState != null)
            {
                normalState.SetSubState(new IdleState(controller));
            }
        }
    }

    public override void OnExit()
    {
        //Debug.Log($"[{controller.name}] Exit Wander State");
        if (controller.agent != null)
            controller.agent.ResetPath();
    }
}