using UnityEngine;

// MyRock kế thừa BaseResource và implement IMinenable
// Quản lý các loại đá, từ đá nhỏ đến hòn đá lớn (Boulder)
public class MyRock : BaseResource, IMinenable
{
    // Loại đá
    public enum RockType { SmallRock, MediumRock, LargeRock, Boulder }

    [Header("Rock Specific")]
    [SerializeField] private RockType rockType = RockType.MediumRock; // Loại đá hiện tại
    [SerializeField] private Transform stonePrefab;                   // Prefab các mảnh đá khi phá

    [Header("Rock Effects")]
    [SerializeField] private Transform dustEffectPrefab;              // Hiệu ứng bụi khi phá đá

    // Khởi tạo máu cho đá dựa trên loại
    protected override void InitializeHealth()
    {
        int healthAmount = rockType switch
        {
            RockType.SmallRock => 20,  // Đá nhỏ có ít máu
            RockType.MediumRock => 30, // Đá vừa
            RockType.LargeRock => 45,  // Đá lớn
            RockType.Boulder => 60,    // Hòn đá to (khó phá)
            _ => 30
        };

        healthSystem = new HealthSystem(healthAmount); // Gán hệ thống máu
        resourceType = ResourceType.Rock;             // Gán loại resource
    }

    // Hàm gọi khi đá bị phá hủy
    protected override void OnResourceDestroyed()
    {
        SpawnStones();       // Spawn mảnh đá rơi ra
        SpawnDustEffect();   // Spawn hiệu ứng bụi
        DestroyResource();   // Xóa object khỏi scene
    }

    // Spawn các mảnh đá rơi ra khi phá
    private void SpawnStones()
    {
        int stoneCount = rockType switch
        {
            RockType.SmallRock => Random.Range(1, 3),  // Đá nhỏ => 1-2 mảnh
            RockType.MediumRock => Random.Range(2, 4), // Đá vừa => 2-3 mảnh
            RockType.LargeRock => Random.Range(3, 6),  // Đá lớn => 3-5 mảnh
            RockType.Boulder => Random.Range(5, 8),    // Boulder => 5-7 mảnh
            _ => Random.Range(2, 4)
        };

        SpawnDrops(stonePrefab, stoneCount, transform.position); // Spawn các prefab đá
    }

    // Spawn hiệu ứng bụi khi phá đá
    private void SpawnDustEffect()
    {
        if (dustEffectPrefab != null)
        {
            Instantiate(dustEffectPrefab, transform.position, Quaternion.identity);
        }
    }

    // Hàm xử lý khi đá nhận sát thương
    protected override void OnDamageReceived(int amount)
    {
        base.OnDamageReceived(amount);

        // Có thể thêm feedback đặc thù cho đá ở đây
        // Ví dụ: văng mảnh đá nhỏ, âm thanh nứt đá, v.v.
    }

    // Kiểm tra các prefab đã được gán chưa
    protected override void ValidateComponents()
    {
        if (stonePrefab == null)
            Debug.LogWarning($"{name}: stonePrefab not assigned!");
    }

    // Trả về loại resource
    public override ResourceType GetResourceType() => ResourceType.Rock;
}
