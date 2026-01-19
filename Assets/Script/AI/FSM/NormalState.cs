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

            // ✅ BƯỚC 1: Kiểm tra có vật cản (rock, tree) tại vị trí này không
            // Sử dụng SphereCast để kiểm tra xung quanh vị trí đó
            Vector3 checkPosition = targetPoint + Vector3.up * 2f; // Kiểm tra từ phía trên

            // Kiểm tra có vật cản trong bán kính 1.5m (đủ rộng cho AI đi qua)
            if (Physics.CheckSphere(checkPosition, 1.5f, controller.obstacleLayer))
            {
                // Có vật cản → Bỏ qua điểm này
                continue;
            }

            // ✅ BƯỚC 2: Bắn ray xuống từ phía trên để kiểm tra có mặt đất không
            Vector3 rayStart = targetPoint + Vector3.up * 10f;

            if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 20f, LayerMask.GetMask("Ground", "Terrain")))
            {
                // ✅ BƯỚC 3: Kiểm tra độ dốc có hợp lý không
                float slope = Vector3.Angle(hit.normal, Vector3.up);

                if (slope <= 45f) // Độ dốc tối đa 45 độ
                {
                    // ✅ BƯỚC 4: Kiểm tra lại xem tại điểm đích có vật cản không
                    // (double-check vì có thể có vật nhỏ sát mặt đất)
                    Vector3 finalCheckPos = hit.point + Vector3.up * 0.5f;

                    if (!Physics.CheckSphere(finalCheckPos, 1.0f, controller.obstacleLayer))
                    {
                        // ✅ TẤT CẢ ĐIỀU KIỆN ĐỀU ĐẠT → Điểm này hợp lệ!
                        return hit.point;
                    }
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