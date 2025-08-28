using UnityEngine;

// BaseResource là lớp trừu tượng (abstract) quản lý các resource chung trong game
// Các resource như cây, đá, khoáng sản sẽ kế thừa lớp này
public abstract class BaseResource : MonoBehaviour, IDamageable
{
    [Header("Base Resource Settings")]
    [SerializeField] protected int baseHealth = 30;           // Máu cơ bản nếu resource không override
    [SerializeField] protected ResourceType resourceType;     // Loại resource (Tree, Rock, v.v.)

    [Header("Drop Settings")]
    [SerializeField] protected int minDropCount = 1;          // Số lượng rơi tối thiểu
    [SerializeField] protected int maxDropCount = 3;          // Số lượng rơi tối đa
    [SerializeField] protected float dropRadius = 0.2f;       // Bán kính rơi ra xung quanh resource
    [SerializeField] protected float dropHeight = 0.1f;       // Chiều cao spawn khi rơi

    protected HealthSystem healthSystem;       // Hệ thống quản lý máu
    protected bool isDestroyed = false;        // Kiểm tra resource đã bị xóa chưa
    protected bool isBeingDestroyed = false;   // Ngăn gọi hủy nhiều lần

    // Awake được gọi khi object được load
    protected virtual void Awake()
    {
        // Ngăn không khởi tạo nhiều lần
        if (healthSystem != null) return;

        InitializeHealth();                 // Khởi tạo máu theo loại resource
        healthSystem.OnDead += OnResourceDestroyed; // Đăng ký sự kiện khi resource chết
        ValidateComponents();               // Kiểm tra prefab, thành phần cần thiết
    }

    // Các phương thức trừu tượng, bắt buộc lớp con implement
    protected abstract void InitializeHealth();       // Khởi tạo máu riêng từng loại
    protected abstract void OnResourceDestroyed();    // Logic khi resource bị phá
    protected abstract void ValidateComponents();     // Kiểm tra các prefab/thiết lập

    public abstract ResourceType GetResourceType();   // Trả về loại resource

    // Xử lý nhận sát thương
    public virtual void Damage(int amount)
    {
        if (isDestroyed || isBeingDestroyed || healthSystem == null) return;

        healthSystem.Damage(amount);       // Trừ máu
        OnDamageReceived(amount);           // Gọi hàm xử lý thêm
    }

    // Xử lý thêm khi resource nhận sát thương (có thể override)
    protected virtual void OnDamageReceived(int amount)
    {
        Debug.Log($"{gameObject.name} took {amount} damage. Health: {healthSystem.GetHealth()}");
    }

    // Trả về HealthSystem để các script khác có thể kiểm tra máu
    public virtual HealthSystem GetHealthSystem()
    {
        return healthSystem;
    }

    // Hàm spawn các drop chung (cây, đá, khoáng sản,...)
    protected void SpawnDrops(Transform prefab, int count, Vector3 basePosition)
    {
        if (prefab == null) return;

        for (int i = 0; i < count; i++)
        {
            // Random vị trí xung quanh basePosition theo dropRadius
            Vector3 offset = new Vector3(
                Random.Range(-dropRadius, dropRadius),
                dropHeight,
                Random.Range(-dropRadius, dropRadius)
            );
            // Random xoay quanh trục Y
            Quaternion randomRotation = Quaternion.Euler(0, Random.Range(0, 360), 0);
            Instantiate(prefab, basePosition + offset, randomRotation);
        }
    }

    // Xóa resource khỏi scene
    protected virtual void DestroyResource()
    {
        if (isDestroyed) return;

        isDestroyed = true;
        Destroy(gameObject); // Xóa GameObject khỏi scene
    }
}
