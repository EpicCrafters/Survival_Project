using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Mirror;
using static BaseAnimalAI;

public class HFSMController : NetworkBehaviour, IDamageable
{
    // ==========================================================
    // 🔹 THAM CHIẾU & THÀNH PHẦN
    // ==========================================================
    [Header("Animator")]
    public HFSMAnimator animator;

    [Header("Animal Data")]
    public AnimalData animalData;

    [Header("NavMesh Agent")]
    public NavMeshAgent agent;

    [Header("Ragdoll")]
    [SerializeField] private Transform ragdollRootBone;
    public Rigidbody baseRigidbody; // Root rigidbody (KHÔNG phải ragdoll)
    private AIRagdoll aIRagdoll;
    private List<Rigidbody> ragdollRigidbodies = new List<Rigidbody>(); // Chỉ chứa ragdoll bones

    [Header("Hurt Box")]
    public Collider hurtBox;

    [Header("Hiệu ứng va chạm")]
    public GameObject hitEffectPrefab;

    [Header("Thanh máu (UI)")]
    public HealthBarUI healthBarUI;
    [HideInInspector] public HealthSystem healthSystem;

    [Header("Trạng thái runtime")]
    public IDetectable currentTarget;
    public bool enemySpotted;
    public State CurrentState { get; private set; }

    // ==========================================================
    //  CÀI ĐẶT HÀNH VI AI
    // ==========================================================
    [Header("AI Settings")]
    public bool isPredator = false;
    public bool isDead = false;
    public float detectionRadius = 12f;
    public float moveSpeed = 3.5f;
    public float attackRange = 5f;
    public float facingAngleThreshold = 12f;
    public float rotationSpeed = 10f;

    private HashSet<string> allowedStates;

    // ==========================================================
    //  ĐỒNG BỘ MẠNG (Mirror SyncVars)
    // ==========================================================
    [SyncVar(hook = nameof(OnHealthChanged))] private int syncedHealth;
    [SyncVar(hook = nameof(OnStateChanged))] private string syncedStateName;
    [SyncVar] public bool syncedIsDead = false;

    // ==========================================================
    //  QUẢN LÝ DANH TÍNH & TRẠNG THÁI
    // ==========================================================
    public string poolKey;
    public bool isSleeping = false;
    public bool pooled = true;
    [HideInInspector] public string uniqueId;

    private bool wasFirstSpawn = false;

    // ==========================================================
    //  KHỞI TẠO & CẤU HÌNH THÀNH PHẦN
    // ==========================================================
    private void Awake() => InitializeComponents();

    private void InitializeComponents()
    {
        animator?.EnableAnimator();

        if (healthSystem == null || healthSystem.GetHealth() <= 0)
            healthSystem = new HealthSystem(animalData.maxHealth);

        aIRagdoll ??= GetComponent<AIRagdoll>();
        agent ??= GetComponent<NavMeshAgent>();

        if (agent != null)
        {
            agent.speed = moveSpeed;
            agent.enabled = true;
        }

        if (baseRigidbody != null)
        {
            baseRigidbody.isKinematic = true;
            baseRigidbody.useGravity = true;
        }

        aIRagdoll?.SetRagdoll(false);

        // Cache tất cả ragdoll rigidbodies (BỎ QUA baseRigidbody)
        if (ragdollRootBone != null && ragdollRigidbodies.Count == 0)
        {
            Rigidbody[] allRigidbodies = ragdollRootBone.GetComponentsInChildren<Rigidbody>();

            foreach (var rb in allRigidbodies)
            {
                // Chỉ thêm ragdoll bones, KHÔNG thêm baseRigidbody
                if (rb != baseRigidbody && rb != null)
                {
                    ragdollRigidbodies.Add(rb);
                }
            }

            Debug.Log($"[{name}] Cached {ragdollRigidbodies.Count} ragdoll bones (excluding base rigidbody)");
        }

        allowedStates ??= new HashSet<string>(animalData.allowedStates);
    }

    // ==========================================================
    //  VÒNG ĐỜI NETWORK
    // ==========================================================
    public override void OnStartServer()
    {
        base.OnStartServer();

        if (string.IsNullOrEmpty(uniqueId))
        {
            uniqueId = System.Guid.NewGuid().ToString();
            wasFirstSpawn = true;
            Debug.Log($"[HFSMController] Generated uniqueId: {uniqueId}");
        }

        // Subscribe với wrapper method không có tham số
        healthSystem.OnDead += OnHealthSystemDead;

        if (healthBarUI != null)
            healthBarUI.SetHealthSystem(healthSystem);

        syncedHealth = animalData.maxHealth;
    }

    // Wrapper method để match với Action delegate
    private void OnHealthSystemDead()
    {
        Die(); // Gọi Die() không có HitInfo
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (!isServer)
        {
            healthBarUI?.SetHealthSystem(healthSystem);
            UpdateClientHealth(syncedHealth);
        }

        if (syncedIsDead)
            HandleClientDeath();
    }

    // ==========================================================
    //  KHỞI ĐỘNG / CẬP NHẬT
    // ==========================================================
    private void Start()
    {
        if (!isServer) return;

        if (CanRunState(typeof(NormalState)))
            ChangeState(new NormalState(this));
        else
            Debug.LogWarning($"{animalData.animalName} has no NormalState allowed!");
    }

    private void FixedUpdate()
    {
        if (isDead && ragdollRootBone != null && baseRigidbody != null)
        {
            Vector3 targetPos = ragdollRootBone.position;
            Vector3 newPos = Vector3.Lerp(baseRigidbody.position, targetPos, Time.fixedDeltaTime * 10f);
            baseRigidbody.MovePosition(newPos);

            Quaternion targetRot = Quaternion.Euler(0, ragdollRootBone.rotation.eulerAngles.y, 0);
            baseRigidbody.MoveRotation(Quaternion.Slerp(baseRigidbody.rotation, targetRot, Time.fixedDeltaTime * 10f));
        }
    }

    // ==========================================================
    //  QUẢN LÝ STATE CỦA AI
    // ==========================================================
    public void ChangeState(State newState)
    {
        if (!CanRunState(newState.GetType()))
        {
            Debug.LogWarning($"{animalData.animalName} cannot enter state {newState.GetType().Name}");
            return;
        }

        CurrentState?.OnExit();
        CurrentState = newState;
        CurrentState.OnEnter();

        if (isServer)
            syncedStateName = newState.GetType().Name;
    }

    public bool CanRunState(System.Type stateType) => allowedStates.Contains(stateType.Name);

    // ==========================================================
    // PHÁT HIỆN MỤC TIÊU
    // ==========================================================
    public void ScanForTargets()
    {
        TargetCategory[] detect = isPredator
            ? new[] { TargetCategory.Player, TargetCategory.Enemy }
            : new[] { TargetCategory.Player };

        currentTarget = DetectionUtility.FindBestTargetByInterface(
            transform, detectionRadius, isPredator, detect, detect);

        enemySpotted = currentTarget != null;
    }

    public bool HasTarget() => currentTarget != null;
    public Transform GetTargetTransform() => (currentTarget as MonoBehaviour)?.transform;
    public bool IsCloseToTarget(float extra = 0f)
        => HasTarget() && Vector3.Distance(transform.position, GetTargetTransform().position) <= attackRange + extra;
    public bool IsFacingTarget(float angle = -1f)
    {
        if (!HasTarget()) return false;
        if (angle < 0) angle = facingAngleThreshold;
        Vector3 dir = (GetTargetTransform().position - transform.position).normalized;
        dir.y = 0;
        return Vector3.Angle(transform.forward, dir) <= angle;
    }

    // ==========================================================
    //  NHẬN SÁT THƯƠNG & CHẾT
    // ==========================================================
    
    public void Damage(int amount, HitInfo hit)
    {
        if (!isServer) return;

        // Kiểm tra xem cú đánh này có giết chết AI không
        int healthAfterDamage = healthSystem.GetHealth() - amount;
        bool willDie = healthAfterDamage <= 0;

        healthSystem.Damage(amount);
        syncedHealth = healthSystem.GetHealth();

        Debug.Log($"{name} took {amount} damage at {hit.point}");

        RpcSpawnHitEffect(hit.point, hit.normal);

        if (healthSystem.GetHealth() <= 0)
        {
            // CHỈ apply knockback nếu chết do damage (không phải do OnDead event)
            Die(hit); // Truyền HitInfo để apply knockback
            return;
        }

        RpcPlayHitAnimation();

        if (CurrentState is CombatState combat)
            combat.SetSubState(new RecoveryState(this, 2f));
        else
            ChangeState(new RecoveryState(this, 2f));
    }

    protected virtual void Die(HitInfo? hit = null)
    {
        if (!isServer) return;

        isDead = true;
        syncedIsDead = true;

        Debug.Log($"{name} died! HasHitInfo={hit.HasValue}");

        baseRigidbody.isKinematic = false;
        baseRigidbody.useGravity = false;
        hurtBox.isTrigger = true;

        if (agent != null && agent.enabled)
            agent.enabled = false;

        CurrentState = null;

        
        RpcDie();
    }

    public bool CanTriggerHitStop() => true;
    public bool IsDead() => syncedIsDead;

    // ==========================================================
    //  NETWORK CALLBACKS
    // ==========================================================
    private void OnHealthChanged(int oldHealth, int newHealth)
    {
        if (!isServer)
            UpdateClientHealth(newHealth);
    }

    private void UpdateClientHealth(int currentHealth)
    {
        int currentHealthValue = healthSystem.GetHealth();
        int diff = currentHealthValue - currentHealth;

        if (diff > 0) healthSystem.Damage(diff);
        else if (diff < 0) healthSystem.Heal(-diff);
    }

    private void OnStateChanged(string oldState, string newState)
    {
        if (isServer) return;
        Debug.Log($"[Client] State changed to: {newState}");
    }

    public void HandleClientDeath()
    {
        healthBarUI?.gameObject.SetActive(false);
        animator?.DisableAnimatorForRagdoll();
        if (agent != null) agent.enabled = false;
        aIRagdoll?.SetRagdoll(true);
    }

    // ==========================================================
    //  CLIENT RPCs
    // ==========================================================
    [ClientRpc]
    private void RpcSpawnHitEffect(Vector3 pos, Vector3 normal)
    {
        if (hitEffectPrefab != null)
            Instantiate(hitEffectPrefab, pos, Quaternion.LookRotation(normal));
    }

    [ClientRpc]
    private void RpcPlayHitAnimation()
        => animator?.PlayHitAnimation();

    [ClientRpc]
    private void RpcDie() => HandleClientDeath();

    // RPC mới: Apply knockback với custom settings từ weapon
    [ClientRpc]
    public void RpcApplyDeathKnockback(Vector3 hitPoint, Vector3 hitDirection, float horizontal,  float radius)
    {
        // Tìm ragdoll bone gần nhất
        Rigidbody closestBone = FindClosestRagdollBone(hitPoint, radius);

        if (closestBone != null)
        {
            Vector3 forceDirection = hitDirection.normalized;
            Vector3 horizontalForce = new Vector3(forceDirection.x, 0, forceDirection.z).normalized * horizontal;
            //Vector3 upwardForce = Vector3.up * upward;
            Vector3 totalForce = horizontalForce;//+ upwardForce;

            closestBone.AddForceAtPosition(totalForce, hitPoint, ForceMode.Impulse);

            Debug.Log($"[{name}] Applied knockback: H={horizontal},  Bone={closestBone.name}");
        }
        else
        {
            Debug.LogWarning($"[{name}] No ragdoll bone found!");
        }
    }

    // ==========================================================
    //  TÌM RAGDOLL BONE GẦN NHẤT
    // ==========================================================
    private Rigidbody FindClosestRagdollBone(Vector3 hitPoint, float searchRadius = -1f)
    {
       

        // Đảm bảo list đã được cache
        if (ragdollRigidbodies.Count == 0)
        {
            if (ragdollRootBone != null)
            {
                Rigidbody[] allRigidbodies = ragdollRootBone.GetComponentsInChildren<Rigidbody>();

                foreach (var rb in allRigidbodies)
                {
                    if (rb != baseRigidbody && rb != null)
                    {
                        ragdollRigidbodies.Add(rb);
                    }
                }
            }
        }

        if (ragdollRigidbodies.Count == 0)
        {
            Debug.LogWarning($"[{name}] No ragdoll rigidbodies found!");
            return null;
        }

        Rigidbody closest = null;
        float closestDistance = float.MaxValue;

        // Tìm bone gần nhất trong bán kính
        foreach (var rb in ragdollRigidbodies)
        {
            if (rb == null || rb == baseRigidbody) continue;

            float distance = Vector3.Distance(rb.position, hitPoint);

            if (distance < closestDistance && distance <= searchRadius)
            {
                closestDistance = distance;
                closest = rb;
            }
        }

        // Fallback: nếu không có bone nào trong radius, lấy bone gần nhất
        if (closest == null)
        {
            foreach (var rb in ragdollRigidbodies)
            {
                if (rb == null || rb == baseRigidbody) continue;

                float distance = Vector3.Distance(rb.position, hitPoint);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closest = rb;
                }
            }
        }

        if (closest != null)
        {
            Debug.Log($"🎯 [{name}] Closest bone hit: {closest.name} (distance {closestDistance:F2}m)");
        }
        else
        {
            Debug.LogWarning($"⚠️ [{name}] Could not find any ragdoll bone near hit point!");
        }

        return closest;
    }

    // ==========================================================
    // NGỦ / THỨC
    // ==========================================================
    public void Sleep()
    {
        if (isSleeping) return;

        isSleeping = true;
        Debug.Log($"[HFSMController] {name} sleeping (id={uniqueId})");

        if (isServer)
        {
            CurrentState?.OnExit();
            CurrentState = null;
        }

        currentTarget = null;
        enemySpotted = false;

        if (agent != null && agent.enabled)
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.enabled = false;
        }

        animator?.DisableAnimator();
        enabled = false;
    }

    public void WakeUp()
    {
        if (!isSleeping) return;
        isSleeping = false;

        InitializeComponents();
        enabled = true;

        if (agent != null)
        {
            agent.enabled = true;

            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 10f, NavMesh.AllAreas))
                agent.Warp(hit.position);
            else
                agent.Warp(transform.position);

            agent.isStopped = false;
            agent.ResetPath();
        }

        animator?.EnableAnimator();

        if (isServer)
        {
            CurrentState?.OnExit();
            CurrentState = null;
            if (CanRunState(typeof(NormalState)))
                ChangeState(new NormalState(this));
        }

        Debug.Log($"[HFSMController] {name} woke up (id={uniqueId})");
    }

    // ==========================================================
    // 💾 LƯU / TẢI DỮ LIỆU
    // ==========================================================
    public AILoadData GetSaveData() => new AILoadData
    {
        uniqueId = uniqueId,
        poolKey = poolKey,
        position = transform.position,
        rotation = transform.rotation,
        health = healthSystem.GetHealth(),
        isDead = isDead,
        lastSavedTime = Time.time
    };

    // ==========================================================
    // ✅ TIỆN ÍCH
    // ==========================================================
    public void OnRespawn()
    {
        if (!gameObject.activeSelf) return;
        InitializeComponents();
        Debug.Log($"✅ {name} Reactivated (id={uniqueId})");
        pooled = false;
        aIRagdoll?.SetRagdoll(false);
        animator?.EnableAnimator();

        if (agent != null)
        {
            agent.enabled = true;
            agent.Warp(transform.position);
            agent.isStopped = false;
        }

        isDead = false;
        isSleeping = false;
        enabled = true;

        if (isServer)
        {
            CurrentState?.OnExit();
            CurrentState = null;
            if (CanRunState(typeof(NormalState)))
                ChangeState(new NormalState(this));
        }
    }

    public bool IsFirstSpawn() => wasFirstSpawn;
}