using UnityEngine;
using System.Collections;

// BaseAnimalAI là lớp trừu tượng quản lý AI chung cho động vật
// Bao gồm quản lý trạng thái, sát thương, di chuyển, animation và ragdoll
public abstract class BaseAnimalAI : MonoBehaviour, IDamageable
{
    [Header("Dữ liệu chính")]
    public AnimalData animalData;         // Thông tin chung về động vật (máu, speed, v.v.)
    public Transform chasingTarget;       // Mục tiêu đang theo đuổi (target)

    [Header("Máu")]
    public HealthBarUI healthBarUI;       // UI hiển thị máu
    [HideInInspector] public HealthSystem healthSystem;

    public float stopDistance;            // Khoảng cách dừng lại khi chase

    [Header("Hurt Box")]
    public Collider hurtBox;              // Collider nhận sát thương
    public Rigidbody baseRigibody;        // Rigidbody chính để apply lực knockback

    [Header("Ragdoll")]
    public Rigidbody[] ragdollBodies;        // Các rigidbody của ragdoll
    public CapsuleCollider[] ragdollColliders; // Các collider ragdoll
    public float ragdollDestroyDelay = 5f;   // Thời gian destroy object sau khi ragdoll

    [Header("Hit Recovery")]
    public float hitRecoveryTime = 2f;    // Thời gian hồi sau khi bị hit

    // Các component phụ trách
    protected AnimalMovement movement;
    protected AnimalAnimator animationHandler;
    protected AnimalCombat attackHandler;

    // Các trạng thái AI
    public enum AnimalState { Idle, Wander, Flee, Chase, Dead, Attack, Hit }
    [SerializeField] protected AnimalState currentState;
    protected float stateTimer;

    protected bool canAttack = true;
    protected AnimalState stateBeforeHit;
    protected bool isDead = false;
    protected bool isRecoveringFromHit = false;

    [Header("Debug/Test")]
    public int debugDamageAmount = 10;

    // Awake: khởi tạo health system và ragdoll
    protected virtual void Awake()
    {
        healthSystem = new HealthSystem(animalData.maxHealth);
        healthSystem.OnDead += Die; // Đăng ký callback khi chết

        if (healthBarUI != null)
            healthBarUI.SetHealthSystem(healthSystem);

        SetRagdollActive(false); // Vô hiệu ragdoll lúc start
    }

    protected virtual void Start()
    {
        if (healthBarUI != null)
            healthBarUI.gameObject.SetActive(false);
        movement = GetComponent<AnimalMovement>();
        animationHandler = GetComponent<AnimalAnimator>();
        attackHandler = GetComponent<AnimalCombat>();
        ragdollBodies = GetComponentsInChildren<Rigidbody>();
        ragdollColliders = GetComponentsInChildren<CapsuleCollider>();
        ChangeState(AnimalState.Idle); // Bắt đầu ở trạng thái Idle
    }

    protected virtual void Update()
    {
        // Nếu chết hoặc đang hit, không xử lý AI
        if (currentState == AnimalState.Dead || currentState == AnimalState.Hit)
            return;

        // Nếu chase, update destination
        if (currentState == AnimalState.Chase && chasingTarget != null)
            movement.UpdateChaseDestination(chasingTarget, stopDistance);

        // Giảm timer trạng thái
        if (stateTimer > 0f)
            stateTimer -= Time.deltaTime;

        // Cập nhật animation
        animationHandler.UpdateAnimation();

        // Gọi phương thức AI riêng của động vật
        HandleAI();
    }

    // Tìm target gần nhất trong scene
    protected Transform FindNearestTarget()
    {
        GameObject[] targets = GameObject.FindGameObjectsWithTag("Target");
        Transform nearest = null;
        float minDist = Mathf.Infinity;

        foreach (GameObject t in targets)
        {
            float dist = Vector3.Distance(transform.position, t.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = t.transform;
            }
        }

        return nearest;
    }

    // Các phương thức trừu tượng để lớp con implement
    protected abstract void HandleAI();
    protected abstract void HandleTargetInRange(float distanceToTarget);
    protected abstract bool ShouldReactToTarget(float distanceToTarget);

    // AI chung cho tất cả động vật (Idle, Wander, Chase)
    protected virtual void HandleCommonAI()
    {
        // Recovery sau khi bị hit
        if (isRecoveringFromHit && stateTimer > 0f)
            return;

        if (isRecoveringFromHit && stateTimer <= 0f)
        {
            isRecoveringFromHit = false;
            Debug.Log("Recovery from hit completed");
        }

        // Idle cooldown
        if (currentState == AnimalState.Idle && stateTimer > 0f)
            return;

        // Xử lý target detection
        if (chasingTarget != null)
        {
            float distanceToTarget = Vector3.Distance(transform.position, chasingTarget.position);

            if (ShouldReactToTarget(distanceToTarget) && !isRecoveringFromHit)
            {
                HandleTargetInRange(distanceToTarget);
                return;
            }
            else if (distanceToTarget > movement.detectionRange)
            {
                chasingTarget = FindNearestTarget();
                if (chasingTarget == null)
                    ChangeState(AnimalState.Idle);
            }
        }

        // Idle/Wander khi không có target
        if (currentState == AnimalState.Idle && stateTimer <= 0f && !isRecoveringFromHit)
        {
            ChangeState(AnimalState.Wander);
            return;
        }

        if (currentState == AnimalState.Wander && movement.IsAtDestination())
        {
            ChangeState(AnimalState.Idle);
            return;
        }
    }

    // Kiểm tra có thể attack target không
    protected bool CanAttackTarget(Transform target)
    {
        if (target == null) return false;
        float dist = Vector3.Distance(transform.position, target.position);
        return dist <= attackHandler.attackRange;
    }

    // Quay hướng nhìn về target
    protected void LookAtTarget(Transform target)
    {
        if (target == null) return;
        Vector3 direction = (target.position - transform.position).normalized;
        direction.y = 0;
        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 5f);
        }
    }

    // Kiểm tra động vật đang nhìn về target chưa
    protected bool IsLookingAtTarget()
    {
        if (chasingTarget == null) return true;
        Vector3 direction = (chasingTarget.position - transform.position).normalized;
        direction.y = 0;
        if (direction == Vector3.zero) return true;
        float angle = Vector3.Angle(transform.forward, direction);
        return angle < 10f;
    }

    // Chuyển trạng thái động vật
    public virtual void ChangeState(AnimalState newState)
    {
        if (currentState == newState) return;
        currentState = newState;

        switch (newState)
        {
            case AnimalState.Idle:
                animationHandler.PlayIdleAnimation();
                movement.Stop();
                if (!isRecoveringFromHit)
                    stateTimer = Random.Range(2f, 8f);
                break;

            case AnimalState.Wander:
                animationHandler.PlayMoveAnimation();
                movement.StartWander();
                stateTimer = 2f;
                break;

            case AnimalState.Flee:
                animationHandler.PlayMoveAnimation();
                movement.StartFlee(chasingTarget);
                break;

            case AnimalState.Chase:
                animationHandler.PlayChaseAnimation();
                movement.StartChase(chasingTarget);
                break;

            case AnimalState.Attack:
                movement.Stop();
                animationHandler.PlayAttackAnimation();
                stateTimer = attackHandler.attackCooldown;
                canAttack = false;
                StartCoroutine(attackHandler.AttackCooldown(() => {
                    canAttack = true;
                    if (!isDead)
                    {
                        ChangeState(AnimalState.Idle);
                        stateTimer = Random.Range(1f, 2f);
                    }
                }));
                break;

            case AnimalState.Hit:
                movement.Stop();
                attackHandler.StopAllCoroutines();
                animationHandler.ResetAllAnimations();
                animationHandler.PlayHitAnimation();
                stateTimer = animationHandler.hitAnimationDuration;
                StartCoroutine(HitRoutine(animationHandler.hitAnimationDuration));
                break;

            case AnimalState.Dead:
                movement.Stop();
                animationHandler.StopAnimation();
                break;
        }
    }
    public bool CanTriggerHitStop() => true; // Animals can trigger hit stop
    public bool IsDead() => isDead || (healthSystem != null && healthSystem.GetHealth() <= 0);
    // Nhận sát thương
    public virtual void Damage(int amount)
    {
        Debug.Log("Hit");
        if (currentState == AnimalState.Dead) return;

        healthSystem.Damage(amount);

        stateBeforeHit = currentState;
        ChangeState(AnimalState.Hit);
    }
    public void Damage(int amount, HitInfo hit)
    {

    }
    // Coroutine xử lý hit animation
    protected IEnumerator HitRoutine(float duration)
    {
        yield return new WaitForSeconds(duration);

        if (!isDead)
        {
            Debug.Log("Hit animation finished, entering recovery period");
            isRecoveringFromHit = true;
            canAttack = true;
            ChangeState(AnimalState.Idle);
            stateTimer = animationHandler.hitAnimationDuration;
        }
    }

    // Khi động vật chết
    protected virtual void Die()
    {
        if (isDead) return;
        isDead = true;
        hurtBox.enabled = false;
        ChangeState(AnimalState.Dead);

        if (healthBarUI != null)
            healthBarUI.gameObject.SetActive(false);
        if (animationHandler != null)
            animationHandler.DisableAnimator();

        ActivateRagdoll();
    }

    // Apply knockback
    public void ApplyKnockBack(Vector3 dir, float force)
    {
        if (!movement.agent.isOnNavMesh) return;
        movement.agent.isStopped = false;
        baseRigibody.AddForce(dir * force, ForceMode.Impulse);

        if (!movement.agent.isOnNavMesh) return;
        movement.agent.isStopped = true;
        Debug.Log("push back");
    }

    // Kích hoạt ragdoll khi chết
    protected void ActivateRagdoll()
    {
        SetRagdollActive(true);

        if (movement != null) movement.enabled = false;
        if (animationHandler != null) animationHandler.enabled = false;

        Destroy(gameObject, ragdollDestroyDelay);
    }

    // Kích hoạt/vô hiệu ragdoll
    protected void SetRagdollActive(bool active)
    {
        foreach (var rb in ragdollBodies)
            rb.isKinematic = !active;

        foreach (var col in ragdollColliders)
            col.enabled = active;

        var mainCollider = GetComponent<Collider>();
        if (mainCollider != null)
            mainCollider.enabled = !active;

        var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null)
            agent.enabled = !active;
    }

    // ContextMenu cho test damage
    [ContextMenu("Damage AI")]
    protected void DamageAIButton()
    {
        Damage(debugDamageAmount);
    }
}
