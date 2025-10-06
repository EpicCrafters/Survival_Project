using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Mirror;
using static BaseAnimalAI;

public class HFSMController : NetworkBehaviour, IDamageable
{
    
    // References
   
    [Header("Animator")]
    public HFSMAnimator animator;

    [Header("Animal Data")]
    public AnimalData animalData;
    private AIRagdoll aIRagdoll;

    [Header("NavMesh Agent")]
    [HideInInspector] public NavMeshAgent agent;

    [Header("Ragdoll")]
    [SerializeField] private Transform ragdollRootBone;
    public Rigidbody baseRigidbody;

    [Header("Hurt Box")]
    public Collider hurtBox;

    [Header("Effects")]
    public GameObject hitEffectPrefab;

    [Header("Health")]
    public HealthBarUI healthBarUI;
    [HideInInspector] public HealthSystem healthSystem;

    [Header("Runtime")]
    public IDetectable currentTarget;
    public State CurrentState { get; private set; }

  
    // AI Settings
   
    [Header("AI Settings")]
    public bool isPredator = false;
    public bool isDead = false;
    public float detectionRadius = 12f;
    public float moveSpeed = 3.5f;
    public float attackRange = 5f;
    public float facingAngleThreshold = 12f;
    public float rotationSpeed = 10f;
    

    private HashSet<string> allowedStates;

   
    // Network Sync
  
    [SyncVar(hook = nameof(OnHealthChanged))]
    private int syncedHealth;

    [SyncVar(hook = nameof(OnStateChanged))]
    private string syncedStateName;

    [SyncVar]
    private bool syncedIsDead = false;

   
    // Unity Callbacks
  
    private void Awake()
    {
        // Khởi tạo hệ thống máu
        healthSystem = new HealthSystem(animalData.maxHealth);

        // Lấy component ragdoll & NavMeshAgent
        aIRagdoll = GetComponent<AIRagdoll>();
        agent = GetComponent<NavMeshAgent>();
        if (agent != null) agent.speed = moveSpeed;
        baseRigidbody.isKinematic = true;
        baseRigidbody.useGravity = true;
        
        // Tắt ragdoll lúc bắt đầu
        aIRagdoll.SetRagdoll(false);

        // Lấy danh sách trạng thái được phép từ AnimalData
        allowedStates = new HashSet<string>(animalData.allowedStates);
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        // Chỉ server đăng ký event khi chết
        healthSystem.OnDead += Die;

        // Cấu hình health bar cho server
        if (healthBarUI != null)
            healthBarUI.SetHealthSystem(healthSystem);

        // Đồng bộ máu ban đầu
        syncedHealth = animalData.maxHealth;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        // Chỉ client thuần mới setup health bar
        if (!isServer)
        {
            if (healthBarUI != null)
                healthBarUI.SetHealthSystem(healthSystem);

            // Đồng bộ máu ban đầu
            UpdateClientHealth(syncedHealth);
        }
        if (syncedIsDead)
        {
            HandleClientDeath();
        }
    }

    private void Start()
    {
        // Chỉ server chạy AI logic
        if (!isServer) return;

        if (CanRunState(typeof(NormalState)))
            ChangeState(new NormalState(this));
        else
            Debug.LogWarning($"{animalData.animalName} has no NormalState allowed!");
    }

    private void Update()
    {
        // Chỉ server chạy AI logic
        if (!isServer) return;
        if (syncedIsDead) return;

        ScanForTargets();
        CurrentState?.Update();
    }

    
    private void FixedUpdate()
    {
        if (isDead && ragdollRootBone != null && baseRigidbody != null)
        {
            // Compute target position (keep root height if you don’t want full drop)
            Vector3 targetPos = ragdollRootBone.position;

            // Smooth follow using MovePosition
            Vector3 newPos = Vector3.Lerp(baseRigidbody.position, targetPos, Time.fixedDeltaTime * 10f);
            baseRigidbody.MovePosition(newPos);

            // (Optional) Only rotate around Y to keep orientation reasonable
            Quaternion targetRot = Quaternion.Euler(0, ragdollRootBone.rotation.eulerAngles.y, 0);
            baseRigidbody.MoveRotation(Quaternion.Slerp(baseRigidbody.rotation, targetRot, Time.fixedDeltaTime * 10f));
        }
        //if (isDead && ragdollRootBone != null && !baseRigidbody.isKinematic)
        //{
        //    // Use physics movement instead of transform teleport
        //    Vector3 targetPos = ragdollRootBone.position;
        //    baseRigidbody.MovePosition(Vector3.Lerp(baseRigidbody.position, targetPos, Time.fixedDeltaTime * 15f));

        //    Quaternion targetRot = ragdollRootBone.rotation;
        //    baseRigidbody.MoveRotation(Quaternion.Slerp(baseRigidbody.rotation, targetRot, Time.fixedDeltaTime * 10f));
        //}
    }
    // ==========================================================
    // AI State Management
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

        // Đồng bộ trạng thái cho client
        if (isServer)
            syncedStateName = newState.GetType().Name;
    }

    public bool CanRunState(System.Type stateType) => allowedStates.Contains(stateType.Name);

    // ==========================================================
    // Target & Detection
    // ==========================================================
    private void ScanForTargets()
    {
        currentTarget = DetectionUtility.FindBestTargetByInterface(
            transform, detectionRadius, isPredator,
            new[] { TargetCategory.Player, TargetCategory.Enemy },
            new[] { TargetCategory.Enemy }
        );
    }

    public bool HasTarget() => currentTarget != null;
    public Transform GetTargetTransform() => (currentTarget as MonoBehaviour)?.transform;

    public bool IsCloseToTarget(float extra = 0f)
    {
        if (!HasTarget()) return false;
        return Vector3.Distance(transform.position, GetTargetTransform().position) <= attackRange + extra;
    }

    public bool IsFacingTarget(float angle = -1f)
    {
        if (!HasTarget()) return false;
        if (angle < 0) angle = facingAngleThreshold;
        Vector3 dir = (GetTargetTransform().position - transform.position).normalized;
        dir.y = 0;
        return Vector3.Angle(transform.forward, dir) <= angle;
    }

   
    // Damage / Hit
    
    public void Damage(int amount, HitInfo hit)
    {
        if (!isServer) return;

        // Trừ máu
        healthSystem.Damage(amount);
        syncedHealth = healthSystem.GetHealth();

        Debug.Log($"{name} took {amount} damage at {hit.point}");

        // Spawn hiệu ứng hit trên client
        RpcSpawnHitEffect(hit.point, hit.normal);

        // Nếu chết
        if (healthSystem.GetHealth() <= 0)
        {
            Die();
            return;
        }

        // Nếu còn sống → phản ứng hit
        RpcPlayHitAnimation();

        if (CurrentState is CombatState combat)
            combat.SetSubState(new RecoveryState(this, 2f));
        else
            ChangeState(new RecoveryState(this, 2f));
    }

    public bool CanTriggerHitStop() => true;
    public bool IsDead() => syncedIsDead;

   
    // Death Logic
    
    protected virtual void Die()
    {
        if (!isServer) return;

        isDead = true;
        syncedIsDead = true;
        Debug.Log($"{name} died!");
        baseRigidbody.isKinematic = false;
        baseRigidbody.useGravity = false;
        hurtBox.isTrigger = true;
        if (agent != null && agent.enabled)
        {
            agent.enabled = false; 
        }

        CurrentState = null;

        // Gọi client RPC để hiển thị ragdoll, tắt animator, health bar
        RpcDie();
    }

   

  
    // Network Callbacks
  
    private void OnHealthChanged(int oldHealth, int newHealth)
    {
        if (!isServer)
            UpdateClientHealth(newHealth);
    }

    private void UpdateClientHealth(int currentHealth)
    {
        int currentHealthValue = healthSystem.GetHealth();
        int difference = currentHealthValue - currentHealth;

        if (difference > 0)
            healthSystem.Damage(difference); // Trừ máu trên client
        else if (difference < 0)
            healthSystem.Heal(-difference); // Hồi máu trên client
    }

    private void OnStateChanged(string oldState, string newState)
    {
        if (isServer) return; // Server không cần xử lý
        Debug.Log($"[Client] State changed to: {newState}");
    }
    private void HandleClientDeath()
    {
        if (healthBarUI != null)
            healthBarUI.gameObject.SetActive(false);
        if (animator != null)
            animator.DisableAnimator();
        if (agent != null)
            agent.enabled = false;
        if (aIRagdoll != null)
            aIRagdoll.SetRagdoll(true);
    }


    // Client RPCs

    [ClientRpc]
    private void RpcSpawnHitEffect(Vector3 position, Vector3 normal)
    {
        if (hitEffectPrefab != null)
            Instantiate(hitEffectPrefab, position, Quaternion.LookRotation(normal));
    }

    [ClientRpc]
    private void RpcPlayHitAnimation()
    {
        if (animator != null)
            animator.PlayHitAnimation();
    }

    [ClientRpc]
    private void RpcDie()
    {
        HandleClientDeath();
    }
}
