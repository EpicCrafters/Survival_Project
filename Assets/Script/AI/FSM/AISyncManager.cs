using UnityEngine;
using Mirror;
using System.Collections.Generic;

public class AISyncManager : NetworkBehaviour
{
    public static AISyncManager Instance;

    [Header("Sync Settings")]
    [SerializeField] private float aiSyncInterval = 0.1f;   // ✅ Thời gian giữa mỗi lần đồng bộ trạng thái AI từ server
    [SerializeField] private float aiLerpRate = 10f;        // ✅ Tốc độ nội suy (interpolation) trên client

    private float aiLastSyncTime;  // ✅ Lưu lại thời điểm đồng bộ cuối cùng (chỉ dùng cho server)

    // Danh sách tất cả AI đã đăng ký để đồng bộ
    private readonly List<AIEntity> aiEntities = new List<AIEntity>();

    void Awake()
    {
        // Singleton để dễ dàng truy cập AISyncManager từ chỗ khác
        Instance = this;
    }

    void Update()
    {
        if (isServer)
        {
            // Chỉ server mới chịu trách nhiệm gửi trạng thái AI xuống client
            if (Time.time - aiLastSyncTime >= aiSyncInterval)
            {
                // Thu thập và cập nhật trạng thái mới từ tất cả AI
                foreach (var ai in aiEntities)
                {
                    if (ai != null)
                        ai.UpdateServerState();  // Gửi dữ liệu vị trí, trạng thái,... lên network
                }
                aiLastSyncTime = Time.time;
            }
        }
        else
        {
            // Ở phía client thì không gửi dữ liệu, chỉ nội suy để chuyển động mượt
            foreach (var ai in aiEntities)
            {
                if (ai != null)
                    ai.ClientInterpolate(aiLerpRate);  // Nội suy dựa trên dữ liệu sync từ server
            }
        }
    }

    // Đăng ký một AI vào hệ thống để quản lý đồng bộ
    public void RegisterAI(AIEntity ai)
    {
        if (!aiEntities.Contains(ai))
            aiEntities.Add(ai);
    }

    // Hủy đăng ký AI (khi AI bị xóa hoặc chết)
    public void UnregisterAI(AIEntity ai)
    {
        aiEntities.Remove(ai);
    }
}
