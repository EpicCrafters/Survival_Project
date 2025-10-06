using UnityEngine.AI;
using UnityEngine;

public class NormalState : State
{
    private State subState;

    public NormalState(HFSMController controller) : base(controller) { }

    public override void OnEnter()
    {
        SetSubState(new IdleState(controller));
    }

    public override void Update()
    {
        subState.Update();

        // Predator sees target → Combat
        if (controller.isPredator && controller.HasTarget())
            controller.ChangeState(new CombatState(controller));
    }

    public void SetSubState(State newSub)
    {
        subState?.OnExit();
        subState = newSub;
        subState.OnEnter();
    }
}

public class IdleState : State
{
    private float timer;
    public IdleState(HFSMController controller) : base(controller) { }

    public override void OnEnter()
    {
        timer = Random.Range(2f, 5f);
        controller.animator.PlayIdleAnimation();
        controller.agent.ResetPath();
    }

    public override void Update()
    {
        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            (controller.CurrentState as NormalState)?.SetSubState(new WanderState(controller));
        }
    }
}

public class WanderState : State
{
    private Vector3 wanderTarget;
    public WanderState(HFSMController controller) : base(controller) { }

    public override void OnEnter()
    {
        controller.agent.speed = controller.animalData.moveSpeed * Random.Range(0.9f, 1.1f);
        controller.animator.PlayWanderAnimation();

        wanderTarget = GetRandomPoint();
        if (wanderTarget != Vector3.zero)
        {
            controller.agent.isStopped = false;
            controller.agent.SetDestination(wanderTarget);
        }
        else
        {
            (controller.CurrentState as NormalState)?.SetSubState(new IdleState(controller));
        }
    }

    private Vector3 GetRandomPoint()
    {
        for (int i = 0; i < 10; i++)
        {
            Vector3 dir = Random.insideUnitSphere * 30f;
            dir += controller.transform.position;
            if (NavMesh.SamplePosition(dir, out var hit, 30f, NavMesh.AllAreas))
                return hit.position;
        }
        return Vector3.zero;
    }

    public override void Update()
    {
        controller.animator.UpdateAnimation();
        if (!controller.agent.pathPending &&
            controller.agent.remainingDistance <= controller.agent.stoppingDistance + 0.2f)
        {
            controller.agent.ResetPath();
            (controller.CurrentState as NormalState)?.SetSubState(new IdleState(controller));
        }
    }

    public override void OnExit()
    {
        controller.agent.ResetPath();
    }
}
