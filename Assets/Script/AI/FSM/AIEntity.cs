using UnityEngine;
using Mirror;


public class AIEntity : NetworkBehaviour
{
   
    // SyncVars để Mirror tự động đồng bộ từ server → client

    [SyncVar] private Vector3 syncedPosition;
    [SyncVar] private Quaternion syncedRotation;

   
    // Unity Callbacks

    void Start()
    {
        // Đăng ký AI với AISyncManager để quản lý tập trung
        if (AISyncManager.Instance != null)
            AISyncManager.Instance.RegisterAI(this);
    }

    void OnDestroy()
    {
        // Hủy đăng ký khi đối tượng bị hủy
        if (AISyncManager.Instance != null)
            AISyncManager.Instance.UnregisterAI(this);
    }

  
    // Server logic
  
  
    // Chỉ server gọi để cập nhật vị trí và rotation đồng bộ
   
    [Server]
    public void UpdateServerState()
    {
        syncedPosition = transform.position;
        syncedRotation = transform.rotation;
    }

    // ==========================================================
    // Client logic
    // ==========================================================
   
    //Chỉ client (không phải server) sử dụng Lerp/Slerp để mượt mà
    // đồng bộ với server
    [Client]
    public void ClientInterpolate(float lerpRate)
    {
        if (!isServer) // client thuần
        {
            transform.position = Vector3.Lerp(transform.position, syncedPosition, Time.deltaTime * lerpRate);
            transform.rotation = Quaternion.Slerp(transform.rotation, syncedRotation, Time.deltaTime * lerpRate);
        }
    }
}
