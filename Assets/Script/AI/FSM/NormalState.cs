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

        // Nếu là Predator và phát hiện mục tiêu → Chuyển sang Combat
        if (controller.isPredator && controller.HasTarget())
            controller.ChangeState(new CombatState(controller));

        // Nếu KHÔNG phải Predator và phát hiện mục tiêu → Chuyển sang Survival (chạy trốn)
        if (!controller.isPredator && controller.HasTarget())
            controller.ChangeState(new SurvivalState(controller));
    }

    public void SetSubState(State newSub)
    {
        subState?.OnExit();
        subState = newSub;
        subState.OnEnter();
    }

    public override void OnExit()
    {
        subState?.OnExit();
    }
}

// ==========================================================
// 💤 IDLE STATE - Trạng thái đứng yên
// ==========================================================
public class IdleState : State
{
    private float timer;

    public IdleState(HFSMController controller) : base(controller) { }

    public override void OnEnter()
    {
        // Thời gian idle ngẫu nhiên từ 2-5 giây
        timer = Random.Range(2f, 5f);

        // Chơi animation idle
        controller.animator.PlayIdleAnimation();

        // Dừng di chuyển hoàn toàn
        controller.StopMovement();
    }

    public override void Update()
    {
        timer -= Time.deltaTime;

        if (timer <= 0f)
        {
            // Hết thời gian idle → Chuyển sang Wander
            (controller.CurrentState as NormalState)?.SetSubState(new WanderState(controller));
        }
    }
}

// ==========================================================
// 🚶 WANDER STATE - Trạng thái đi lang thang
// ==========================================================
public class WanderState : State
{
    private Vector3 wanderTarget;
    private float currentSpeed;

    public WanderState(HFSMController controller) : base(controller) { }

    public override void OnEnter()
    {
        // Tốc độ ngẫu nhiên để tạo sự tự nhiên
        currentSpeed = controller.animalData.moveSpeed * Random.Range(0.9f, 1.1f);

        // Chơi animation đi bộ
        controller.animator.PlayWanderAnimation();

        // Tìm điểm đến ngẫu nhiên có thể đi được
        wanderTarget = GetRandomWalkablePoint();

        if (wanderTarget != Vector3.zero)
        {
            // Đặt điểm đích
            controller.SetDestination(wanderTarget);
        }
        else
        {
            // Không tìm được điểm đến → Quay về Idle
            (controller.CurrentState as NormalState)?.SetSubState(new IdleState(controller));
        }
    }

    private Vector3 GetRandomWalkablePoint()
    {
        // Thử tối đa 10 lần để tìm điểm hợp lệ
        for (int i = 0; i < 10; i++)
        {
            // Tạo hướng ngẫu nhiên trong bán kính 30m
            Vector3 randomDirection = Random.insideUnitSphere * 30f;
            randomDirection.y = 0; // Chỉ di chuyển trên mặt phẳng XZ

            Vector3 targetPoint = controller.transform.position + randomDirection;

            // Bắn ray xuống từ phía trên để kiểm tra có mặt đất không
            Vector3 rayStart = targetPoint + Vector3.up * 10f;

            if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 20f, LayerMask.GetMask("Ground", "Terrain")))
            {
                // Kiểm tra độ dốc có hợp lý không
                float slope = Vector3.Angle(hit.normal, Vector3.up);

                if (slope <= 45f) // Độ dốc tối đa 45 độ
                {
                    return hit.point;
                }
            }
        }

        // Không tìm được điểm hợp lệ sau 10 lần thử
        Debug.LogWarning($"{controller.name}: Không tìm được điểm wander hợp lệ!");
        return Vector3.zero;
    }

    public override void Update()
    {
        // Cập nhật animation dựa trên tốc độ di chuyển
        controller.animator.UpdateAnimation();

        // ✅ SỬ DỤNG OBSTACLE AVOIDANCE
        controller.MoveToTargetDestinationWithAvoidance(currentSpeed);

        // Kiểm tra xem đã đến đích chưa
        if (!controller.IsMoving())
        {
            // Đã đến đích → Quay về Idle để nghỉ một lúc
            (controller.CurrentState as NormalState)?.SetSubState(new IdleState(controller));
        }
    }

    public override void OnExit()
    {
        // Dừng di chuyển khi thoát state
        controller.StopMovement();
    }
}