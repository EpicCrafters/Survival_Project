using UnityEngine;

[CreateAssetMenu(fileName = "New Projectile", menuName = "Combat/Projectile Data")]
public class ProjectileData : ScriptableObject
{
    [Header("Projectile Info")]
    [Tooltip("Name of this projectile type")]
    public string projectileName = "Arrow";
    // Tên của loại đạn / mũi tên này

    [Header("Prefabs")]
    [Tooltip("The actual projectile that gets spawned and flies")]
    public GameObject projectilePrefab;
    // Prefab viên đạn thật sự được bắn ra 

    [Tooltip("Visual representation shown while drawing/aiming")]
    public GameObject visualPrefab;
    // Prefab hiển thị khi kéo dây cung, không phải viên đạn thật

    [Header("Damage")]
    [Tooltip("Minimum damage at 0% charge")]
    public float minDamage = 5f;
    // Sát thương khi kéo cung ít nhất (0%)

    [Tooltip("Maximum damage at 100% charge")]
    public float maxDamage = 25f;
    // Sát thương khi kéo cung tối đa (100%)

    [Header("Range")]
    [Tooltip("Minimum speed/range at 0% charge")]
    public float minSpeed;
    // Tốc độ bay thấp nhất khi bắn không kéo

    [Tooltip("Maximum speed/range at 100% charge")]
    public float maxSpeed;
    // Tốc độ bay cao nhất khi kéo cung tối đa

    [Header("Charge Settings")] // ✨ NEW SECTION
    [Tooltip("Maximum time to fully charge the bow (in seconds)")]
    public float maxChargeTime = 2f;
    // Thời gian tối đa để kéo cung đầy (giây)
    // Ví dụ: 2 giây = sau 2 giây giữ chuột thì đạt 100% sát thương

    [Header("Physics")]
    [Tooltip("Gravity multiplier for this projectile")]
    public float gravityMultiplier = 1f;
    // Hệ số trọng lực (1 = như Unity, >1 = rơi nhanh hơn)

    [Tooltip("How long the projectile exists before auto-destroying")]
    public float lifetime = 10f;
    // Thời gian tồn tại tối đa của đạn trước khi tự hủy

    [Header("Impact")]
    [Tooltip("Minimum velocity required for projectile to stick to surfaces")]
    public float minVelocityToStick = 2f;
    // Tốc độ tối thiểu để đạn có thể dính vào bề mặt
    // Nếu tốc độ nhỏ hơn thì theo mặc định sẽ rơi/không dính (tùy code xử lý)

    [Tooltip("How long stuck projectiles remain before despawning")]
    public float stuckLifetime = 5f;
    // Thời gian tồn tại khi đạn đã dính vào bề mặt trước khi biến mất

    /// <summary>
    /// Lấy sát thương dựa trên % kéo cung
    /// </summary>
    public float GetDamage(float chargePercent)
    {
        chargePercent = Mathf.Clamp01(chargePercent);
        return Mathf.Lerp(minDamage, maxDamage, chargePercent);
    }

    /// <summary>
    /// Lấy tốc độ dựa trên % kéo cung
    /// </summary>
    public float GetSpeed(float chargePercent)
    {
        chargePercent = Mathf.Clamp01(chargePercent);
        return Mathf.Lerp(minSpeed, maxSpeed, chargePercent);
    }

    /// <summary>
    /// Lấy cả sát thương và tốc độ
    /// </summary>
    public (float damage, float speed) GetProjectileStats(float chargePercent)
    {
        chargePercent = Mathf.Clamp01(chargePercent);
        float damage = Mathf.Lerp(minDamage, maxDamage, chargePercent);
        float speed = Mathf.Lerp(minSpeed, maxSpeed, chargePercent);
        return (damage, speed);
    }

    /// <summary>
    /// Tính charge percent dựa trên thời gian đã giữ
    /// </summary>
    /// <param name="heldTime">Thời gian đã giữ (giây)</param>
    /// <returns>Phần trăm charge từ 0 đến 1</returns>
    public float CalculateChargePercent(float heldTime)
    {
        return Mathf.Clamp01(heldTime / maxChargeTime);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Đảm bảo các giá trị min/max hợp lý khi chỉnh trong Inspector
        if (maxDamage < minDamage)
            maxDamage = minDamage;
        if (maxSpeed < minSpeed)
            maxSpeed = minSpeed;

        minDamage = Mathf.Max(0f, minDamage);
        minSpeed = Mathf.Max(0f, minSpeed);
        maxChargeTime = Mathf.Max(0.1f, maxChargeTime); // ✨ Đảm bảo maxChargeTime > 0
    }
#endif
}