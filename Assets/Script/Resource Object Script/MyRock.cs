using UnityEngine;

public class MyRock : MonoBehaviour, IDamageable, IMinenable
{
    // Loại đá (tạm thời chỉ có 1 loại)
    public enum Type
    {
        Rock,
    }

    [SerializeField] private Type rockType;
    [SerializeField] private Transform stonePrefab; // Prefab viên đá được sinh ra khi phá
    public int itemDrop; // Số lượng đá sẽ rơi ra

    private HealthSystem healthSystem;

    private void Awake()
    {
        int healthAmount;

        // Gán lượng máu theo loại đá
        switch (rockType)
        {
            default:
            case Type.Rock: healthAmount = 30; break;
        }

        healthSystem = new HealthSystem(healthAmount);
        healthSystem.OnDead += HealthSystem_OnDead;

        // Kiểm tra null prefab
        if (stonePrefab == null)
        {
            Debug.LogWarning($"{name}: stonePrefab chưa được gán trong Inspector!");
        }
    }

    // Gọi khi máu của đá về 0
    private void HealthSystem_OnDead()
    {
        // Sinh itemDrop viên đá nếu prefab hợp lệ
        for (int i = 0; i < itemDrop; i++)
        {
            if (stonePrefab != null)
            {
                Vector3 offset = new Vector3(Random.Range(-0.2f, 0.2f), 0.1f, Random.Range(-0.2f, 0.2f));
                Quaternion randomRot = Quaternion.Euler(0, Random.Range(0, 360), 0);
                Instantiate(stonePrefab, transform.position + offset, randomRot);
            }
        }

        // Xoá đối tượng đá gốc sau khi bị phá
        Destroy(gameObject);
    }

    // Gọi khi đá bị sát thương
    public void Damage(int amount)
    {
        if (healthSystem != null)
        {
            healthSystem.Damage(amount);
        }
        else
        {
            Debug.LogError($"{name}: HealthSystem chưa được khởi tạo!");
        }
    }

    // Trả về loại tài nguyên
    public ResourceType GetResourceType() => ResourceType.Rock;

    // Lấy hệ thống máu
    public HealthSystem GetHealthSystem()
    {
        return healthSystem;
    }
}
