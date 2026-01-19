using System.Collections.Generic;
using UnityEngine;
using Mirror;
using Mirror.Examples.Common.Controllers.Player;

public class AISpawnManager : NetworkBehaviour
{
    public static AISpawnManager Instance;

    [Header("Cấu hình Tick AI")]
    [Tooltip("Thời gian giữa các lần tick AI (chạy logic hành vi)")]
    public float tickRate = 0.25f;

    [Header("Khoảng cách các tầng hoạt động của AI")]
    [Tooltip("Vùng an toàn quanh player - AI sẽ KHÔNG spawn ở đây")]
    public float safeZoneRadius = 24f;

    [Tooltip("Vùng AI có thể spawn và hoạt động (ngoài safe zone)")]
    public float sleepRadius = 34f;

    [Tooltip("Khoảng cách đệm, AI sẽ despawn nếu vượt quá")]
    public float despawnDistance = 10f;

    [Header("Cấu hình sinh AI")]
    [Tooltip("Danh sách các loại AI có thể spawn")]
    public List<AISpawnData> aiSpawnList = new List<AISpawnData>();

    [Tooltip("Khoảng cách tối thiểu giữa các AI spawn ra")]
    public float minAISeparation = 5f;

    [Tooltip("Layer dùng để xác định mặt đất (cho raycast)")]
    public LayerMask groundLayer;

    [Header("Tùy chọn spawn")]
    [Tooltip("Số lần thử tìm vị trí spawn hợp lệ")]
    public int randomSpawnAttempts = 15;

    [Tooltip("Độ trễ trước khi bắt đầu spawn lần đầu")]
    public float initialSpawnDelay = 2f;

    [Header("Spawn Timer (Chính)")]
    [Tooltip("Khoảng thời gian giữa mỗi lần spawn một AI (giây)")]
    public float spawnInterval = 3f;

    [Tooltip("Số AI spawn trong đợt đầu tiên (0 = không spawn ngay)")]
    public int initialSpawnCount = 0;

    [Tooltip("Tổng số AI tối đa trong toàn bộ scene (0 = không giới hạn)")]
    public int maxTotalAIsInScene = 20;

    [Header("Object Pooling - Mirror Compatible")]
    [Tooltip("Bật/tắt pooling (tái sử dụng AI thay vì Instantiate/Destroy)")]
    public bool usePooling = true;

    [Tooltip("AI ra ngoài vòng tròn sẽ được pool lại để spawn")]
    public bool poolAIsOutsideRing = true;

    // ========================= BIẾN NỘI BỘ =========================
    private float tickTimer;
    private float nextSpawnTime;
    private int totalSpawnedThisSession = 0;

    private readonly Dictionary<string, int> activeAICount = new();
    private readonly Dictionary<string, Queue<GameObject>> aiPools = new();
    private readonly List<HFSMController> trackedAIs = new();
    private readonly List<Transform> cachedPlayers = new();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        if (!isServer) return;

        // Khởi tạo các pool
        foreach (var spawnData in aiSpawnList)
        {
            if (string.IsNullOrEmpty(spawnData.poolKey))
                spawnData.poolKey = spawnData.prefab.name;

            activeAICount[spawnData.poolKey] = 0;

            if (usePooling)
            {
                aiPools[spawnData.poolKey] = new Queue<GameObject>();
                // DON'T pre-instantiate - Mirror doesn't like it
               // Debug.Log($"[AISpawnManager] ✅ Pool '{spawnData.poolKey}' initialized (on-demand creation)");
            }
        }

        nextSpawnTime = Time.time + initialSpawnDelay;

        if (initialSpawnCount > 0)
        {
            for (int i = 0; i < initialSpawnCount; i++)
            {
                SpawnSingleAI();
            }
        }

       // Debug.Log($"[AISpawnManager] ⏱️ Timer: {spawnInterval}s | Max AI: {maxTotalAIsInScene} | Pooling: {usePooling}");
    }

    void Update()
    {
        if (!isServer) return;

        UpdatePlayerCache();

        // Timer spawn
        if (Time.time >= nextSpawnTime)
        {
            nextSpawnTime = Time.time + spawnInterval;
            SpawnSingleAI();
        }

        ManageAIDistances();

        // Tick AI
        tickTimer += Time.deltaTime;
        if (tickTimer >= tickRate)
        {
            tickTimer = 0f;
            TickAllActiveAIs();
        }
    }

    // ========================= MIRROR-COMPATIBLE POOLING =========================

    /// <summary>
    /// Lấy AI từ pool (nếu có). Trả về null nếu pool trống.
    /// </summary>
    private GameObject GetFromPool(string poolKey)
    {
        if (!usePooling || !aiPools.ContainsKey(poolKey))
            return null;

        var pool = aiPools[poolKey];

        // Dọn dẹp các object null trong pool
        while (pool.Count > 0)
        {
            GameObject obj = pool.Dequeue();
            if (obj != null)
            {
                return obj; // Tìm thấy object hợp lệ
            }
        }

        return null; // Pool trống hoặc toàn null
    }

    /// <summary>
    /// Trả AI về pool - QUAN TRỌNG: phải UnSpawn trước khi SetActive(false)
    /// </summary>
    private void ReturnToPool(HFSMController ai)
    {
        if (!usePooling || ai == null) return;

        string key = ai.poolKey;
        GameObject obj = ai.gameObject;

        // BƯỚC 1: UnSpawn từ network TRƯỚC
        var netIdentity = obj.GetComponent<NetworkIdentity>();
        if (netIdentity != null && NetworkServer.active)
        {
            NetworkServer.UnSpawn(obj);
        }

        // BƯỚC 2: Reset state
        ai.pooled = true;
        ai.isSleeping = true;

        // BƯỚC 3: Deactivate SAU khi đã UnSpawn
        obj.SetActive(false);

        // BƯỚC 4: Thêm vào pool
        if (aiPools.ContainsKey(key))
        {
            aiPools[key].Enqueue(obj);
            //Debug.Log($"[AISpawnManager] ♻️ {key} returned to pool (Pool: {aiPools[key].Count})");
        }
    }

    // ========================= PLAYER CACHE =========================

    private void UpdatePlayerCache()
    {
        cachedPlayers.Clear();
        foreach (var conn in NetworkServer.connections.Values)
            if (conn?.identity != null)
                cachedPlayers.Add(conn.identity.transform);
    }

    // ========================= TICK AI =========================

    private void TickAllActiveAIs()
    {
        foreach (var controller in trackedAIs)
        {
            if (controller == null || controller.isSleeping || controller.syncedIsDead)
                continue;

            var entity = controller.GetComponent<AIEntity>();
            if (entity != null && !entity.IsSleeping)
                entity.TickAI();
        }
    }

    // ========================= SPAWN LOGIC =========================

    private void SpawnSingleAI()
    {
        if (cachedPlayers.Count == 0)
        {
           // Debug.LogWarning("[AISpawnManager] ⚠️ No players - skipping spawn");
            return;
        }

        int totalActiveAIs = GetTotalActiveAIs();
        if (maxTotalAIsInScene > 0 && totalActiveAIs >= maxTotalAIsInScene)
            return;

        List<AISpawnData> availableSpawns = new List<AISpawnData>();
        foreach (var spawnData in aiSpawnList)
        {
            int currentCount = GetActiveAICountByType(spawnData.poolKey);
            if (currentCount < spawnData.maxActive)
                availableSpawns.Add(spawnData);
        }

        if (availableSpawns.Count == 0) return;

        AISpawnData selectedSpawn = availableSpawns[Random.Range(0, availableSpawns.Count)];

        Vector3 spawnPos = FindValidSpawnPosition(selectedSpawn);
        if (spawnPos == Vector3.zero)
        {
           // Debug.LogWarning($"[AISpawnManager] ⚠️ No valid spawn position for {selectedSpawn.poolKey}");
            return;
        }

        SpawnAI(selectedSpawn, spawnPos);
        totalSpawnedThisSession++;
    }

    private Vector3 FindValidSpawnPosition(AISpawnData spawnData)
    {
        for (int attempt = 0; attempt < randomSpawnAttempts; attempt++)
        {
            Transform randomPlayer = cachedPlayers[Random.Range(0, cachedPlayers.Count)];

            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float distance = Random.Range(safeZoneRadius, sleepRadius);
            Vector3 offset = new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);
            Vector3 testPos = randomPlayer.position + offset;

            if (!Physics.Raycast(testPos + Vector3.up * 50f, Vector3.down, out RaycastHit hit, 100f, groundLayer))
                continue;

            Vector3 groundPos = hit.point;

            if (!IsPositionSafeFromAllPlayers(groundPos))
                continue;

            if (!IsPositionSeparatedFromAIs(groundPos))
                continue;

            return groundPos;
        }

        return Vector3.zero;
    }

    private bool IsPositionSafeFromAllPlayers(Vector3 position)
    {
        foreach (var player in cachedPlayers)
        {
            if (Vector3.Distance(position, player.position) < safeZoneRadius)
                return false;
        }
        return true;
    }

    private bool IsPositionSeparatedFromAIs(Vector3 position)
    {
        foreach (var ai in trackedAIs)
        {
            if (ai == null || ai.syncedIsDead) continue;
            if (Vector3.Distance(position, ai.transform.position) < minAISeparation)
                return false;
        }
        return true;
    }

    // ========================= SPAWN AI - MIRROR COMPATIBLE =========================

    [Server]
    private void SpawnAI(AISpawnData spawnData, Vector3 position)
    {
        GameObject obj = null;
        bool isFromPool = false;

        // Thử lấy từ pool trước
        if (usePooling)
        {
            obj = GetFromPool(spawnData.poolKey);
            if (obj != null)
            {
                isFromPool = true;
                //Debug.Log($"[AISpawnManager] ♻️ Reusing {spawnData.poolKey} from pool");
            }
        }

        // Tạo mới nếu pool trống
        if (obj == null)
        {
            obj = Instantiate(spawnData.prefab);
            //Debug.Log($"[AISpawnManager] 🆕 Creating new {spawnData.poolKey}");
        }

        // Đặt vị trí và rotation TRƯỚC KHI ACTIVE
        obj.transform.position = position;
        obj.transform.rotation = Quaternion.Euler(0, Random.Range(0, 360f), 0);

        // QUAN TRỌNG: Disable NavMeshAgent trước khi active object
        var navAgent = obj.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (navAgent != null)
        {
            navAgent.enabled = false;
        }

        // Active object
        if (!obj.activeSelf)
            obj.SetActive(true);

        // Kiểm tra NetworkIdentity
        var netIdentity = obj.GetComponent<NetworkIdentity>();
        if (netIdentity == null)
        {
            //Debug.LogError($"[AISpawnManager] ❌ {spawnData.poolKey} missing NetworkIdentity!");
            Destroy(obj);
            return;
        }

        // Spawn trên network
        try
        {
            NetworkServer.Spawn(obj);
            //Debug.Log($"[AISpawnManager] 📡 Spawned {spawnData.poolKey} on network (from pool: {isFromPool})");
        }
        catch (System.Exception e)
        {
            //Debug.LogError($"[AISpawnManager] ❌ Failed to spawn {spawnData.poolKey}: {e.Message}");
            Destroy(obj);
            return;
        }

        // Setup controller TRƯỚC KHI enable NavMeshAgent
        var controller = obj.GetComponent<HFSMController>();
        if (controller != null)
        {
            controller.poolKey = spawnData.poolKey;
            controller.isSleeping = false;
            controller.pooled = false;

            if (!trackedAIs.Contains(controller))
            {
                trackedAIs.Add(controller);
                activeAICount[spawnData.poolKey]++;
            }
        }

        // Đăng ký với AISyncManager
        var entity = obj.GetComponent<AIEntity>();
        if (entity != null && AISyncManager.Instance != null)
            AISyncManager.Instance.RegisterAI(entity);

        // QUAN TRỌNG: Enable NavMeshAgent SAU KHI đã setup xong
        // Đợi 1 frame để NavMesh có thể place agent
        if (navAgent != null)
        {
            StartCoroutine(EnableNavMeshAgentDelayed(navAgent, controller));
        }
    }

    /// <summary>
    /// Enable NavMeshAgent sau khi object đã được place đúng vị trí
    /// </summary>
    private System.Collections.IEnumerator EnableNavMeshAgentDelayed(UnityEngine.AI.NavMeshAgent agent, HFSMController controller)
    {
        // Đợi 1 frame để object settle
        yield return null;

        if (agent != null && agent.gameObject.activeSelf)
        {
            // Try to place on NavMesh first
            if (UnityEngine.AI.NavMesh.SamplePosition(agent.transform.position, out UnityEngine.AI.NavMeshHit hit, 10f, UnityEngine.AI.NavMesh.AllAreas))
            {
                agent.transform.position = hit.position;
            }

            agent.enabled = true;

            // Đợi thêm 1 frame để NavMeshAgent được place
            yield return null;

            // Verify agent is on NavMesh before calling OnRespawn
            if (agent.isOnNavMesh)
            {
                // Bây giờ mới gọi OnRespawn (sẽ reset NavMeshAgent)
                if (controller != null)
                {
                    controller.OnRespawn();
                }
            }
            else
            {
                //Debug.LogWarning($"[AISpawnManager] ⚠️ {controller.name} NavMeshAgent not on NavMesh after delay!");
            }
        }
    }

    // ========================= DISTANCE MANAGEMENT =========================

    private int GetTotalActiveAIs()
    {
        trackedAIs.RemoveAll(ai => ai == null);

        int total = 0;
        foreach (var ai in trackedAIs)
            if (ai != null && !ai.syncedIsDead && !ai.pooled)
                total++;

        return total;
    }

    private int GetActiveAICountByType(string poolKey)
    {
        int count = 0;
        foreach (var ai in trackedAIs)
            if (ai != null && !ai.syncedIsDead && !ai.pooled && ai.poolKey == poolKey)
                count++;

        return count;
    }

    private void ManageAIDistances()
    {
        trackedAIs.RemoveAll(ai => ai == null);
        if (cachedPlayers.Count == 0) return;

        List<HFSMController> toPool = new();

        foreach (var ai in trackedAIs)
        {
            if (ai == null || ai.syncedIsDead || ai.pooled) continue;

            float minDist = GetMinDistanceToPlayers(ai.transform.position);
            var entity = ai.GetComponent<AIEntity>();

            if (entity == null) continue;

            if (poolAIsOutsideRing && minDist > sleepRadius + despawnDistance)
            {
                toPool.Add(ai);
            }
            else if (minDist > sleepRadius)
            {
                if (!entity.IsSleeping)
                {
                    entity.Sleep();
                    //Debug.Log($"[AISpawnManager] 💤 {ai.name} sleeping (dist: {minDist:F1}m)");
                }
            }
            else
            {
                if (entity.IsSleeping)
                {
                    entity.WakeUp();
                    //Debug.Log($"[AISpawnManager] ⏰ {ai.name} woke up (dist: {minDist:F1}m)");
                }
            }
        }

        foreach (var ai in toPool)
        {
            //Debug.Log($"[AISpawnManager] ♻️ {ai.name} too far, pooling (dist: {GetMinDistanceToPlayers(ai.transform.position):F1}m)");
            PoolAI(ai);
        }
    }

    private float GetMinDistanceToPlayers(Vector3 position)
    {
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
            ReturnToPool(ai); // Sử dụng method mới với UnSpawn
            //Debug.Log($"[AISpawnManager] ♻️ {key} pooled (Active: {GetTotalActiveAIs()})");
        }
        else
        {
            NetworkServer.Destroy(ai.gameObject);
            //Debug.Log($"[AISpawnManager] 🗑️ Destroyed {key}");
        }
    }

    // ========================= DEBUG GIZMOS =========================

    private void OnDrawGizmosSelected()
    {
        if (cachedPlayers.Count == 0)
        {
            var players = FindObjectsOfType<PlayerMovement>();
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
        DrawCircle(center, safeZoneRadius);

        Gizmos.color = Color.green;
        DrawCircle(center, sleepRadius);

        Gizmos.color = Color.red;
        DrawCircle(center, sleepRadius + despawnDistance);
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

[System.Serializable]
public class AISpawnData
{
    [Tooltip("Tên định danh pool (để trống = dùng tên prefab)")]
    public string poolKey;

    [Tooltip("Prefab AI tương ứng")]
    public GameObject prefab;

    [Tooltip("Số lượng AI giữ sẵn trong pool (không dùng với Mirror - để 0)")]
    public int poolSize = 0;

    [Tooltip("Số lượng AI tối đa hoạt động cùng lúc")]
    public int maxActive = 5;
}