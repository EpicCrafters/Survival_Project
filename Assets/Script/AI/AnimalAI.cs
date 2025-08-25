using UnityEngine;
using System.Collections;

public class AnimalAI : MonoBehaviour, IDamageable
{
    [Header("Dữ liệu chính")]
    public AnimalData animalData; // Thông số của thú (máu, tốc độ, tấn công...)
    public Transform chasingTarget; // Mục tiêu để đuổi hoặc tấn công

    [Header("Máu")]
    public HealthBarUI healthBarUI; // UI hiển thị máu
    [HideInInspector] public HealthSystem healthSystem; // Hệ thống máu thực tế

    public float stopDistance;

    [Header("Hurt Box")]
    public Collider hurtBox; // Vùng nhận sát thương
    public Rigidbody baseRigibody; // Rigidbody chính (cho knockback)

    [Header("Ragdoll")]
    public Rigidbody[] ragdollBodies; // Các rigidbody khi thành ragdoll
    public CapsuleCollider[] ragdollColliders; // Các collider của ragdoll
    public float ragdollDestroyDelay = 5f; // Thời gian hủy object sau khi chết

    [Header("Hit Recovery")]
    public float hitRecoveryTime = 2f; // Time to stay idle after being hit

    private AnimalMovement movement; // Script điều khiển di chuyển
    private AnimalAnimator animationHandler; // Script điều khiển animation
    private AnimalCombat attackHandler; // Script điều khiển tấn công

    // Các trạng thái AI
    public enum AnimalState { Idle, Wander, Flee, Chase, Dead, Attack, Hit }
    [SerializeField] private AnimalState currentState;
    private float stateTimer; // Hẹn giờ cho các hành động (idle, wander...)

    private bool canAttack = true; // Có thể tấn công hay không
    private AnimalState stateBeforeHit; // Lưu trạng thái trước khi bị đánh
    private bool isDead = false; // Trạng thái chết
    private bool isRecoveringFromHit = false; // Flag to track if animal is recovering from hit

    [Header("Debug/Test")]
    public int debugDamageAmount = 10; // Lượng sát thương để test

    private void Awake()
    {
        healthSystem = new HealthSystem(animalData.maxHealth); // Tạo health system
        healthSystem.OnDead += Die; // Đăng ký sự kiện chết

        if (healthBarUI != null)
            healthBarUI.SetHealthSystem(healthSystem); // Gán health bar

        SetRagdollActive(false); // Tắt ragdoll lúc bắt đầu
    }

    private void Start()
    {
        movement = GetComponent<AnimalMovement>();
        animationHandler = GetComponent<AnimalAnimator>();
        attackHandler = GetComponent<AnimalCombat>();
        ragdollBodies = GetComponentsInChildren<Rigidbody>(); // Lấy tất cả rb con
        ragdollColliders = GetComponentsInChildren<CapsuleCollider>(); // Lấy tất cả collider con
        ChangeState(AnimalState.Idle); // Bắt đầu ở trạng thái Idle
    }

    private void Update()
    {
        if (currentState == AnimalState.Dead || currentState == AnimalState.Hit)
            return; // Không làm gì khi chết hoặc đang bị đánh

        if (currentState == AnimalState.Chase && chasingTarget != null)
            movement.UpdateChaseDestination(chasingTarget, stopDistance); // Cập nhật điểm đuổi

        if (stateTimer > 0f)
            stateTimer -= Time.deltaTime; // Giảm timer

        animationHandler.UpdateAnimation(); // Cập nhật animation
        HandleAIStates(); // Xử lý logic AI
    }

    private Transform FindNearestTarget()
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

    private void HandleAIStates()
    {
        // Nếu đang tấn công hoặc bị đánh thì bỏ qua
        if (currentState == AnimalState.Attack || currentState == AnimalState.Hit)
            return;

        // ✅ If recovering from hit, stay in idle until recovery time is over
        if (isRecoveringFromHit && stateTimer > 0f)
            return;

        // ✅ If recovery time is over, reset the flag
        if (isRecoveringFromHit && stateTimer <= 0f)
        {
            isRecoveringFromHit = false;
            Debug.Log("Recovery from hit completed");
        }

        // ✅ If in idle state and still has cooldown timer, don't do anything
        if (currentState == AnimalState.Idle && stateTimer > 0f)
            return;

        if (chasingTarget != null)
        {
            float distanceToTarget = Vector3.Distance(transform.position, chasingTarget.position);

            // ✅ ABSOLUTE PRIORITY: If target is in attack range and we can attack, ALWAYS attack (NEVER chase)
            if (CanAttackTarget(chasingTarget) && animalData.canAttack && canAttack && !isRecoveringFromHit)
            {
                if (!IsLookingAtTarget())
                {
                    LookAtTarget(chasingTarget);
                    return;
                }
                ChangeState(AnimalState.Attack);
                canAttack = false;
                return; // Exit immediately, don't check chase conditions
            }

            // ✅ Only check chase/flee if target is NOT in attack range and not recovering from hit
            if (distanceToTarget <= movement.detectionRange && !CanAttackTarget(chasingTarget) && !isRecoveringFromHit)
            {
                AnimalState nextState = animalData.canAttack ? AnimalState.Chase : AnimalState.Flee;
                if (currentState != nextState)
                    ChangeState(nextState);
                return;
            }
            else if (distanceToTarget > movement.detectionRange)
            {
                chasingTarget = FindNearestTarget();
                if (chasingTarget == null)
                    ChangeState(AnimalState.Idle);
            }
        }

        // Khi không có target, loop giữa Idle và Wander (only if no cooldown timer and not recovering)
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

    private bool CanAttackTarget(Transform target)
    {
        if (target == null) return false;
        float dist = Vector3.Distance(transform.position, target.position);
        return dist <= attackHandler.attackRange; // Kiểm tra khoảng cách tấn công
    }

    private void LookAtTarget(Transform target)
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

    private bool IsLookingAtTarget()
    {
        if (chasingTarget == null) return true;
        Vector3 direction = (chasingTarget.position - transform.position).normalized;
        direction.y = 0;
        if (direction == Vector3.zero) return true;
        float angle = Vector3.Angle(transform.forward, direction);
        return angle < 10f; // Góc nhỏ hơn 10 độ coi là đang nhìn vào target
    }

    public void ChangeState(AnimalState newState)
    {
        if (currentState == newState) return;
        currentState = newState;

        switch (newState)
        {
            case AnimalState.Idle:
                animationHandler.PlayIdleAnimation();
                movement.Stop();
                // Only set random timer if not recovering from hit
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
                    // ✅ After attack, go to idle instead of chase to prevent immediate re-attack
                    if (!isDead)
                    {
                        ChangeState(AnimalState.Idle);
                        stateTimer = Random.Range(1f, 2f); // Short cooldown after attack
                    }
                }));
                break;

            case AnimalState.Hit:
                movement.Stop();
                // ✅ Stop attack coroutine and reset canAttack when hit
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

    public void Damage(int amount)
    {
        Debug.Log("Hit");
        if (currentState == AnimalState.Dead) return;

        healthSystem.Damage(amount); // Trừ máu

        stateBeforeHit = currentState; // Lưu trạng thái trước khi hit
        ChangeState(AnimalState.Hit);
    }

    private IEnumerator HitRoutine(float duration)
    {
        yield return new WaitForSeconds(duration);

        if (!isDead)
        {
            // ✅ After getting hit, go directly to idle and set recovery flag
            Debug.Log("Hit animation finished, entering recovery period");
            isRecoveringFromHit = true;
            canAttack = true; // Reset attack ability when hit
            ChangeState(AnimalState.Idle);
            // Set recovery time - animal will stay idle during this period
            stateTimer = animationHandler.hitAnimationDuration;
        }
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;
        hurtBox.enabled = false;
        ChangeState(AnimalState.Dead);

        if (healthBarUI != null)
            healthBarUI.gameObject.SetActive(false);
        if (animationHandler != null)
            animationHandler.DisableAnimator();

        ActivateRagdoll(); // Kích hoạt ragdoll
    }

    public void ApplyKnockBack(Vector3 dir, float force)
    {
        if (!movement.agent.isOnNavMesh) return;
        movement.agent.isStopped = false;
        baseRigibody.AddForce(dir * force, ForceMode.Impulse); // Đẩy knockback

        if (!movement.agent.isOnNavMesh) return;
        movement.agent.isStopped = true;
        Debug.Log("push back");
    }

    private void ActivateRagdoll()
    {
        SetRagdollActive(true);

        if (movement != null) movement.enabled = false;
        if (animationHandler != null) animationHandler.enabled = false;

        Destroy(gameObject, ragdollDestroyDelay); // Hủy object sau vài giây
    }

    private void SetRagdollActive(bool active)
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

    [ContextMenu("Damage AI")]
    private void DamageAIButton()
    {
        Damage(debugDamageAmount); // Test damage từ inspector
    }
}