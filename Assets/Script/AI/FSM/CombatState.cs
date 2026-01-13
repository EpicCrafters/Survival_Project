using System.Collections;
using UnityEngine;

public class CombatState : State
{
    private State subState;

    public CombatState(HFSMController controller) : base(controller) { }

    public override void OnEnter()
    {
        // Bắt đầu bằng Chase (đuổi theo mục tiêu)
        SetSubState(new ChaseState(controller));
    }

    public override void Update()
    {
        // Cập nhật sub-state hiện tại
        subState.Update();

        // Nếu mất mục tiêu → Quay về NormalState
        if (!controller.HasTarget())
            controller.ChangeState(new NormalState(controller));
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
// 🏃 CHASE STATE - Trạng thái đuổi theo mục tiêu
// ==========================================================
public class ChaseState : State
{
    public ChaseState(HFSMController c) : base(c) { }

    public override void OnEnter()
    {
        // Chơi animation chạy
        controller.animator.PlayChaseAnimation();
    }

    public override void Update()
    {
        if (!controller.HasTarget()) return;

        Transform target = controller.GetTargetTransform();
        float dist = Vector3.Distance(controller.transform.position, target.position);

        // Nếu còn xa → Tiếp tục đuổi theo
        if (dist > controller.attackRange)
        {
            // Cập nhật điểm đích là vị trí mục tiêu (liên tục)
            controller.SetDestination(target.position);

            // ✅ SỬ DỤNG OBSTACLE AVOIDANCE
            controller.MoveToTargetDestinationWithAvoidance(controller.animalData.fleeSpeed);

            // Cập nhật animation dựa trên tốc độ
            controller.animator.UpdateAnimation();
        }
        else
        {
            // Đã đến khoảng cách tấn công → Dừng lại và chuyển sang Attack
            controller.StopMovement();
            (controller.CurrentState as CombatState)?.SetSubState(new AttackState(controller));
        }
    }

    public override void OnExit()
    {
        // Dừng di chuyển khi thoát khỏi Chase
        controller.StopMovement();
    }
}

// ==========================================================
// 💥 ATTACK STATE - Trạng thái tấn công
// ==========================================================
public class AttackState : State
{
    private bool hasAttacked = false;
    private bool isFacingTarget = false;

    public AttackState(HFSMController controller) : base(controller) { }

    public override void OnEnter()
    {
        // Dừng di chuyển hoàn toàn khi tấn công
        controller.StopMovement();

        // Chơi animation tấn công
        controller.animator.PlayAttackAnimation();

        isFacingTarget = false;
        hasAttacked = false;
    }

    public override void Update()
    {
        if (!controller.HasTarget()) return;

        Transform target = controller.GetTargetTransform();
        Vector3 dir = (target.position - controller.transform.position).normalized;
        dir.y = 0f; // Chỉ xoay trên mặt phẳng XZ

        // Xoay mượt về phía mục tiêu
        if (dir != Vector3.zero)
        {
            Quaternion targetRot = Quaternion.LookRotation(dir);
            controller.transform.rotation = Quaternion.Slerp(
                controller.transform.rotation,
                targetRot,
                Time.deltaTime * 10f // Tốc độ xoay
            );
        }

        // Kiểm tra xem đã quay đủ về phía mục tiêu chưa
        float angle = Vector3.Angle(controller.transform.forward, dir);
        if (angle <= controller.facingAngleThreshold)
        {
            if (!isFacingTarget)
            {
                isFacingTarget = true;
                // Đã quay đủ → Bắt đầu tấn công
                controller.StartCoroutine(DelayedAttack());
            }
        }
    }

    private IEnumerator DelayedAttack()
    {
        if (hasAttacked) yield break;
        hasAttacked = true;

        // Thời gian chuẩn bị đòn đánh (wind-up)
        yield return new WaitForSeconds(0.4f);

        // Thực hiện tấn công nếu còn mục tiêu và ở gần
        if (controller.HasTarget() && controller.IsCloseToTarget())
            DoHit();

        // Sau khi tấn công → Chuyển sang Recovery
        (controller.CurrentState as CombatState)?.SetSubState(new RecoveryState(controller));
    }

    private void DoHit()
    {
        Debug.Log($"{controller.name} đã tấn công mục tiêu!");

        // TODO: Thêm logic gây sát thương
        // Ví dụ:
        // if (controller.HasTarget())
        // {
        //     var damageable = controller.GetTargetTransform().GetComponent<IDamageable>();
        //     damageable?.Damage(controller.animalData.attackDamage, hitInfo);
        // }
    }

    public override void OnExit()
    {
        // Không cần làm gì đặc biệt
    }
}

// ==========================================================
// 🛡️ RECOVERY STATE - Trạng thái hồi phục
// ==========================================================
public class RecoveryState : State
{
    private float timer;

    public RecoveryState(HFSMController controller, float duration = -1f) : base(controller)
    {
        // Nếu không truyền duration, mặc định là 2 giây
        timer = duration > 0f ? duration : 2f;
    }

    public override void OnEnter()
    {
        // Dừng di chuyển trong lúc hồi phục
        controller.StopMovement();

        // Chơi animation idle
        controller.animator.PlayIdleAnimation();

        Debug.Log($"{controller.name} đang hồi phục trong {timer} giây...");
    }

    public override void Update()
    {
        timer -= Time.deltaTime;

        // Hết thời gian hồi phục
        if (timer <= 0f)
        {
            if (controller.HasTarget())
            {
                // Còn mục tiêu → Quay lại Chase để tiếp tục đuổi
                (controller.CurrentState as CombatState)?.SetSubState(new ChaseState(controller));
            }
            else
            {
                // Mất mục tiêu → Về NormalState
                controller.ChangeState(new NormalState(controller));
            }
        }
    }

    public override void OnExit()
    {
        // Không cần làm gì đặc biệt
    }
}