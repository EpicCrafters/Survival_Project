using System.Collections.Generic;
using UnityEngine;
using Mirror;
using Mirror.Examples.Common.Controllers.Player;

public class AISpawnManager : NetworkBehaviour
{
    public static AISpawnManager Instance; // Singleton để truy cập toàn cục

    // ========================= CẤU HÌNH CHUNG =========================
    [Header("Cấu hình Tick AI")]
    [Tooltip("Thời gian giữa các lần tick AI (chạy logic hành vi)")]
    public float tickRate = 0.25f; // Tần suất tick logic AI (cập nhật hành vi)

    [Header("Khoảng cách các tầng hoạt động của AI")]
    [Tooltip("Vùng an toàn quanh player - AI sẽ KHÔNG spawn ở đây")]
    public float safeZoneRadius = 24f; // Bán kính an toàn quanh player (AI không spawn trong đây)

    [Tooltip("Vùng AI có thể spawn và hoạt động (ngoài safe zone)")]
    public float sleepRadius = 34f; // Bán kính AI hoạt động (ngoài vùng an toàn)

    [Tooltip("Khoảng cách đệm, AI sẽ despawn nếu vượt quá")]
    public float despawnDistance = 10f; // Khoảng cách thêm để despawn (ngoài sleepRadius)

    [Header("Cấu hình sinh AI")]
    [Tooltip("Danh sách các loại AI có thể spawn")]
    public List<AISpawnData> aiSpawnList = new List<AISpawnData>(); // Danh sách các prefab AI

    [Tooltip("Khoảng cách tối thiểu giữa các AI spawn ra")]
    public float minAISeparation = 5f; // Đảm bảo AI không spawn quá sát nhau

    [Tooltip("Layer dùng để xác định mặt đất (cho raycast)")]
    public LayerMask groundLayer; // Layer để raycast kiểm tra mặt đất

    [Header("Tùy chọn spawn")]
    [Tooltip("Số lần thử tìm vị trí spawn hợp lệ")]
    public int randomSpawnAttempts = 15; // Số lần thử tìm chỗ spawn hợp lệ

    [Tooltip("Độ trễ trước khi bắt đầu spawn lần đầu")]
    public float initialSpawnDelay = 2f; // Delay ban đầu trước khi spawn

    [Header("Spawn Timer (Chính)")]
    [Tooltip("Khoảng thời gian giữa mỗi lần spawn một AI (giây)")]
    public float spawnInterval = 3f; // Thời gian giữa 2 lần spawn

    [Tooltip("Số AI spawn trong đợt đầu tiên (0 = không spawn ngay)")]
    public int initialSpawnCount = 0; // Số lượng AI spawn ngay lúc bắt đầu

    [Tooltip("Tổng số AI tối đa trong toàn bộ scene (0 = không giới hạn)")]
    public int maxTotalAIsInScene = 20; // Giới hạn tổng AI hoạt động

    [Header("Object Pooling")]
    [Tooltip("Bật/tắt pooling (tái sử dụng AI thay vì Instantiate/Destroy)")]
    public bool usePooling = true; // Có dùng object pool hay không

    [Tooltip("AI ra ngoài vòng tròn sẽ được pool lại để spawn")]
    public bool poolAIsOutsideRing = true; // AI ra xa quá thì thu hồi vào pool

    // ========================= BIẾN NỘI BỘ =========================
    private float tickTimer; // Bộ đếm tick AI
    private float nextSpawnTime; // Thời gian spawn kế tiếp
    private int totalSpawnedThisSession = 0; // Tổng số AI đã spawn lần này

    // Các bộ dữ liệu quản lý AI
    private readonly Dictionary<string, int> activeAICount = new(); // Số lượng AI đang hoạt động theo loại
    private readonly Dictionary<string, Queue<GameObject>> aiPools = new(); // Pool object theo loại AI
    private readonly List<HFSMController> trackedAIs = new(); // Danh sách AI đang hoạt động (được theo dõi)
    private readonly List<Transform> cachedPlayers = new(); // Danh sách player hiện tại trên server

    // ========================= UNITY LIFECYCLE =========================
    void Awake()
    {
        // Đảm bảo chỉ có 1 Instance (Singleton)
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        // Chỉ chạy logic spawn AI trên server
        if (!isServer) return;

        // Khởi tạo các pool cho từng loại AI
        foreach (var spawnData in aiSpawnList)
        {
            // Nếu chưa có poolKey thì tự đặt bằng tên prefab
            if (string.IsNullOrEmpty(spawnData.poolKey))
                spawnData.poolKey = spawnData.prefab.name;

            activeAICount[spawnData.poolKey] = 0;

            // Nếu bật pooling thì tạo sẵn pool
            if (usePooling)
            {
                aiPools[spawnData.poolKey] = new Queue<GameObject>();
                InitializePool(spawnData);
            }
        }

        // Đặt thời gian spawn đầu tiên
        nextSpawnTime = Time.time + initialSpawnDelay;

        // Spawn số lượng AI ban đầu nếu có cài đặt
        if (initialSpawnCount > 0)
        {
            for (int i = 0; i < initialSpawnCount; i++)
            {
                SpawnSingleAI();
            }
        }

        Debug.Log($"[AISpawnManager] ⏱️ Timer spawn: {spawnInterval}s | Max AI: {maxTotalAIsInScene} | Pool ngoài vòng: {poolAIsOutsideRing}");
    }

    void Update()
    {
        // Chỉ server quản lý tick & spawn
        if (!isServer) return;

        // Cập nhật danh sách player hiện có
        UpdatePlayerCache();

        // ============ QUẢN LÝ TIMER SPAWN ============
        if (Time.time >= nextSpawnTime)
        {
            nextSpawnTime = Time.time + spawnInterval;
            SpawnSingleAI(); // Spawn một con AI
        }

        // ============ QUẢN LÝ KHOẢNG CÁCH ============
        ManageAIDistances();

        // ============ TICK LOGIC AI ============
        tickTimer += Time.deltaTime;
        if (tickTimer >= tickRate)
        {
            tickTimer = 0f;
            TickAllActiveAIs(); // Gọi logic cho toàn bộ AI đang hoạt động
        }
    }

    // ========================= OBJECT POOLING =========================
    private void InitializePool(AISpawnData spawnData)
    {
        // Tạo sẵn số lượng AI trong pool
        for (int i = 0; i < spawnData.poolSize; i++)
        {
            GameObject obj = Instantiate(spawnData.prefab);
            obj.SetActive(false); // Ẩn đi

            var controller = obj.GetComponent<HFSMController>();
            if (controller != null)
            {
                controller.poolKey = spawnData.poolKey;
                controller.pooled = true; // Đánh dấu là đang ở pool
            }

            aiPools[spawnData.poolKey].Enqueue(obj); // Thêm vào hàng chờ pool
        }
    }

    private GameObject GetFromPool(string poolKey)
    {
        // Lấy AI ra từ pool nếu còn
        if (!usePooling || !aiPools.ContainsKey(poolKey) || aiPools[poolKey].Count == 0)
            return null;

        GameObject obj = aiPools[poolKey].Dequeue();
        obj.SetActive(true);
        return obj;
    }

    private void ReturnToPool(HFSMController ai)
    {
        // Đưa AI trở lại pool
        if (!usePooling || ai == null) return;

        string key = ai.poolKey;
        GameObject obj = ai.gameObject;

        obj.SetActive(false);
        ai.pooled = true;

        if (aiPools.ContainsKey(key))
        {
            aiPools[key].Enqueue(obj);
            Debug.Log($"[AISpawnManager] ♻️ Đã trả {key} về pool (Pool size: {aiPools[key].Count})");
        }
    }

    // ========================= PLAYER CACHE =========================
    private void UpdatePlayerCache()
    {
        // Cập nhật danh sách player từ Mirror server
        cachedPlayers.Clear();
        foreach (var conn in NetworkServer.connections.Values)
            if (conn?.identity != null)
                cachedPlayers.Add(conn.identity.transform);
    }

    // ========================= TICK AI =========================
    private void TickAllActiveAIs()
    {
        // Gọi hàm TickAI() cho mỗi AI hoạt động
        foreach (var controller in trackedAIs)
        {
            if (controller == null || controller.isSleeping || controller.syncedIsDead)
                continue;

            var entity = controller.GetComponent<AIEntity>();
            if (entity != null && !entity.IsSleeping)
                entity.TickAI(); // Gọi update logic hành vi
        }
    }

    // ========================= LOGIC SINH AI =========================
    private void SpawnSingleAI()
    {
        if (cachedPlayers.Count == 0)
        {
            Debug.LogWarning("[AISpawnManager] ⚠️ Không có player -> bỏ qua spawn");
            return;
        }

        // Kiểm tra số lượng AI tổng
        int totalActiveAIs = GetTotalActiveAIs();
        if (maxTotalAIsInScene > 0 && totalActiveAIs >= maxTotalAIsInScene)
            return;

        // Tìm các loại AI chưa đạt giới hạn maxActive
        List<AISpawnData> availableSpawns = new List<AISpawnData>();
        foreach (var spawnData in aiSpawnList)
        {
            int currentCount = GetActiveAICountByType(spawnData.poolKey);
            if (currentCount < spawnData.maxActive)
                availableSpawns.Add(spawnData);
        }

        if (availableSpawns.Count == 0) return;

        // Chọn ngẫu nhiên 1 loại AI
        AISpawnData selectedSpawn = availableSpawns[Random.Range(0, availableSpawns.Count)];

        // Tìm vị trí spawn hợp lệ
        Vector3 spawnPos = FindValidSpawnPosition(selectedSpawn);
        if (spawnPos == Vector3.zero)
        {
            Debug.LogWarning($"[AISpawnManager] ⚠️ Không tìm được vị trí spawn cho {selectedSpawn.poolKey}");
            return;
        }

        // Spawn AI thật
        SpawnAI(selectedSpawn, spawnPos);
        totalSpawnedThisSession++;
    }

    private Vector3 FindValidSpawnPosition(AISpawnData spawnData)
    {
        // Thử nhiều lần tìm vị trí spawn hợp lệ
        for (int attempt = 0; attempt < randomSpawnAttempts; attempt++)
        {
            Transform randomPlayer = cachedPlayers[Random.Range(0, cachedPlayers.Count)];

            // Sinh ngẫu nhiên vị trí quanh player
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float distance = Random.Range(safeZoneRadius, sleepRadius);
            Vector3 offset = new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);
            Vector3 testPos = randomPlayer.position + offset;

            // Raycast xuống mặt đất
            if (!Physics.Raycast(testPos + Vector3.up * 50f, Vector3.down, out RaycastHit hit, 100f, groundLayer))
                continue;

            Vector3 groundPos = hit.point;

            // Kiểm tra vị trí có an toàn không (xa player)
            if (!IsPositionSafeFromAllPlayers(groundPos))
                continue;

            // Kiểm tra không quá sát AI khác
            if (!IsPositionSeparatedFromAIs(groundPos))
                continue;

            return groundPos; // Hợp lệ
        }

        return Vector3.zero;
    }

    private bool IsPositionSafeFromAllPlayers(Vector3 position)
    {
        // Đảm bảo vị trí spawn không quá gần bất kỳ player nào
        foreach (var player in cachedPlayers)
        {
            if (Vector3.Distance(position, player.position) < safeZoneRadius)
                return false;
        }
        return true;
    }

    private bool IsPositionSeparatedFromAIs(Vector3 position)
    {
        // Đảm bảo AI spawn cách xa AI khác
        foreach (var ai in trackedAIs)
        {
            if (ai == null || ai.syncedIsDead) continue;
            if (Vector3.Distance(position, ai.transform.position) < minAISeparation)
                return false;
        }
        return true;
    }

    // ========================= SPAWN AI =========================
    [Server]
    private void SpawnAI(AISpawnData spawnData, Vector3 position)
    {
        GameObject obj;

        // Lấy từ pool hoặc tạo mới
        if (usePooling)
        {
            obj = GetFromPool(spawnData.poolKey);
            if (obj == null)
            {
                obj = Instantiate(spawnData.prefab);
                Debug.Log($"[AISpawnManager] 🆕 Pool trống, tạo mới {spawnData.poolKey}");
            }
        }
        else obj = Instantiate(spawnData.prefab);

        // Đặt vị trí và xoay ngẫu nhiên
        obj.transform.position = position;
        obj.transform.rotation = Quaternion.Euler(0, Random.Range(0, 360f), 0);

        if (!obj.activeSelf)
            obj.SetActive(true);

        // Spawn qua mạng Mirror
        var netIdentity = obj.GetComponent<NetworkIdentity>();
        if (netIdentity != null)
            NetworkServer.Spawn(obj);
        else
            Debug.LogError($"[AISpawnManager] ❌ {spawnData.poolKey} thiếu NetworkIdentity!");

        // Gắn thông tin vào controller
        var controller = obj.GetComponent<HFSMController>();
        if (controller != null)
        {
            controller.poolKey = spawnData.poolKey;
            controller.isSleeping = false;
            controller.pooled = false;
            controller.OnRespawn(); // Reset trạng thái

            if (!trackedAIs.Contains(controller))
            {
                trackedAIs.Add(controller);
                activeAICount[spawnData.poolKey]++;
            }
        }

        // Đăng ký AI với AISyncManager (đồng bộ)
        var entity = obj.GetComponent<AIEntity>();
        if (entity != null && AISyncManager.Instance != null)
            AISyncManager.Instance.RegisterAI(entity);
    }

    // ========================= QUẢN LÝ KHOẢNG CÁCH & POOL =========================
    private int GetTotalActiveAIs()
    {
        // Tính tổng số AI đang hoạt động
        trackedAIs.RemoveAll(ai => ai == null);

        int total = 0;
        foreach (var ai in trackedAIs)
            if (ai != null && !ai.syncedIsDead && !ai.pooled)
                total++;

        return total;
    }

    private int GetActiveAICountByType(string poolKey)
    {
        // Đếm số lượng AI hoạt động của 1 loại cụ thể
        int count = 0;
        foreach (var ai in trackedAIs)
            if (ai != null && !ai.syncedIsDead && !ai.pooled && ai.poolKey == poolKey)
                count++;

        return count;
    }

    private void ManageAIDistances()
    {
        // Quản lý khoảng cách giữa AI và người chơi
        trackedAIs.RemoveAll(ai => ai == null);
        if (cachedPlayers.Count == 0) return;

        List<HFSMController> toPool = new();

        foreach (var ai in trackedAIs)
        {
            if (ai == null || ai.syncedIsDead || ai.pooled) continue;

            float minDist = GetMinDistanceToPlayers(ai.transform.position);
            var entity = ai.GetComponent<AIEntity>();

            if (entity == null) continue; // Safety check

            // Nếu quá xa => thu hồi về pool
            if (poolAIsOutsideRing && minDist > sleepRadius + despawnDistance)
            {
                toPool.Add(ai);
            }
            // Nếu hơi xa => cho AI "ngủ" (không chạy logic)
            else if (minDist > sleepRadius)
            {
                if (!entity.IsSleeping)
                {
                    entity.Sleep();
                    Debug.Log($"[AISpawnManager] 💤 {ai.name} đi vào chế độ ngủ (khoảng cách: {minDist:F1}m)");
                }
            }
            // Nếu gần => đánh thức AI
            else
            {
                if (entity.IsSleeping)
                {
                    entity.WakeUp();
                    Debug.Log($"[AISpawnManager] ⏰ {ai.name} đã thức dậy (khoảng cách: {minDist:F1}m)");
                }
            }
        }

        // Pool các AI ra ngoài vùng hoạt động
        foreach (var ai in toPool)
        {
            Debug.Log($"[AISpawnManager] ♻️ {ai.name} quá xa, đưa về pool (khoảng cách: {GetMinDistanceToPlayers(ai.transform.position):F1}m)");
            PoolAI(ai);
        }
    }

    private float GetMinDistanceToPlayers(Vector3 position)
    {
        // Tìm khoảng cách nhỏ nhất đến bất kỳ player nào
        float minDist = float.MaxValue;
        foreach (var player in cachedPlayers)
        {
            float dist = Vector3.Distance(position, player.position);
            if (dist < minDist)
                minDist = dist;
        }
        return minDist;
    }

    [Server]
    private void PoolAI(HFSMController ai)
    {
        // Thu hồi AI về pool (nếu ra ngoài vòng)
        if (ai == null) return;

        string key = ai.poolKey;
        trackedAIs.Remove(ai);

        if (activeAICount.ContainsKey(key))
            activeAICount[key] = Mathf.Max(0, activeAICount[key] - 1);

        var entity = ai.GetComponent<AIEntity>();
        if (entity != null && AISyncManager.Instance != null)
            AISyncManager.Instance.UnregisterAI(entity);

        if (usePooling)
        {
            NetworkServer.UnSpawn(ai.gameObject); // Hủy spawn trên client
            ReturnToPool(ai); // Trả về pool
            Debug.Log($"[AISpawnManager] ♻️ {key} ra ngoài vòng -> Pool lại (Active: {GetTotalActiveAIs()})");
        }
        else
        {
            NetworkServer.Destroy(ai.gameObject);
            Debug.Log($"[AISpawnManager] 🗑️ Destroy {key}");
        }
    }

    // ========================= DEBUG DRAW GIZMOS =========================
    private void OnDrawGizmosSelected()
    {
        // Vẽ các vòng tròn bán kính trong Scene View để debug
        if (cachedPlayers.Count == 0)
        {
            var players = FindObjectsOfType<Player>();
            if (players.Length == 0) return;

            Vector3 playerPos = players[0].transform.position;
            DrawDebugCircles(playerPos);
        }
        else
        {
            foreach (var player in cachedPlayers)
                DrawDebugCircles(player.position);
        }
    }

    private void DrawDebugCircles(Vector3 center)
    {
        Gizmos.color = Color.yellow;
        DrawCircle(center, safeZoneRadius); // Vùng an toàn

        Gizmos.color = Color.green;
        DrawCircle(center, sleepRadius); // Vùng hoạt động

        Gizmos.color = Color.red;
        DrawCircle(center, sleepRadius + despawnDistance); // Vùng despawn
    }

    private void DrawCircle(Vector3 center, float radius)
    {
        int seg = 64;
        Vector3 last = center + new Vector3(radius, 0, 0);
        for (int i = 1; i <= seg; i++)
        {
            float angle = (float)i / seg * 2 * Mathf.PI;
            Vector3 next = center + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
            Gizmos.DrawLine(last, next);
            last = next;
        }
    }
}

// ========================= CLASS DỮ LIỆU SPAWN =========================
[System.Serializable]
public class AISpawnData
{
    [Tooltip("Tên định danh pool (để trống = dùng tên prefab)")]
    public string poolKey;

    [Tooltip("Prefab AI tương ứng")]
    public GameObject prefab;

    [Tooltip("Số lượng AI giữ sẵn trong pool")]
    public int poolSize = 10;

    [Tooltip("Số lượng AI tối đa hoạt động cùng lúc")]
    public int maxActive = 5;
}
