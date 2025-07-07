using NUnit.Framework.Constraints;
using System.Drawing;
using UnityEngine;

public class MyTree : MonoBehaviour, IDamageable, IMinenable
{
    // Các loại đối tượng cây
    public enum Type
    {
        Tree,       // Cây hoàn chỉnh
        Log,        // Khúc gỗ
        LogHaft,    // Nửa khúc gỗ
        Stump       // Gốc cây
    }

    // Kích cỡ của cây
    public enum Size
    {
        Small,      // Nhỏ
        Medium,     // Vừa
        Large       // Lớn
    }

    [SerializeField] private Size treeSize;                // Kích cỡ cây được gán trong Inspector
    [SerializeField] private Type treeType;                // Loại cây được gán trong Inspector
    [SerializeField] private Transform fxTreeDestroyed;  // Hiệu ứng khi cây bị phá huỷ
    //[SerializeField] private Transform fxTreeLogDestroyed;
    //[SerializeField] private Transform fxTreeLogHalfDestroyed;
    //[SerializeField] private Transform fxTreeStumpDestroyed;

    [SerializeField] private Transform particleSpawnPosition;

    [SerializeField] private Transform treeLog;            // Prefab khúc gỗ
    [SerializeField] private Transform treeLogHalf;        // Prefab nửa khúc gỗ
    [SerializeField] private Transform treeStump;          // Prefab gốc cây
    [SerializeField] private Transform stickPrefab;        // Prefab que gỗ
    private HealthSystem healthSystem;                     // Hệ thống máu của cây

    private void Awake()
    {
        int healthAmount;

        // Thiết lập máu ban đầu dựa theo loại cây
        switch (treeType)
        {
            default:
            case Type.Tree: healthAmount = 30; break;
            case Type.Log: healthAmount = 50; break;
            case Type.LogHaft: healthAmount = 50; break;
            case Type.Stump: healthAmount = 50; break;
        }

        healthSystem = new HealthSystem(healthAmount);             // Khởi tạo hệ thống máu
        healthSystem.OnDead += HealthSystem_OnDead;               // Đăng ký sự kiện khi cây chết
    }

    private void HealthSystem_OnDead()
    {
        // Xử lý khi cây bị phá huỷ
        switch (treeType)
        {
            default:
            case Type.Tree:
                // Sinh khúc gỗ và gốc cây khi cây chính bị đốn
                Vector3 treeLogOffset = transform.up * 0.2f;
                Instantiate(treeLog, transform.position + treeLogOffset,
                    Quaternion.Euler(Random.Range(-1.5f, +1.5f), 0, Random.Range(-1.5f, +1.5f)));
                Instantiate(treeStump, transform.position, transform.rotation);
                Instantiate(fxTreeDestroyed, particleSpawnPosition.position, particleSpawnPosition.rotation);
                break;

            case Type.Log:
                // Khi khúc gỗ bị phá, sinh ra nhiều nửa khúc gỗ theo kích thước cây
                int halfLogCount = treeSize switch
                {
                    Size.Small => 2,
                    Size.Medium => 4,
                    Size.Large => 6,
                    
                };

                float halfLogOffset = 13.0f;

                for (int i = 0; i < halfLogCount; i++)
                {
                    // Tính vị trí và hướng xoay dựa trên hướng của khúc gỗ gốc
                    Vector3 offset = transform.up * halfLogOffset * i;
                    Quaternion rotation = Quaternion.LookRotation(transform.forward, transform.up);
                    rotation *= Quaternion.Euler(0, Random.Range(0f, 360f), 0); // Xoay ngẫu nhiên quanh trục đứng

                    Instantiate(treeLogHalf, transform.position + offset, rotation);
                }
                break;

            case Type.LogHaft:
                // Khi nửa khúc gỗ bị phá, sinh ra que gỗ
                for (int i = 0; i < 3; i++)
                {
                    Vector3 offset = new Vector3(Random.Range(-0.2f, 0.2f), 0.1f, Random.Range(-0.2f, 0.2f));
                    Quaternion randomRot = Quaternion.Euler(0, Random.Range(0, 360), 0);
                    Instantiate(stickPrefab, transform.position + offset, randomRot);
                }
                break;

            case Type.Stump:
                for (int i = 0; i < 3; i++)
                {
                    Vector3 offset = new Vector3(Random.Range(-0.2f, 0.2f), 0.1f, Random.Range(-0.2f, 0.2f));
                    Quaternion randomRot = Quaternion.Euler(0, Random.Range(0, 360), 0);
                    Instantiate(stickPrefab, transform.position + offset, randomRot);
                }
                break;
                // Gốc cây bị phá
                
        }

        Destroy(gameObject); // Xoá đối tượng gốc sau khi phá huỷ
    }

    // Gọi khi cây nhận sát thương
    public void Damage(int amount)
    {
        healthSystem.Damage(amount);
    }

    // Xử lý va chạm
    private void OnCollisionEnter(Collision collision)
    {
        Debug.Log($"Va chạm với {collision.gameObject.name}");

        // Ví dụ xử lý va chạm gây sát thương 
        // IDamageable damageSource = collision.gameObject.GetComponent<IDamageable>();
        // if (damageSource != null && collision.relativeVelocity.magnitude > 1f)
        // {
        //     int damageAmount = Random.Range(5, 20);
        //     DamagePopup.Create(collision.GetContact(0).point, damageAmount, damageAmount > 14);
        //     Damage(damageAmount);
        // }
    }

    // Gọi khi bắt đầu chặt
   

    // Trả về loại tài nguyên
    public ResourceType GetResourceType() => ResourceType.Tree;


    public HealthSystem GetHealthSystem()
    {
        return healthSystem;
    }
}
