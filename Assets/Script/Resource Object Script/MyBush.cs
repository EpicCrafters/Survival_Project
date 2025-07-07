using UnityEngine;

public class Bush : MonoBehaviour, IDamageable, Iinteractable
{
    [SerializeField] private int maxPickup;                   // Số lần người chơi có thể hái berry
    [SerializeField] private GameObject berryMesh;                // Mesh của quả berry (ẩn/hiện)
    [SerializeField] private Transform berryDropPrefab;           // Prefab berry rơi ra khi bị phá
    [SerializeField] private Transform stickDropPrefab;           // Prefab stick rơi ra khi bị phá

    private bool hasBerry = true;                                 // Cờ kiểm tra bụi còn berry không
    private HealthSystem healthSystem;

    private void Awake()
    {
        healthSystem = new HealthSystem(10);                      // Máu của bụi cây
        healthSystem.OnDead += OnBushDestroyed;                   // Đăng ký sự kiện khi bụi bị phá
        UpdateBerryVisual();                                      // Cập nhật trạng thái hiển thị berry
    }

    // Hàm gọi khi người chơi nhấn phím tương tác
    public void Interact()
    {
        if (!hasBerry || maxPickup <= 1)
        {
            Debug.Log("Không còn berry để hái.");
            hasBerry = false;
            UpdateBerryVisual();
            return;
        }

        maxPickup--;              // Giảm số lần có thể hái
               // Đánh dấu đã hái
            // Ẩn berry khỏi mesh
        Debug.Log("Đã hái berry!");

       
    }

    // Gọi khi bụi cây nhận sát thương
    public void Damage(int amount)
    {
        healthSystem.Damage(amount);
    }

    // Khi bụi cây bị phá huỷ hoàn toàn
    private void OnBushDestroyed()
    {
        // Nếu còn berry thì rơi ra berry
        if (hasBerry && berryDropPrefab != null)
        {
            Instantiate(berryDropPrefab, transform.position + Vector3.up * 1.5f, Quaternion.identity);
        }

        // Luôn rơi từ 1 đến 2 que
        int stickCount = Random.Range(1, 3);
        for (int i = 0; i < stickCount; i++)
        {
            Vector3 offset = new Vector3(Random.Range(-0.2f, 0.2f), 0.1f, Random.Range(-0.2f, 0.2f));
            Instantiate(stickDropPrefab, transform.position + offset, Quaternion.identity);
        }

        Destroy(gameObject); // Xoá bụi cây
    }

    // Cập nhật mesh của berry 
    private void UpdateBerryVisual()
    {
        if (berryMesh != null)
            berryMesh.SetActive(hasBerry);
    }

    // Trả về loại tài nguyên để hệ thống tương tác nhận diện
    public ResourceType GetResourceType() => ResourceType.Bush;
}
