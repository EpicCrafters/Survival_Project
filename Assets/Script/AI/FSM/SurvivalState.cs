using UnityEngine;

public class SurvivalState : State
{
    private State subState;
    private float safeDistance = 15f; // Khoảng cách an toàn để dừng chạy trốn

    public SurvivalState(HFSMController controller) : base(controller) { }

    public override void OnEnter()
    {
        Debug.Log($"{controller.name} vào trạng thái Survival - Chạy trốn!");

        // Nếu có mục tiêu nguy hiểm → Bắt đầu chạy trốn
        if (controller.HasTarget())
        {
            SetSubState(new FleeState(controller));
        }
        else
        {
            // Không có mục tiêu → Chuyển sang Idle (đứng yên)
            SetSubState(new IdleState(controller));
        }
    }

    public override void Update()
    {
        // Cập nhật sub-state hiện tại
        subState.Update();

        // Nếu không còn mục tiêu nữa (thoát khỏi tầm nhìn) → Trở về NormalState
        if (!controller.HasTarget())
        {
            controller.ChangeState(new NormalState(controller));
        }
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
        subState = null;
    }
}

// ==========================================================
// 🏃‍♀️ FLEE STATE - Trạng thái chạy trốn
// ==========================================================
public class FleeState : State
{
    private float fleeDistance = 20f; // Khoảng cách chạy trốn
    private float recalcInterval = 0.5f; // Khoảng thời gian tính lại đường đi
    private float recalcTimer = 0f;
    private Vector3 currentFleeTarget;

    public FleeState(HFSMController controller) : base(controller) { }

    public override void OnEnter()
    {
        // Chơi animation chạy (dùng chase animation vì cùng là chạy nhanh)
        controller.animator.PlayChaseAnimation();

        recalcTimer = 0f;
        CalculateNewFleeDestination(); // Tính toán điểm trốn đầu tiên
    }

    public override void Update()
    {
        // Nếu không còn thấy kẻ thù hoặc đã chết → Dừng lại
        if (!controller.enemySpotted || controller.IsDead())
        {
            ExitToIdle();
            return;
        }

        // Tính lại đường đi định kỳ (để tránh bị đuổi kịp)
        recalcTimer -= Time.deltaTime;
        if (recalcTimer <= 0f)
        {
            CalculateNewFleeDestination();
            recalcTimer = recalcInterval;
        }

        // ✅ SỬ DỤNG OBSTACLE AVOIDANCE
        controller.MoveToTargetDestinationWithAvoidance(controller.animalData.fleeSpeed);

        // Cập nhật animation dựa trên tốc độ
        controller.animator.UpdateAnimation();

        // Nếu đã đến điểm trốn hiện tại → Tính điểm mới
        if (!controller.IsMoving())
        {
            CalculateNewFleeDestination();
        }
    }

    private void CalculateNewFleeDestination()
    {
        if (!controller.HasTarget()) return;

        Vector3 threatPos = controller.GetTargetTransform().position;

        // Tính hướng NGƯỢC LẠI với mối đe dọa
        Vector3 dirFromThreat = (controller.transform.position - threatPos).normalized;

        // Thêm offset ngẫu nhiên để đường đi không bị lặp lại và tự nhiên hơn
        Vector3 randomOffset = new Vector3(
            Random.Range(-5f, 5f),
            0f,
            Random.Range(-5f, 5f)
        );

        // Tính điểm đích flee (xa khỏi mối đe dọa)
        Vector3 fleeTarget = controller.transform.position + dirFromThreat * fleeDistance + randomOffset;
        fleeTarget.y = controller.transform.position.y; // Giữ cùng độ cao

        // Kiểm tra xem điểm đích có mặt đất hợp lệ không
        Vector3 validFleePoint = FindValidFleePoint(fleeTarget);

        if (validFleePoint != Vector3.zero)
        {
            currentFleeTarget = validFleePoint;
            controller.SetDestination(currentFleeTarget);
        }
        else
        {
            // Không tìm được điểm hợp lệ → Cố gắng chạy thẳng ra xa
            Debug.LogWarning($"{controller.name}: Không tìm được điểm flee hợp lệ, chạy thẳng!");

            // Chạy thẳng ra xa mà không cần raycast
            Vector3 straightFlee = controller.transform.position + dirFromThreat * fleeDistance;
            controller.SetDestination(straightFlee);
        }
    }

    private Vector3 FindValidFleePoint(Vector3 targetPoint)
    {
        // Thử tìm mặt đất tại điểm mục tiêu
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

        // Nếu không tìm được, thử các điểm xung quanh (8 hướng)
        for (int i = 0; i < 8; i++)
        {
            float angle = i * 45f * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * 5f;
            Vector3 altPoint = targetPoint + offset;

            rayStart = altPoint + Vector3.up * 10f;

            if (Physics.Raycast(rayStart, Vector3.down, out hit, 20f, LayerMask.GetMask("Ground", "Terrain")))
            {
                float slope = Vector3.Angle(hit.normal, Vector3.up);
                if (slope <= 45f)
                {
                    return hit.point;
                }
            }
        }

        return Vector3.zero; // Không tìm được điểm hợp lệ
    }

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
        // Dừng di chuyển khi thoát trạng thái flee
        controller.StopMovement();
        Debug.Log($"{controller.name} đã thoát khỏi trạng thái Flee");
    }
}