using UnityEngine;

// RabbitAI kế thừa từ BaseAnimalAI
// Quản lý hành vi của thỏ: chạy trốn khi có mối đe dọa
public class RabbitAI : BaseAnimalAI
{
    [Header("Rabbit Specific")]
    public float panicFleeDistance = 20f; // Khoảng cách mà thỏ sẽ chạy trốn khi hoảng sợ

    // Xử lý AI chính
    protected override void HandleAI()
    {
        // Sử dụng các logic AI chung từ BaseAnimalAI
        HandleCommonAI();
    }

    // Kiểm tra xem thỏ có nên phản ứng với mục tiêu hay không
    protected override bool ShouldReactToTarget(float distanceToTarget)
    {
        // Thỏ phản ứng với bất kỳ mục tiêu nào trong phạm vi phát hiện
        return distanceToTarget <= movement.detectionRange;
    }

    // Xử lý khi mục tiêu nằm trong phạm vi phát hiện
    protected override void HandleTargetInRange(float distanceToTarget)
    {
        // Thỏ luôn chạy trốn khi phát hiện mục tiêu
        if (currentState != AnimalState.Flee)
        {
            Debug.Log("Rabbit detected threat! Fleeing...");
            ChangeState(AnimalState.Flee); // Chuyển trạng thái sang Flee
        }
    }

    // Chuyển trạng thái thỏ
    public override void ChangeState(AnimalState newState)
    {
        base.ChangeState(newState);

        // Xử lý trạng thái riêng của thỏ
        if (newState == AnimalState.Flee)
        {
            Debug.Log("Rabbit is fleeing from danger!");
            // Có thể thêm các hành vi riêng:
            // - Tăng tốc độ
            // - Di chuyển zigzag
            // - Phát ra âm thanh cảnh báo, v.v.
        }
    }

    // Khi thỏ nhận sát thương
    public override void Damage(int amount)
    {
        base.Damage(amount);

        // Thỏ trở nên cảnh giác hơn sau khi bị tấn công
        if (!isDead)
        {
            movement.detectionRange *= 1.2f; // Tăng phạm vi phát hiện
            Debug.Log("Rabbit is now more alert to threats!");
        }
    }
}
