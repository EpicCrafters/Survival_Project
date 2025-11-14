using UnityEngine;
using UnityEngine.AI;
using Mirror;

public class AIEntity : NetworkBehaviour
{
    // BIẾN ĐỒNG BỘ HÓA (SyncVar)
    [SyncVar] private Vector3 syncedPosition;
    [SyncVar] private Quaternion syncedRotation;

    // 🔹 THAM CHIẾU VÀ TRẠNG THÁI
    public HFSMController controller;
    public HFSMAnimator animator;
    private NavMeshAgent navAgent;
    public bool IsSleeping { get; private set; }

    // 🔹 TỐI ƯU: Chạy AI logic mỗi frame thay vì chờ tick
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

        if (navAgent == null)
            navAgent = GetComponent<NavMeshAgent>();

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
    }

    // 🔹 CLIENT SIDE: NỘI SUY MƯỢT DỮ LIỆU
    [Client]
    public void ClientInterpolate()
    {
        if (isServer) return;

        float smoothFactor = 1 - Mathf.Exp(-clientLerpRate * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, syncedPosition, smoothFactor);
        transform.rotation = Quaternion.Slerp(transform.rotation, syncedRotation, smoothFactor);
    }

    // 🔹 LOGIC AI - Được gọi từ AISyncManager
    public void TickAI()
    {
        if (controller == null) return;
        if (IsSleeping || controller.syncedIsDead) return;

        // Quét mục tiêu và cập nhật FSM
        controller.ScanForTargets();
        controller.CurrentState?.Update();

        // 🎬 CẬP NHẬT ANIMATOR (nếu có và không đang ngủ)
        if (animator != null && !IsSleeping)
        {
            animator.UpdateAnimation();
        }
    }

    // 🔹 SLEEP/WAKE MANAGEMENT - SIMPLIFIED (Animator stays enabled)
    public void Sleep()
    {
        if (IsSleeping) return; // Tránh gọi nhiều lần

        IsSleeping = true;
        if (controller != null)
            controller.isSleeping = true;

        // 🚫 TẮT NAVMESHAGENT (tiết kiệm hiệu năng)
        if (navAgent != null && navAgent.enabled)
        {
            navAgent.isStopped = true;
            navAgent.velocity = Vector3.zero;
            navAgent.ResetPath();
            navAgent.enabled = false;
        }

        // 💤 ĐẶT ANIMATOR VỀ TRẠNG THÁI NGỦ (nhưng không disable)
        if (animator != null)
        {
            animator.DisableAnimator(); // This now just sets idle state
        }

        Debug.Log($"[AIEntity] 💤 {gameObject.name} Sleep - NavAgent: OFF | Animator: Idle");
    }

    public void WakeUp()
    {
        if (!IsSleeping) return; // Tránh gọi nhiều lần

        IsSleeping = false;
        if (controller != null)
            controller.isSleeping = false;

        // ✅ BẬT LẠI NAVMESHAGENT
        if (navAgent != null)
        {
            navAgent.enabled = true;
            navAgent.isStopped = false;
            navAgent.velocity = Vector3.zero;
        }

        // ✅ ĐÁNH THỨC ANIMATOR (resync state)
        if (animator != null)
        {
            animator.EnableAnimator(); // This resyncs animation state
        }

        // 🔥 Force FSM update
        if (isServer && controller?.CurrentState != null)
        {
            controller.CurrentState.Update();
        }

        Debug.Log($"[AIEntity] ⏰ {gameObject.name} Wake - NavAgent: ON | Animator: Active");
    }
}