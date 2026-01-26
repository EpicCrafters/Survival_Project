using UnityEngine;
using Mirror;

public class AIEntity : NetworkBehaviour
{
    // BIẾN ĐỒNG BỘ HÓA (SyncVar)
    [SyncVar] private Vector3 syncedPosition;
    [SyncVar] private Quaternion syncedRotation;
    [SyncVar] private Vector3 syncedVelocity;

    // 🔹 THAM CHIẾU VÀ TRẠNG THÁI
    public HFSMController controller;
    public HFSMAnimator animator;
    private CharacterController characterController;
    public bool IsSleeping { get; private set; }

    // 🔹 TỐI ƯU: Chạy AI logic mỗi frame
    [Header("Performance Settings")]
    [Tooltip("Nếu false, chỉ chạy AI trên server (tiết kiệm hiệu năng)")]
    public bool runAIOnClient = false;

    [Tooltip("Tốc độ nội suy cho client")]
    public float clientLerpRate = 10f;

    // 🔹 HÀM KHỞI TẠO / HỦY
    void Start()
    {
        // Tự động lấy các component nếu chưa gán
        if (animator == null)
            animator = GetComponent<HFSMAnimator>();

        if (characterController == null)
            characterController = GetComponent<CharacterController>();

        if (controller == null)
            controller = GetComponent<HFSMController>();

        // ✅ CHARACTER CONTROLLER CHỈ HOẠT ĐỘNG TRÊN SERVER
        if (characterController != null)
        {
            if (isServer)
            {
                characterController.enabled = !controller.syncedIsDead;
            }
            else
            {
                // ❌ TẮT HOÀN TOÀN TRÊN CLIENT
                characterController.enabled = false;
            }
        }

        if (AISyncManager.Instance != null)
            AISyncManager.Instance.RegisterAI(this);
    }

    void OnDestroy()
    {
        if (AISyncManager.Instance != null)
            AISyncManager.Instance.UnregisterAI(this);
    }

    // 🔹 SERVER SIDE: CẬP NHẬT TRẠNG THÁI
    [Server]
    public void UpdateServerState()
    {
        syncedPosition = transform.position;
        syncedRotation = transform.rotation;

        // ✅ LẤY VELOCITY TỪ CONTROLLER
        if (controller != null)
        {
            syncedVelocity = controller.GetVelocity();
        }
    }

    // 🔹 CLIENT SIDE: NỘI SUY MƯỢT DỮ LIỆU
    [Client]
    public void ClientInterpolate()
    {
        if (isServer) return;

        // ✅ INTERPOLATION MỀM MẠI HƠN
        float smoothFactor = 1 - Mathf.Exp(-clientLerpRate * Time.deltaTime);

        // Sử dụng distance check để tránh teleport
        float distance = Vector3.Distance(transform.position, syncedPosition);

        if (distance > 5f) // Nếu quá xa, teleport luôn
        {
            transform.position = syncedPosition;
            transform.rotation = syncedRotation;
        }
        else if (distance > 0.01f) // Chỉ interpolate khi có sự khác biệt đáng kể
        {
            transform.position = Vector3.Lerp(transform.position, syncedPosition, smoothFactor);
            transform.rotation = Quaternion.Slerp(transform.rotation, syncedRotation, smoothFactor);
        }
    }

    // 🔹 LOGIC AI - Được gọi từ AISyncManager
    public void TickAI()
    {
        if (controller == null) return;
        if (IsSleeping || controller.syncedIsDead) return;

        // Quét mục tiêu và cập nhật FSM
        controller.ScanForTargets();
        controller.CurrentState?.Update();

        // 🎬 CẬP NHẬT ANIMATOR
        if (animator != null && !IsSleeping)
        {
            animator.UpdateAnimation();
        }
    }

    // 🔹 SLEEP/WAKE MANAGEMENT
    public void Sleep()
    {
        if (IsSleeping) return;

        IsSleeping = true;
        if (controller != null)
            controller.isSleeping = true;

        // 🚫 TẮT CHARACTER CONTROLLER
        if (characterController != null)
        {
            characterController.enabled = false;
        }

        // 💤 ĐẶT ANIMATOR VỀ TRẠNG THÁI NGỦ
        if (animator != null)
        {
            animator.DisableAnimator();
        }

        Debug.Log($"[AIEntity] 💤 {gameObject.name} Sleep - CharacterController: Disabled");
    }

    public void WakeUp()
    {
        if (!IsSleeping) return;

        IsSleeping = false;
        if (controller != null)
            controller.isSleeping = false;

        // ✅ BẬT LẠI CHARACTER CONTROLLER
        if (characterController != null)
        {
            characterController.enabled = true;
        }

        // ✅ ĐÁNH THỨC ANIMATOR
        if (animator != null)
        {
            animator.EnableAnimator();
        }

        // 🔥 Force FSM update
        if (isServer && controller?.CurrentState != null)
        {
            controller.CurrentState.Update();
        }

        Debug.Log($"[AIEntity] ⏰ {gameObject.name} Wake - CharacterController: Enabled");
    }
}