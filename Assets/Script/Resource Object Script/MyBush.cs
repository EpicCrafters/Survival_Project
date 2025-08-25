using UnityEngine;

public class Bush : MonoBehaviour, IDamageable, Iinteractable
{
    [SerializeField] private int maxPickup;                      // Số lần người chơi có thể hái berry
    [SerializeField] private GameObject berryMesh;               // Mesh của quả berry (ẩn/hiện)
    [SerializeField] private Transform berryDropPrefab;          // Prefab berry rơi ra khi bị phá
    [SerializeField] private Transform stickDropPrefab;          // Prefab stick rơi ra khi bị phá

    private bool hasBerry = true;                                // Cờ kiểm tra bụi còn berry không
    private HealthSystem healthSystem;

    private void Awake()
    {
        healthSystem = new HealthSystem(10);                      // Khởi tạo hệ thống máu
        healthSystem.OnDead += OnBushDestroyed;                  // Gắn sự kiện khi bị phá

        // Cảnh báo nếu thiếu prefab/mesh trong Inspector
        if (berryMesh == null) Debug.LogWarning($"{name}: berryMesh chưa được gán!");
        if (berryDropPrefab == null) Debug.LogWarning($"{name}: berryDropPrefab chưa được gán!");
        if (stickDropPrefab == null) Debug.LogWarning($"{name}: stickDropPrefab chưa được gán!");

        UpdateBerryVisual();                                     // Cập nhật hiển thị quả berry
    }

    // Gọi khi người chơi tương tác
    public void Interact()
    {
        if (!hasBerry || maxPickup <= 1)
        {
            Debug.Log("Không còn berry để hái.");
            hasBerry = false;
            UpdateBerryVisual();
            return;
        }

        maxPickup--;      // Giảm số lần hái
        Debug.Log("Đã hái berry!");

        if (maxPickup <= 0)
        {
            hasBerry = false;
        }

        UpdateBerryVisual();
    }

    // Gọi khi bụi cây nhận sát thương
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

    // Xử lý khi bụi cây bị phá huỷ
    private void OnBushDestroyed()
    {
        // Nếu còn berry thì spawn berryPrefab
        if (hasBerry && berryDropPrefab != null)
        {
            Instantiate(berryDropPrefab, transform.position + Vector3.up * 1.5f, Quaternion.identity);
        }

        // Luôn rơi từ 1 đến 2 que nếu prefab hợp lệ
        int stickCount = Random.Range(1, 3);
        for (int i = 0; i < stickCount; i++)
        {
            if (stickDropPrefab != null)
            {
                Vector3 offset = new Vector3(Random.Range(-0.2f, 0.2f), 0.1f, Random.Range(-0.2f, 0.2f));
                Instantiate(stickDropPrefab, transform.position + offset, Quaternion.identity);
            }
        }

        Destroy(gameObject);
    }

    // Ẩn hoặc hiện mesh quả berry dựa theo trạng thái
    private void UpdateBerryVisual()
    {
        if (berryMesh != null)
        {
            berryMesh.SetActive(hasBerry);
        }
    }

    // Trả về loại tài nguyên để tương tác
    public ResourceType GetResourceType() => ResourceType.Bush;
}
