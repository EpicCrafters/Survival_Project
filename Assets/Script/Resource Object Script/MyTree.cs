using System.Drawing;
using UnityEngine;

// MyTree kế thừa BaseResource và implement IMinenable
// Đây là script quản lý các loại cây, khúc gỗ, khúc gỗ nửa và gốc cây
public class MyTree : BaseResource, IMinenable
{
    // Loại cây
    public enum TreeType { Tree, Log, LogHalf, Stump }
    // Kích thước cây, dùng để xác định số lượng khúc gỗ nửa spawn ra
    public enum TreeSize { Small, Medium, Large }

    [Header("Tree Specific")]
    [SerializeField] private TreeType treeType = TreeType.Tree;
    [SerializeField] private TreeSize treeSize = TreeSize.Medium;

    [Header("Tree Prefabs")]
    [SerializeField] private Transform treeLogPrefab;      // Prefab khúc gỗ đầy đủ
    [SerializeField] private Transform treeLogHalfPrefab;  // Prefab khúc gỗ nửa
    [SerializeField] private Transform treeStumpPrefab;    // Prefab gốc cây
    [SerializeField] private Transform stickPrefab;        // Prefab que/cành nhỏ rơi ra

    [Header("Effects")]
    [SerializeField] private Transform destructionFxPrefab; // Hiệu ứng khi cây bị phá
    [SerializeField] private Transform particleSpawnPosition; // Vị trí spawn particle

    [Header("Log Halves Settings")]
    [SerializeField] private float halfLogSpacing = 1.2f;  // Khoảng cách giữa các khúc gỗ nửa khi spawn
    [SerializeField] private bool useColliderBounds = true; // Tự động lấy chiều dài khúc gỗ từ collider
    [SerializeField] private float manualLogLength = 5f;   // Nếu không có collider thì dùng chiều dài mặc định

    [Header("Tree Data")]
    [HideInInspector] public UVMapTreeSpawner.TreeData treeData; // Thông tin cây từ spawner
    [HideInInspector] public UVMapTreeSpawner spawner;           // Tham chiếu tới spawner

    // Khởi tạo máu cho cây dựa trên loại cây
    protected override void InitializeHealth()
    {
        int healthAmount = treeType switch
        {
            TreeType.Tree => 30,      // Cây nguyên
            TreeType.Log => 25,       // Khúc gỗ
            TreeType.LogHalf => 15,   // Khúc gỗ nửa
            TreeType.Stump => 20,     // Gốc cây
            _ => 30
        };

        healthSystem = new HealthSystem(healthAmount);
        resourceType = ResourceType.Tree;
    }

    // Hàm gọi khi cây bị phá hủy
    protected override void OnResourceDestroyed()
    {
        // Ngăn không cho gọi nhiều lần
        if (isBeingDestroyed)
        {
            Debug.LogWarning($"{gameObject.name}: OnResourceDestroyed called multiple times!");
            return;
        }

        isBeingDestroyed = true;
        Debug.Log($"{gameObject.name}: Tree destruction starting - Type: {treeType}");

        HandleTreeDestruction();
        DestroyResource(); // Xóa object khỏi scene
    }

    // Xử lý logic khi cây bị phá
    private void HandleTreeDestruction()
    {
        // Đánh dấu cây đã bị chặt trong dữ liệu spawner
        if (treeData != null)
            treeData.isCut = true;

        // Spawn các thành phần dựa trên loại cây
        switch (treeType)
        {
            case TreeType.Tree:
                SpawnTreeComponents(); // Cây nguyên => spawn log + gốc
                break;

            case TreeType.Log:
                SpawnLogHalves();      // Khúc gỗ => spawn log nửa
                break;

            case TreeType.LogHalf:
            case TreeType.Stump:
                SpawnSticks();         // Khúc gỗ nửa hoặc gốc => spawn stick
                break;
        }
    }

    // Spawn log và gốc cây khi chặt cây nguyên
    private void SpawnTreeComponents()
    {
        Debug.Log("Tree destroyed - spawning log and stump");

        // Spawn log
        if (treeLogPrefab != null)
        {
            Vector3 logPosition = transform.position + transform.up * 0.2f;
            Quaternion logRotation = Quaternion.Euler(
                Random.Range(-1.5f, 1.5f),
                Random.Range(0f, 360f),
                Random.Range(-1.5f, 1.5f)
            );

            Transform spawnedLog = Instantiate(treeLogPrefab, logPosition, logRotation);
            Debug.Log($"Spawned log: {spawnedLog.name}");
        }

        // Spawn gốc cây
        if (treeStumpPrefab != null)
        {
            Transform spawnedStump = Instantiate(treeStumpPrefab, transform.position, transform.rotation);
            Debug.Log($"Spawned stump: {spawnedStump.name}");
        }

        // Hiệu ứng phá cây
        if (destructionFxPrefab != null && particleSpawnPosition != null)
        {
            Instantiate(destructionFxPrefab, particleSpawnPosition.position, particleSpawnPosition.rotation);
        }
    }

    // Spawn các khúc gỗ nửa dựa trên size của cây
    private void SpawnLogHalves()
    {
        int halfLogCount = treeSize switch
        {
            TreeSize.Small => 2,
            TreeSize.Medium => 4,
            TreeSize.Large => 6
        };
        float halfLogOffset = 13.0f; // Khoảng cách giữa các khúc gỗ nửa
        for (int i = 0; i < halfLogCount; i++)
        {
            Vector3 offset = transform.up * halfLogOffset * i;
            Quaternion rotation = Quaternion.LookRotation(transform.forward, transform.up) * Quaternion.Euler(0, Random.Range(0f, 360f), 0);
            Instantiate(treeLogHalfPrefab, transform.position + offset, rotation);
        }
    }

    // Lấy chiều dài khúc gỗ từ collider hoặc dùng manual
    private float GetLogLength()
    {
        if (!useColliderBounds)
            return manualLogLength;

        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            Debug.LogWarning($"{gameObject.name}: No collider found, using manual length");
            return manualLogLength;
        }

        Vector3 size = col.bounds.size;
        return Mathf.Max(size.x, size.y, size.z); // Trả về chiều dài lớn nhất
    }

    // Lấy hướng dài nhất của khúc gỗ
    private Vector3 GetLogLengthDirection()
    {
        Collider col = GetComponent<Collider>();
        if (col == null)
            return transform.forward;

        Vector3 size = col.bounds.size;

        if (size.x >= size.y && size.x >= size.z)
            return transform.right;
        else if (size.y >= size.x && size.y >= size.z)
            return transform.up;
        else
            return transform.forward;
    }

    // Thêm lực vật lý cho khúc gỗ nửa khi spawn
    private void AddPhysicsToHalfLog(GameObject halfLog, Vector3 lengthDirection)
    {
        Rigidbody rb = halfLog.GetComponent<Rigidbody>();
        if (rb == null) return;

        // Lực scatter ngẫu nhiên
        Vector3 scatterForce = Vector3.Cross(lengthDirection, Vector3.up) * Random.Range(-2f, 2f);
        scatterForce += Vector3.up * Random.Range(0f, 1f);

        rb.AddForce(scatterForce, ForceMode.Impulse);
        rb.AddTorque(Random.insideUnitSphere * 1f, ForceMode.Impulse);
    }

    // Spawn stick nhỏ khi log half hoặc stump bị phá
    private void SpawnSticks()
    {
        int stickCount = Random.Range(minDropCount, maxDropCount + 1);
        Debug.Log($"Spawning {stickCount} sticks");
        SpawnDrops(stickPrefab, stickCount, transform.position);
    }

    // Kiểm tra xem các prefab đã được gán chưa
    protected override void ValidateComponents()
    {
        if (treeType == TreeType.Tree && treeLogPrefab == null)
            Debug.LogWarning($"{name}: treeLogPrefab not assigned!");

        if (treeType == TreeType.Log && treeLogHalfPrefab == null)
            Debug.LogWarning($"{name}: treeLogHalfPrefab not assigned!");

        if ((treeType == TreeType.LogHalf || treeType == TreeType.Stump) && stickPrefab == null)
            Debug.LogWarning($"{name}: stickPrefab not assigned!");
    }

    // Trả về loại resource
    public override ResourceType GetResourceType() => ResourceType.Tree;

    [ContextMenu("Test Damage 10")]
    private void TestDamage()
    {
        if (healthSystem == null)
        {
            InitializeHealth();
        }

        healthSystem.Damage(30);
        Debug.Log($"{name} took 10 damage. Current HP: {healthSystem.GetHealth()}");
    }

    [ContextMenu("Debug Log Info")]
    private void DebugLogInfo()
    {
        Debug.Log($"Tree Type: {treeType}, Size: {treeSize}");
        Debug.Log($"Log Length: {GetLogLength()}, Direction: {GetLogLengthDirection()}");

        Collider col = GetComponent<Collider>();
        if (col != null)
            Debug.Log($"Collider bounds: {col.bounds.size}");
    }
}
