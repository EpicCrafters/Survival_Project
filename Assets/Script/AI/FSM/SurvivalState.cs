using UnityEngine;
using UnityEngine.AI;

// ===========================
// SurvivalState (Trạng thái sinh tồn với Flee)
// ===========================
public class SurvivalState : State
{
    private State subState;
    private float safeDistance = 15f; // Khoảng cách an toàn để dừng chạy trốn

    public SurvivalState(HFSMController controller) : base(controller) { }

    public override void OnEnter()
    {
        Debug.Log("Vào trạng thái Survival");

        // Nếu có mục tiêu, bắt đầu chạy trốn
        if (controller.HasTarget())
        {
            // Chỉ truyền controller, substate sẽ là FleeState
            SetSubState(new FleeState(controller));
        }
        else
        {
            // Nếu không có mục tiêu, chuyển sang trạng thái Idle
            SetSubState(new IdleState(controller));
        }
    }

    public override void Update()
    {
        subState.Update();

        // Nếu không còn mục tiêu nữa, trở về trạng thái Normal
        if (!controller.HasTarget())
        {
            controller.ChangeState(new NormalState(controller));
        }
    }

    // Hàm để set subState mới
    public void SetSubState(State newSub)
    {
        subState?.OnExit(); // Thoát trạng thái cũ nếu có
        subState = newSub;
        subState.OnEnter(); // Vào trạng thái mới
    }

    public override void OnExit()
    {
        subState?.OnExit();
        subState = null;
    }
}

// ===========================
// FleeState (Trạng thái chạy trốn)
// ===========================
public class FleeState : State
{
    private HFSMController controller;
    private float fleeDistance = 20f; // Khoảng cách chạy trốn
    private float recalcInterval = 0.3f; // Khoảng thời gian tính lại đường đi
    private float recalcTimer = 0f;

    public FleeState(HFSMController controller) : base(controller)
    {
        this.controller = controller;
    }

    public override void OnEnter()
    {
        // Set tốc độ chạy trốn
        controller.agent.speed = controller.animalData.fleeSpeed;

        // Bật animation chạy trốn
        controller.animator.PlayChaseAnimation();

        controller.agent.isStopped = false;

        recalcTimer = 0f;
        SetNewFleeDestination(); // Tính toán điểm đến đầu tiên
    }

    public override void Update()
    {
        // Nếu không còn thấy kẻ thù hoặc chết, trở về Idle
        if (!controller.enemySpotted || controller.IsDead())
        {
            ExitToIdle();
            return;
        }

        // Tính lại đường đi định kỳ
        recalcTimer -= Time.deltaTime;
        if (recalcTimer <= 0f)
        {
            SetNewFleeDestination();
            recalcTimer = recalcInterval;
        }

        // Cập nhật animation
        controller.animator.UpdateAnimation();

        // Nếu bị kẹt, tính lại đường đi
        if (!controller.agent.pathPending &&
            controller.agent.remainingDistance <= controller.agent.stoppingDistance + 0.1f)
        {
            SetNewFleeDestination();
        }
    }

    // Hàm tính điểm chạy trốn mới
    private void SetNewFleeDestination()
    {
        if (!controller.HasTarget()) return;

        Vector3 threatPos = controller.GetTargetTransform().position;
        Vector3 dirFromThreat = (controller.transform.position - threatPos).normalized;

        // Thêm offset ngẫu nhiên để đường đi không bị lặp
        Vector3 randomOffset = new Vector3(
            Random.Range(-5f, 5f),
            0f,
            Random.Range(-5f, 5f)
        );

        Vector3 fleeTarget = controller.transform.position + dirFromThreat * fleeDistance + randomOffset;

        // Lấy vị trí hợp lệ trên NavMesh
        if (NavMesh.SamplePosition(fleeTarget, out var hit, 10f, NavMesh.AllAreas))
        {
            controller.agent.SetDestination(hit.position);
        }
    }

    // Chuyển về trạng thái Idle
    private void ExitToIdle()
    {
        SurvivalState survivalState = controller.CurrentState as SurvivalState;
        if (survivalState != null)
        {
            survivalState.SetSubState(new IdleState(controller));
        }
    }

    public override void OnExit()
    {
        // Dừng agent khi thoát trạng thái
        controller.agent.ResetPath();
        controller.agent.isStopped = true;
    }
}
