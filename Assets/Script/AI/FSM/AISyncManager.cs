using System.Collections.Generic;
using UnityEngine;
using Mirror;

public class AISyncManager : NetworkBehaviour
{
    public static AISyncManager Instance;

    [Header("Thiết lập đồng bộ AI")]
    public float aiSyncInterval = 0.1f; // Thời gian giữa các lần sync dữ liệu AI

    [Header("Thiết lập cập nhật AI")]
    [Tooltip("Chạy AI logic mỗi frame (true) hoặc theo tick interval (false)")]
    public bool updateEveryFrame = true;

    [Tooltip("Nếu false, chỉ chạy AI trên server")]
    public bool allowClientAIUpdate = false;

    // Danh sách và dữ liệu quản lý AI
    private readonly List<AIEntity> aiEntities = new();             // Danh sách AI đang tồn tại trong scene
    private readonly Dictionary<string, AILoadData> savedAIData = new(); // Dữ liệu các AI đã despawn (có thể respawn lại)
    private readonly HashSet<string> sleepingAIs = new();           // Danh sách AI đang "ngủ"

    private float lastSync; // Dùng để đếm thời gian sync định kỳ

    // =========================================================
    // 🧠 KHỞI TẠO
    // =========================================================
    void Awake() => Instance = this;

    // =========================================================
    // 🔁 CẬP NHẬT - CENTRALIZED AI UPDATE
    // =========================================================
    void Update()
    {
        float time = Time.time;

        // ===== SERVER LOGIC =====
        if (isServer)
        {
            // 🔄 Cập nhật AI logic mỗi frame (nếu bật)
            if (updateEveryFrame)
            {
                foreach (var ai in aiEntities)
                {
                    if (ai != null && !ai.IsSleeping)
                    {
                        ai.TickAI();
                    }
                }
            }

            // 📡 Đồng bộ trạng thái AI định kỳ
            if (time - lastSync >= aiSyncInterval)
            {
                foreach (var ai in aiEntities)
                {
                    ai?.UpdateServerState(); // Gửi trạng thái mới về client
                }
                lastSync = time;
            }
        }
        // ===== CLIENT LOGIC =====
        else
        {
            // 🎯 Nội suy vị trí mượt mà cho tất cả AI
            foreach (var ai in aiEntities)
            {
                ai?.ClientInterpolate();
            }

            // 🔧 Option: Chạy AI trên client (nếu bật)
            if (allowClientAIUpdate && updateEveryFrame)
            {
                foreach (var ai in aiEntities)
                {
                    if (ai != null && ai.runAIOnClient && !ai.IsSleeping)
                    {
                        ai.TickAI();
                    }
                }
            }
        }
    }

    // =========================================================
    // 🎯 MANUAL TICK (nếu muốn gọi từ bên ngoài)
    // =========================================================
    [Server]
    public void TickAllAI()
    {
        foreach (var ai in aiEntities)
        {
            if (ai != null && !ai.IsSleeping)
            {
                ai.TickAI();
            }
        }
    }

    // =========================================================
    // 🔍 TÌM NGƯỜI CHƠI GẦN NHẤT
    // =========================================================
    private Transform FindClosestPlayer(Vector3 aiPos)
    {
        Transform closest = null;
        float minDist = float.MaxValue;

        foreach (var conn in NetworkServer.connections.Values)
        {
            if (conn == null || conn.identity == null) continue;

            var player = conn.identity.transform;
            float dist = (player.position - aiPos).sqrMagnitude;

            if (dist < minDist)
            {
                minDist = dist;
                closest = player;
            }
        }

        return closest;
    }

    // =========================================================
    // 🔗 QUẢN LÝ DANH SÁCH AI
    // =========================================================
    public void RegisterAI(AIEntity ai)
    {
        if (ai == null || ai.controller == null) return;

        if (!aiEntities.Contains(ai))
        {
            aiEntities.Add(ai);
            //Debug.Log($"[AISync] ✅ Đăng ký AI: {ai.controller.name} (id={ai.controller.uniqueId})");
        }
    }

    public void UnregisterAI(AIEntity ai)
    {
        aiEntities.Remove(ai);

        if (ai != null && ai.controller != null)
        {
            sleepingAIs.Remove(ai.controller.uniqueId);
            //Debug.Log($"[AISync] ❎ Gỡ đăng ký AI: {ai.controller.name} (id={ai.controller.uniqueId})");
        }
    }

    // =========================================================
    // 📊 DEBUG INFO
    // =========================================================
    public int GetActiveAICount() => aiEntities.Count;
    public int GetSleepingAICount() => sleepingAIs.Count;
}