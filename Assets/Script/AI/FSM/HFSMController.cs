using System.Collections.Generic;
using UnityEngine;
using Mirror;
using System;
using System.Collections;

public class HFSMController : NetworkBehaviour, IDamageable
{
    // ==========================================================
    // 🔹 THAM CHIẾU & THÀNH PHẦN
    // ==========================================================
    [Header("Animator")]
    public HFSMAnimator animator;

    [Header("Animal Data")]
    public AnimalData animalData;

    [Header("Movement Component")]
    public CharacterController characterController;
    private Rigidbody rb;

    [Header("Ground Detection")]
    [SerializeField] private LayerMask walkableLayer;
    [SerializeField] private float maxSlope = 45f;
    [SerializeField] private float groundCheckDistance = 0.3f;
    [SerializeField] private float slopeForce = 8f;

    [Header("Movement Settings")]
    [SerializeField] private float acceleration = 8f;
    [SerializeField] private float deceleration = 10f;
    [SerializeField] private float turnSpeed = 5f;
    [SerializeField] private float stoppingDistance = 0.5f;
    [SerializeField] private float rotationThreshold = 45f;

    [Header("Gravity Settings")]
    [SerializeField] private float gravityForce = 20f;
    [SerializeField] private float terminalVelocity = 50f;

    [Header("Obstacle Avoidance")]
    [SerializeField] private bool enableObstacleAvoidance = true;
    [SerializeField] private float detectionDistance = 3f;
    [SerializeField] private float sideRayOffset = 0.5f;
    [SerializeField] private int numberOfRays = 5;
    [SerializeField] private float raySpreadAngle = 45f;
    [SerializeField] private float avoidanceForce = 2f;
    [SerializeField] private LayerMask obstacleLayer;
    [SerializeField] private bool debugObstacleAvoidance = true;

    // ==========================================================
    // 👁️ LINE OF SIGHT SYSTEM - HỆ THỐNG TẦM NHÌN
    // ==========================================================
    [Header("Line of Sight Detection")]
    [SerializeField] private bool enableLineOfSight = true;
    [SerializeField] private float losDistance = 15f; // Khoảng cách tầm nhìn
    [SerializeField] private float losAngle = 120f; // Góc nhìn (FOV)
    [SerializeField] private float losCheckInterval = 0.2f; // Kiểm tra LOS mỗi 0.2s
    [SerializeField] private LayerMask losObstacleLayer; // Layer che khuất tầm nhìn (tường, cây...)
    [SerializeField] private bool debugLOS = true;

    [Header("Target Ray System")]
    [SerializeField] private float targetRayMaxTime = 5f; // Thời gian tối đa để tìm target (5s)
    [SerializeField] private float targetRayDistance = 20f; // Khoảng cách của target ray
    [SerializeField] private bool debugTargetRay = true;

    // Cache danh sách IDetectable để tránh FindObjectsOfType mỗi frame
    private static IDetectable[] allDetectables;
    private static float lastDetectableRefresh = 0f;
    private const float DETECTABLE_REFRESH_INTERVAL = 1f; // Refresh danh sách mỗi 1 giây

    // Biến nội bộ cho Line of Sight
    private float losCheckTimer = 0f;
    private bool targetInLOS = false; // Có mục tiêu trong tầm nhìn không
    private float targetRayTimer = 0f; // Timer để đếm thời gian tracking target
    private bool isTrackingWithTargetRay = false; // Đang dùng target ray để track không

    // Biến di chuyển
    private Vector3 currentDestination;
    private bool hasDestination = false;
    private Vector3 currentVelocity;
    private float verticalVelocity;
    private bool isGrounded;
    private Vector3 avoidanceDirection = Vector3.zero;
    private bool isAvoidingObstacle = false;

    [Header("Ragdoll")]
    [SerializeField] private Transform ragdollRootBone;
    private AIRagdoll aIRagdoll;

    [Header("Loot Drop")]
    [SerializeField] private List<LootEntry> lootTable = new();
    [SerializeField] private float destroyDelay = 20f;
    [SerializeField] private Transform dropPoint;

    [Header("Thanh máu (UI)")]
    public HealthBarUI healthBarUI;
    [HideInInspector] public HealthSystem healthSystem;

    [Header("Trạng thái runtime")]
    public IDetectable currentTarget;
    public bool enemySpotted;
    public State CurrentState { get; private set; }

    [Header("AI Settings")]
    public bool isPredator = false;
    public bool isDead = false;
    public float moveSpeed = 3.5f;
    public float attackRange = 5f;
    public float facingAngleThreshold = 12f;

    private HashSet<string> allowedStates;

    [SyncVar(hook = nameof(OnHealthChanged))] private int syncedHealth;
    [SyncVar(hook = nameof(OnStateChanged))] private string syncedStateName;
    [SyncVar] public bool syncedIsDead = false;

    public string poolKey;
    public bool isSleeping = false;
    public bool pooled = true;
    [HideInInspector] public string uniqueId;
    private bool wasFirstSpawn = false;

    [Serializable]
    public class LootEntry
    {
        public ItemData item;
        [Min(1)] public int amount = 1;
        [Range(0f, 1f)] public float dropChance = 1f;
    }

    private void Awake() => InitializeComponents();

    private void InitializeComponents()
    {
        animator?.EnableAnimator();

        if (healthSystem == null || healthSystem.GetHealth() <= 0)
            healthSystem = new HealthSystem(animalData.maxHealth);

        aIRagdoll ??= GetComponent<AIRagdoll>();

        characterController ??= GetComponent<CharacterController>();
        if (characterController == null)
        {
            characterController = gameObject.AddComponent<CharacterController>();
            characterController.radius = 0.5f;
            characterController.height = 2f;
            characterController.center = Vector3.up;
            characterController.slopeLimit = maxSlope;
            characterController.stepOffset = 0.3f;
            characterController.skinWidth = 0.08f;
        }

        rb ??= GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        aIRagdoll?.SetRagdoll(false);
        allowedStates ??= new HashSet<string>(animalData.allowedStates);
        currentVelocity = Vector3.zero;
        verticalVelocity = 0f;
    }

    // ==========================================================
    // 👁️ LINE OF SIGHT DETECTION SYSTEM
    // ==========================================================

    /// <summary>
    /// ✅ QUÉT TẦM NHÌN ĐỂ TÌM MỤC TIÊU (LOS RAY)
    /// Được gọi định kỳ để tìm mục tiêu trong tầm nhìn
    /// </summary>
    public void ScanForTargets()
    {
        if (!enableLineOfSight) return;

        // Giảm tần suất kiểm tra để tối ưu hiệu năng
        losCheckTimer -= Time.deltaTime;
        if (losCheckTimer > 0f) return;

        losCheckTimer = losCheckInterval;

        // Nếu đang tracking với target ray, không cần scan LOS nữa
        if (isTrackingWithTargetRay) return;

        // ✅ LẤY DANH SÁCH TẤT CẢ IDETECTABLE (CACHE)
        RefreshDetectableList();

        if (allDetectables == null || allDetectables.Length == 0)
        {
            targetInLOS = false;
            currentTarget = null;
            enemySpotted = false;
            return;
        }

        // Xác định loại mục tiêu cần tìm
        TargetCategory[] detectCategories = isPredator
            ? new[] { TargetCategory.Player, TargetCategory.Enemy }
            : new[] { TargetCategory.Player };

        IDetectable bestTarget = null;
        float closestDistance = float.MaxValue;

        // ✅ KIỂM TRA TỪNG IDETECTABLE
        foreach (IDetectable detectable in allDetectables)
        {
            if (detectable == null) continue;

            MonoBehaviour detectableMono = detectable as MonoBehaviour;
            if (detectableMono == null || !detectableMono.gameObject.activeInHierarchy) continue;

            // Kiểm tra xem mục tiêu có thuộc loại cần tìm không
            bool isValidCategory = false;
            foreach (var category in detectCategories)
            {
                if (detectable.Category == category)
                {
                    isValidCategory = true;
                    break;
                }
            }

            if (!isValidCategory) continue;

            // Lấy transform của mục tiêu
            Transform targetTransform = detectableMono.transform;
            Vector3 targetPosition = targetTransform.position;

            // ✅ KIỂM TRA KHOẢNG CÁCH
            float distanceToTarget = Vector3.Distance(transform.position, targetPosition);

            if (distanceToTarget > losDistance) continue; // Quá xa

            // ✅ KIỂM TRA GÓC NHÌN (FOV)
            Vector3 directionToTarget = (targetPosition - transform.position).normalized;
            float angleToTarget = Vector3.Angle(transform.forward, directionToTarget);

            if (angleToTarget > losAngle / 2f) continue; // Ngoài góc nhìn

            // ✅ KIỂM TRA LOS RAY - Có vật cản giữa AI và mục tiêu không?
            Vector3 rayOrigin = transform.position + Vector3.up * 1f; // Ray từ mắt AI
            Vector3 rayTarget = targetPosition + Vector3.up * 1f; // Ray đến mục tiêu
            Vector3 rayDirection = rayTarget - rayOrigin;

            if (debugLOS)
            {
                // Vẽ ray để debug
                Debug.DrawRay(rayOrigin, rayDirection, Color.yellow, losCheckInterval);
            }

            // Bắn ray để kiểm tra vật cản
            if (Physics.Raycast(rayOrigin, rayDirection.normalized, out RaycastHit hit, rayDirection.magnitude, losObstacleLayer))
            {
                // Có vật cản → Không thấy mục tiêu này
                if (debugLOS)
                {
                    Debug.DrawLine(rayOrigin, hit.point, Color.red, losCheckInterval);
                }
                continue;
            }

            // ✅ KHÔNG CÓ VẬT CẢN → THẤY MỤC TIÊU!
            if (debugLOS)
            {
                Debug.DrawRay(rayOrigin, rayDirection, Color.green, losCheckInterval);
            }

            // Chọn mục tiêu gần nhất
            if (distanceToTarget < closestDistance)
            {
                closestDistance = distanceToTarget;
                bestTarget = detectable;
            }
        }

        // Cập nhật trạng thái phát hiện mục tiêu
        if (bestTarget != null)
        {
            // ✅ TÌM THẤY MỤC TIÊU TRONG TẦM NHÌN
            if (currentTarget != bestTarget)
            {
                currentTarget = bestTarget;
                Debug.Log($"{name} phát hiện mục tiêu: {(bestTarget as MonoBehaviour)?.name}");
            }

            targetInLOS = true;
            enemySpotted = true;

            // ✅ BẮT ĐẦU SỬ DỤNG TARGET RAY ĐỂ THEO DÕI
            StartTargetRayTracking();
        }
        else
        {
            // Không tìm thấy mục tiêu nào
            targetInLOS = false;

            // Nếu không có target ray tracking, reset mục tiêu
            if (!isTrackingWithTargetRay)
            {
                if (currentTarget != null)
                {
                    Debug.Log($"{name} mất mục tiêu khỏi tầm nhìn");
                }
                currentTarget = null;
                enemySpotted = false;
            }
        }
    }

    /// <summary>
    /// ✅ LẤY VÀ CACHE DANH SÁCH TẤT CẢ IDETECTABLE
    /// </summary>
    private void RefreshDetectableList()
    {
        // Chỉ refresh mỗi 1 giây để tối ưu
        if (Time.time - lastDetectableRefresh < DETECTABLE_REFRESH_INTERVAL)
        {
            return;
        }

        lastDetectableRefresh = Time.time;

        // ✅ TÌM TẤT CẢ OBJECT CÓ COMPONENT IDETECTABLE
        // Lưu ý: FindObjectsOfType tốn hiệu năng nên chỉ gọi định kỳ
        MonoBehaviour[] allMonos = FindObjectsOfType<MonoBehaviour>();
        System.Collections.Generic.List<IDetectable> detectableList = new System.Collections.Generic.List<IDetectable>();

        foreach (MonoBehaviour mono in allMonos)
        {
            if (mono is IDetectable detectable)
            {
                detectableList.Add(detectable);
            }
        }

        allDetectables = detectableList.ToArray();
    }

    // ==========================================================
    // 🎯 TARGET RAY TRACKING SYSTEM
    // ==========================================================

    /// <summary>
    /// ✅ BẮT ĐẦU TRACKING VỚI TARGET RAY
    /// Được gọi khi LOS ray tìm thấy mục tiêu
    /// </summary>
    private void StartTargetRayTracking()
    {
        if (!isTrackingWithTargetRay)
        {
            isTrackingWithTargetRay = true;
            targetRayTimer = 0f;
           
            Debug.Log($"{name} bắt đầu tracking với Target Ray");
        }
    }

    /// <summary>
    /// ✅ CẬP NHẬT TARGET RAY (được gọi mỗi frame khi đang tracking)
    /// Bắn ray thẳng về phía mục tiêu hiện tại
    /// Nếu không trúng trong 5 giây → Mất mục tiêu
    /// </summary>
    private void UpdateTargetRay()
    {
        if (!isTrackingWithTargetRay || currentTarget == null) return;

        // Tăng timer
        targetRayTimer += Time.deltaTime;

        // Kiểm tra timeout (5 giây)
        if (targetRayTimer >= targetRayMaxTime)
        {
            // ✅ TIMEOUT → MẤT MỤC TIÊU
            Debug.Log($"{name} mất target sau {targetRayMaxTime}s - Quay về Normal State");
            LoseTarget();
            return;
        }

        // ✅ LẤY VỊ TRÍ MỤC TIÊU HIỆN TẠI
        MonoBehaviour targetMono = currentTarget as MonoBehaviour;
        if (targetMono == null || !targetMono.gameObject.activeInHierarchy)
        {
            // Mục tiêu bị destroy hoặc disabled
            LoseTarget();
            return;
        }

        Vector3 targetPosition = targetMono.transform.position + Vector3.up * 1f;
        Vector3 rayOrigin = transform.position + Vector3.up * 1f;
        Vector3 rayDirection = (targetPosition - rayOrigin).normalized;
        float distanceToTarget = Vector3.Distance(rayOrigin, targetPosition);

        // Kiểm tra khoảng cách
        if (distanceToTarget > targetRayDistance)
        {
            // Mục tiêu quá xa
            if (debugTargetRay)
            {
                Debug.DrawLine(rayOrigin, targetPosition, Color.yellow);
            }
            return;
        }

        if (debugTargetRay)
        {
            // Vẽ target ray (màu cyan)
            Debug.DrawRay(rayOrigin, rayDirection * distanceToTarget, Color.cyan);
        }

        // ✅ BẮN RAY THẲNG VỀ PHÍA MỤC TIÊU
        if (Physics.Raycast(rayOrigin, rayDirection, out RaycastHit hit, distanceToTarget, losObstacleLayer))
        {
            // ✅ CÓ VẬT CẢN GIỮA AI VÀ MỤC TIÊU
            if (debugTargetRay)
            {
                Debug.DrawLine(rayOrigin, hit.point, Color.red);
            }

            // Không reset timer - để timer tăng dần đến timeout
            return;
        }

        // ✅ KHÔNG CÓ VẬT CẢN → TRÚNG MỤC TIÊU
        if (debugTargetRay)
        {
            Debug.DrawLine(rayOrigin, targetPosition, Color.green);
        }

        // Reset timer vì đã thấy mục tiêu
        targetRayTimer = 0f;
        enemySpotted = true;
    }

    /// <summary>
    /// ✅ MẤT MỤC TIÊU - Dừng tracking và reset
    /// </summary>
    private void LoseTarget()
    {
        isTrackingWithTargetRay = false;
        targetInLOS = false;
        currentTarget = null;
        enemySpotted = false;
        targetRayTimer = 0f;
    }

    /// <summary>
    /// ✅ DỪNG TARGET RAY TRACKING (gọi khi không cần nữa)
    /// </summary>
    public void StopTargetRayTracking()
    {
        isTrackingWithTargetRay = false;
        targetRayTimer = 0f;
    }

    // ==========================================================
    //  🚀 CHARACTER CONTROLLER MOVEMENT
    // ==========================================================

    public void SetDestination(Vector3 destination)
    {
        currentDestination = destination;
        hasDestination = true;
    }

    public void StopMovement()
    {
        hasDestination = false;
        currentVelocity = Vector3.zero;
    }

    public bool IsMoving()
    {
        if (!hasDestination) return false;
        float distToDestination = Vector3.Distance(transform.position, currentDestination);
        return distToDestination > stoppingDistance;
    }

    public float GetRemainingDistance()
    {
        if (!hasDestination) return 0f;
        return Vector3.Distance(transform.position, currentDestination);
    }

    // ==========================================================
    //  🛡️ OBSTACLE AVOIDANCE SYSTEM
    // ==========================================================

    private Vector3 CalculateObstacleAvoidance(Vector3 desiredDirection)
    {
        if (!enableObstacleAvoidance) return desiredDirection;

        Vector3 rayOrigin = transform.position + Vector3.up * 0.5f;

        // ✅ ĐẾM SỐ RAY TRÚNG Ở BÊN TRÁI VÀ PHẢI
        int leftHitCount = 0;
        int rightHitCount = 0;
        int centerHitCount = 0;

        float leftClearance = detectionDistance; // Khoảng cách trống bên trái
        float rightClearance = detectionDistance; // Khoảng cách trống bên phải

        bool centerBlocked = false;

        // ✅ KIỂM TRA RAY GIỮA (CENTER)
        RaycastHit centerHit;
        if (Physics.Raycast(rayOrigin, desiredDirection, out centerHit, detectionDistance, obstacleLayer))
        {
            centerBlocked = true;
            centerHitCount++;

            if (debugObstacleAvoidance)
            {
                Debug.DrawRay(rayOrigin, desiredDirection * centerHit.distance, Color.red);
            }
        }
        else if (debugObstacleAvoidance)
        {
            Debug.DrawRay(rayOrigin, desiredDirection * detectionDistance, Color.green);
        }

        // ✅ QUÉT CÁC RAY XUNG QUANH ĐỂ PHÁT HIỆN VẬT CẢN
        for (int i = 0; i < numberOfRays; i++)
        {
            // Tính góc từ -raySpreadAngle đến +raySpreadAngle
            float angle = Mathf.Lerp(-raySpreadAngle, raySpreadAngle, i / (float)(numberOfRays - 1));
            Vector3 rayDirection = Quaternion.Euler(0, angle, 0) * desiredDirection;

            RaycastHit hit;
            if (Physics.Raycast(rayOrigin, rayDirection, out hit, detectionDistance, obstacleLayer))
            {
                // Ray trúng vật cản
                if (angle < -5f) // Bên trái (góc âm)
                {
                    leftHitCount++;
                    leftClearance = Mathf.Min(leftClearance, hit.distance);
                }
                else if (angle > 5f) // Bên phải (góc dương)
                {
                    rightHitCount++;
                    rightClearance = Mathf.Min(rightClearance, hit.distance);
                }
                else // Gần giữa
                {
                    centerHitCount++;
                }

                if (debugObstacleAvoidance)
                {
                    Debug.DrawRay(rayOrigin, rayDirection * hit.distance, Color.red);
                }
            }
            else
            {
                // Ray không trúng → Cập nhật khoảng cách trống
                if (angle < -5f)
                {
                    leftClearance = detectionDistance;
                }
                else if (angle > 5f)
                {
                    rightClearance = detectionDistance;
                }

                if (debugObstacleAvoidance)
                {
                    Debug.DrawRay(rayOrigin, rayDirection * detectionDistance, Color.cyan);
                }
            }
        }

        // ✅ KIỂM TRA RAY BÊN CẠNH (SIDE RAYS)
        Vector3 leftDir = Quaternion.Euler(0, -90, 0) * desiredDirection;
        Vector3 rightDir = Quaternion.Euler(0, 90, 0) * desiredDirection;

        RaycastHit leftSideHit, rightSideHit;
        bool leftSideBlocked = Physics.Raycast(rayOrigin + leftDir * sideRayOffset, desiredDirection, out leftSideHit, detectionDistance, obstacleLayer);
        bool rightSideBlocked = Physics.Raycast(rayOrigin + rightDir * sideRayOffset, desiredDirection, out rightSideHit, detectionDistance, obstacleLayer);

        if (leftSideBlocked)
        {
            leftHitCount += 2; // Tăng trọng số cho side ray
            leftClearance = Mathf.Min(leftClearance, leftSideHit.distance);
            if (debugObstacleAvoidance)
                Debug.DrawRay(rayOrigin + leftDir * sideRayOffset, desiredDirection * leftSideHit.distance, Color.magenta);
        }

        if (rightSideBlocked)
        {
            rightHitCount += 2; // Tăng trọng số cho side ray
            rightClearance = Mathf.Min(rightClearance, rightSideHit.distance);
            if (debugObstacleAvoidance)
                Debug.DrawRay(rayOrigin + rightDir * sideRayOffset, desiredDirection * rightSideHit.distance, Color.magenta);
        }

        // ✅ TÍNH TOÁN HƯỚNG TỐI ƯU DỰA TRÊN SỐ LƯỢNG VA CHẠM
        bool hasObstacle = centerBlocked || leftHitCount > 0 || rightHitCount > 0;
        isAvoidingObstacle = hasObstacle;

        if (!hasObstacle)
        {
            // Không có vật cản → Đi thẳng
            avoidanceDirection = Vector3.zero;
            return desiredDirection;
        }

        // ✅ CÓ VẬT CẢN → XÁC ĐỊNH HƯỚNG ĐI
        Vector3 finalDirection;

        if (centerBlocked)
        {
            // ✅ TÂM BỊ CHẶN → RẼ SANG BÊN CÓ ÍT VẬT CẢN HƠN
            if (leftHitCount < rightHitCount)
            {
                // Bên trái thoáng hơn → Rẽ trái
                float turnAngle = -45f; // Rẽ trái 45 độ
                finalDirection = Quaternion.Euler(0, turnAngle, 0) * desiredDirection;

                if (debugObstacleAvoidance)
                {
                    Debug.DrawRay(rayOrigin, finalDirection * 2f, Color.yellow, 0.1f);
                }
            }
            else if (rightHitCount < leftHitCount)
            {
                // Bên phải thoáng hơn → Rẽ phải
                float turnAngle = 45f; // Rẽ phải 45 độ
                finalDirection = Quaternion.Euler(0, turnAngle, 0) * desiredDirection;

                if (debugObstacleAvoidance)
                {
                    Debug.DrawRay(rayOrigin, finalDirection * 2f, Color.yellow, 0.1f);
                }
            }
            else
            {
                // Hai bên bằng nhau → So sánh khoảng cách trống
                if (leftClearance > rightClearance)
                {
                    // Trái có khoảng trống xa hơn
                    finalDirection = Quaternion.Euler(0, -45f, 0) * desiredDirection;
                }
                else
                {
                    // Phải có khoảng trống xa hơn
                    finalDirection = Quaternion.Euler(0, 45f, 0) * desiredDirection;
                }
            }
        }
        else
        {
            // ✅ CHỈ BÊN CẠNH BỊ CHẶN → ĐI DỌC TƯỜNG
            if (leftHitCount > rightHitCount)
            {
                // Bên trái nhiều vật cản → Rẽ phải nhẹ
                finalDirection = Quaternion.Euler(0, 30f, 0) * desiredDirection;
            }
            else if (rightHitCount > leftHitCount)
            {
                // Bên phải nhiều vật cản → Rẽ trái nhẹ
                finalDirection = Quaternion.Euler(0, -30f, 0) * desiredDirection;
            }
            else
            {
                // Bằng nhau → Giữ nguyên hướng
                finalDirection = desiredDirection;
            }
        }

        finalDirection.y = 0;
        finalDirection.Normalize();
        avoidanceDirection = finalDirection;

        return finalDirection;
    }

    private Vector3 FindBestAvoidanceDirection(Vector3 desiredDirection)
    {
        float[] testAngles = { 0, 30, -30, 60, -60, 90, -90, 120, -120 };
        float bestScore = float.MinValue;
        Vector3 bestDirection = desiredDirection;

        foreach (float angle in testAngles)
        {
            Vector3 testDir = Quaternion.Euler(0, angle, 0) * desiredDirection;
            Vector3 rayOrigin = transform.position + Vector3.up * 0.5f;

            if (!Physics.Raycast(rayOrigin, testDir, detectionDistance, obstacleLayer))
            {
                float score = Vector3.Dot(testDir, desiredDirection);
                score -= Mathf.Abs(angle) * 0.01f;

                if (score > bestScore && CanWalkInDirection(testDir))
                {
                    bestScore = score;
                    bestDirection = testDir;
                }
            }
        }

        return bestDirection;
    }

    public bool IsObstacleAhead()
    {
        Vector3 rayOrigin = transform.position + Vector3.up * 0.5f;
        return Physics.Raycast(rayOrigin, transform.forward, detectionDistance, obstacleLayer);
    }

    // ==========================================================
    //  🚀 MOVEMENT WITH OBSTACLE AVOIDANCE
    // ==========================================================

    public void MoveToTargetDestination(float speed)
    {
        if (!hasDestination || characterController == null || isDead) return;

        Vector3 currentPos = transform.position;
        float distToDestination = Vector3.Distance(currentPos, currentDestination);

        if (distToDestination <= stoppingDistance)
        {
            StopMovement();
            return;
        }

        Vector3 direction = (currentDestination - currentPos).normalized;
        direction.y = 0;

        if (direction == Vector3.zero) return;

        if (!CanWalkInDirection(direction))
        {
            StopMovement();
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Lerp(
            transform.rotation,
            targetRotation,
            turnSpeed * Time.fixedDeltaTime
        );

        float angleToTarget = Vector3.Angle(transform.forward, direction);

        if (angleToTarget < rotationThreshold)
        {
            Vector3 targetVelocity = direction * speed;
            currentVelocity = Vector3.Lerp(
                currentVelocity,
                targetVelocity,
                acceleration * Time.fixedDeltaTime
            );
        }
        else
        {
            currentVelocity = Vector3.Lerp(
                currentVelocity,
                Vector3.zero,
                deceleration * Time.fixedDeltaTime
            );
        }
    }

    public void MoveToTargetDestinationWithAvoidance(float speed)
    {
        if (!hasDestination || characterController == null || isDead) return;

        Vector3 currentPos = transform.position;
        float distToDestination = Vector3.Distance(currentPos, currentDestination);

        if (distToDestination <= stoppingDistance)
        {
            StopMovement();
            return;
        }

        Vector3 desiredDirection = (currentDestination - currentPos).normalized;
        desiredDirection.y = 0;

        if (desiredDirection == Vector3.zero) return;

        Vector3 finalDirection = CalculateObstacleAvoidance(desiredDirection);

        if (!CanWalkInDirection(finalDirection))
        {
            finalDirection = FindBestAvoidanceDirection(desiredDirection);

            if (!CanWalkInDirection(finalDirection))
            {
                StopMovement();
                return;
            }
        }

        Quaternion targetRotation = Quaternion.LookRotation(finalDirection);
        transform.rotation = Quaternion.Lerp(
            transform.rotation,
            targetRotation,
            turnSpeed * Time.fixedDeltaTime
        );

        float angleToTarget = Vector3.Angle(transform.forward, finalDirection);

        if (angleToTarget < rotationThreshold)
        {
            float speedMultiplier = isAvoidingObstacle ? 0.7f : 1f;
            Vector3 targetVelocity = finalDirection * speed * speedMultiplier;

            currentVelocity = Vector3.Lerp(
                currentVelocity,
                targetVelocity,
                acceleration * Time.fixedDeltaTime
            );
        }
        else
        {
            currentVelocity = Vector3.Lerp(
                currentVelocity,
                Vector3.zero,
                deceleration * Time.fixedDeltaTime
            );
        }
    }

    private bool CanWalkInDirection(Vector3 direction)
    {
        Vector3 rayStart = transform.position + Vector3.up * 0.5f + direction * 0.5f;

        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, groundCheckDistance + 1f, walkableLayer))
        {
            float slope = Vector3.Angle(hit.normal, Vector3.up);
            return slope <= maxSlope;
        }

        return false;
    }

    private void ApplyGravityAndGroundCheck()
    {
        isGrounded = characterController.isGrounded;

        if (isGrounded && verticalVelocity < 0)
        {
            verticalVelocity = -2f;

            if (IsOnSlope())
            {
                verticalVelocity -= slopeForce;
            }
        }
        else
        {
            verticalVelocity -= gravityForce * Time.fixedDeltaTime;

            if (verticalVelocity < -terminalVelocity)
            {
                verticalVelocity = -terminalVelocity;
            }
        }
    }

    private bool IsOnSlope()
    {
        if (!isGrounded) return false;

        Vector3 rayStart = transform.position + Vector3.up * 0.1f;

        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, groundCheckDistance, walkableLayer))
        {
            float angle = Vector3.Angle(hit.normal, Vector3.up);
            return angle > 0f && angle <= maxSlope;
        }

        return false;
    }

    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;

        // Vẽ đích đến
        if (hasDestination)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(currentDestination, 0.5f);
            Gizmos.DrawLine(transform.position, currentDestination);
        }

        // Vẽ hướng di chuyển
        if (currentVelocity.magnitude > 0.1f)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(transform.position, currentVelocity.normalized * 2f);
        }

        // Vẽ avoidance direction
        if (isAvoidingObstacle && avoidanceDirection != Vector3.zero)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(transform.position, avoidanceDirection * 2f);
        }

        // Vẽ ground check
        Vector3 forward = transform.forward;
        Vector3 rayStart = transform.position + Vector3.up * 0.5f + forward * 0.5f;

        Gizmos.color = CanWalkInDirection(forward) ? Color.green : Color.red;
        Gizmos.DrawRay(rayStart, Vector3.down * (groundCheckDistance + 1f));

        // Vẽ grounded status
        Gizmos.color = isGrounded ? Color.cyan : Color.yellow;
        Gizmos.DrawRay(transform.position, Vector3.down * groundCheckDistance);

        // Vẽ CharacterController bounds
        if (characterController != null)
        {
            Gizmos.color = Color.magenta;
            Vector3 center = transform.position + characterController.center;
            Gizmos.DrawWireSphere(center, characterController.radius);
        }

        // ✅ VẼ LINE OF SIGHT (FOV CONE)
        if (enableLineOfSight)
        {
            Gizmos.color = targetInLOS ? Color.green : Color.white;

            // Vẽ vòng tròn tầm nhìn
            Gizmos.DrawWireSphere(transform.position, losDistance);

            // Vẽ góc nhìn (FOV)
            Vector3 leftBoundary = Quaternion.Euler(0, -losAngle / 2f, 0) * transform.forward * losDistance;
            Vector3 rightBoundary = Quaternion.Euler(0, losAngle / 2f, 0) * transform.forward * losDistance;

            Gizmos.DrawLine(transform.position, transform.position + leftBoundary);
            Gizmos.DrawLine(transform.position, transform.position + rightBoundary);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * losDistance);
        }

        // ✅ VẼ TARGET RAY (nếu đang tracking)
        if (isTrackingWithTargetRay && currentTarget != null)
        {
            MonoBehaviour targetMono = currentTarget as MonoBehaviour;
            if (targetMono != null && targetMono.gameObject.activeInHierarchy)
            {
                Vector3 rayOrigin = transform.position + Vector3.up * 1f;
                Vector3 targetPos = targetMono.transform.position + Vector3.up * 1f;
                Vector3 direction = (targetPos - rayOrigin).normalized;
                float distance = Vector3.Distance(rayOrigin, targetPos);

                Gizmos.color = Color.cyan;
                Gizmos.DrawRay(rayOrigin, direction * Mathf.Min(distance, targetRayDistance));
                Gizmos.DrawWireSphere(targetPos, 0.3f);
            }
        }
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        if (string.IsNullOrEmpty(uniqueId))
        {
            uniqueId = System.Guid.NewGuid().ToString();
            wasFirstSpawn = true;
        }

        healthSystem.OnDead += OnHealthSystemDead;

        if (healthBarUI != null)
            healthBarUI.SetHealthSystem(healthSystem);

        syncedHealth = animalData.maxHealth;
    }

    private void OnHealthSystemDead()
    {
        Die();
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

    private void Start()
    {
        if (!isServer) return;

        if (CanRunState(typeof(NormalState)))
            ChangeState(new NormalState(this));
    }

    private void FixedUpdate()
    {
        if (isDead || characterController == null) return;

        // ✅ CẬP NHẬT TARGET RAY (nếu đang tracking)
        if (isTrackingWithTargetRay)
        {
            UpdateTargetRay();
        }

        // ✅ ÁP DỤNG GRAVITY
        ApplyGravityAndGroundCheck();

        // ✅ KẾT HỢP VELOCITY
        Vector3 motion = currentVelocity + Vector3.up * verticalVelocity;

        // ✅ DI CHUYỂN BẰNG CHARACTER CONTROLLER
        if (characterController != null && characterController.enabled && characterController.gameObject.activeInHierarchy)
        {
            characterController.Move(motion * Time.fixedDeltaTime);
        }

        // ✅ GIỚI HẠN VẬN TỐC
        if (currentVelocity.magnitude > moveSpeed * 1.5f)
        {
            currentVelocity = currentVelocity.normalized * (moveSpeed * 1.5f);
        }
    }

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

    public void Damage(int amount, HitInfo hit)
    {
        if (!isServer) return;

        if (aIRagdoll != null && hit.point != Vector3.zero)
        {
            float multiplier = aIRagdoll.GetDamageMultiplier(hit.point, 1f);
            string bodyPartName = aIRagdoll.GetHitBodyPartName(hit.point, 1f);
            int modifiedDamage = Mathf.RoundToInt(amount * multiplier);
            amount = modifiedDamage;
        }

        healthSystem.Damage(amount);
        syncedHealth = healthSystem.GetHealth();

        if (healthSystem.GetHealth() <= 0)
        {
            Die(hit);
            return;
        }

        RpcPlayHitAnimation();

        if (CurrentState is CombatState combat)
            combat.SetSubState(new RecoveryState(this, 2f));
        else
            ChangeState(new RecoveryState(this, 2f));
    }

    private void DropLoot()
    {
        if (!isServer || lootTable == null || lootTable.Count == 0)
            return;

        foreach (var loot in lootTable)
        {
            if (loot.item == null) continue;
            if (UnityEngine.Random.value > loot.dropChance) continue;

            for (int i = 0; i < loot.amount; i++)
            {
                Vector3 pos = dropPoint != null
                    ? dropPoint.position
                    : transform.position + Vector3.up * 0.5f;

                GameObject lootObj = Instantiate(
                    loot.item.worldPrefab,
                    pos + UnityEngine.Random.insideUnitSphere * 0.2f,
                    Quaternion.identity
                );

                if (lootObj.TryGetComponent<Rigidbody>(out var rigidBody))
                {
                    rigidBody.AddForce(UnityEngine.Random.insideUnitSphere * 2f, ForceMode.Impulse);
                }

                NetworkServer.Spawn(lootObj);
            }
        }
    }

    protected virtual void Die(HitInfo? hit = null)
    {
        if (!isServer) return;
        if (isDead) return;

        isDead = true;
        syncedIsDead = true;

        StopMovement();
        StopTargetRayTracking(); // ✅ Dừng tracking khi chết

        if (characterController != null)
        {
            characterController.enabled = false;
        }

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
        }

        CurrentState = null;

        if (hit.HasValue)
            ApplyDeathKnockback(hit.Value);

        RpcDie();
        StartCoroutine(RemoveAfterDelay());
    }

    private IEnumerator RemoveAfterDelay()
    {
        yield return new WaitForSeconds(destroyDelay);
        DropLoot();
        if (pooled)
        {
            Sleep();
        }
        else
        {
            NetworkServer.Destroy(gameObject);
        }
    }

    private void ApplyDeathKnockback(HitInfo hit)
    {
        float horizontalForce = 10f;
        float searchRadius = 1f;

        if (hit.itemData != null)
        {
            KnockbackSettings kb = hit.itemData.GetKnockbackSettings();
            horizontalForce = kb.horizontalForce;
            searchRadius = kb.boneSearchRadius;
        }

        Rigidbody hitBodyPart = aIRagdoll?.GetClosestBodyPart(hit.point, searchRadius);

        if (hitBodyPart != null)
        {
            RpcApplyDeathKnockback(hit.point, hit.direction, horizontalForce, searchRadius);
        }
    }

    public bool CanTriggerHitStop() => true;
    public bool IsDead() => syncedIsDead;

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
    }

    public void HandleClientDeath()
    {
        healthBarUI?.gameObject.SetActive(false);
        animator?.DisableAnimatorForRagdoll();
        StopMovement();

        if (characterController != null)
            characterController.enabled = false;

        aIRagdoll?.SetRagdoll(true);
    }

    [ClientRpc]
    private void RpcPlayHitAnimation()
        => animator?.PlayHitAnimation();

    [ClientRpc]
    private void RpcDie() => HandleClientDeath();

    [ClientRpc]
    public void RpcApplyDeathKnockback(Vector3 hitPoint, Vector3 hitDirection, float horizontal, float radius)
    {
        if (rb != null)
        {
            Vector3 forceDirection = hitDirection.normalized;
            Vector3 horizontalForce = new Vector3(forceDirection.x, 0, forceDirection.z).normalized * horizontal;
            Vector3 totalForce = horizontalForce + Vector3.up * (horizontal * 0.3f);

            rb.AddForce(totalForce, ForceMode.Impulse);
        }
    }

    public void Sleep()
    {
        if (isSleeping) return;

        isSleeping = true;
        StopMovement();
        StopTargetRayTracking(); // ✅ Dừng tracking khi sleep

        if (isServer)
        {
            CurrentState?.OnExit();
            CurrentState = null;
        }

        currentTarget = null;
        enemySpotted = false;

        currentVelocity = Vector3.zero;
        verticalVelocity = 0f;

        if (characterController != null)
            characterController.enabled = false;

        animator?.DisableAnimator();
        enabled = false;
    }

    public void WakeUp()
    {
        if (!isSleeping) return;
        isSleeping = false;

        InitializeComponents();
        enabled = true;

        if (characterController != null)
            characterController.enabled = true;

        currentVelocity = Vector3.zero;
        verticalVelocity = 0f;

        animator?.EnableAnimator();
        StartCoroutine(WakeUpDelayed());
    }

    private System.Collections.IEnumerator WakeUpDelayed()
    {
        yield return null;

        if (isServer)
        {
            CurrentState?.OnExit();
            CurrentState = null;
            if (CanRunState(typeof(NormalState)))
                ChangeState(new NormalState(this));
        }
    }

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

    public void OnRespawn()
    {
        if (!gameObject.activeSelf) return;

        InitializeComponents();
        pooled = false;
        aIRagdoll?.SetRagdoll(false);
        animator?.EnableAnimator();

        if (characterController != null)
            characterController.enabled = true;

        if (rb != null)
        {
            rb.isKinematic = true;
        }

        currentVelocity = Vector3.zero;
        verticalVelocity = 0f;

        StopMovement();
        StopTargetRayTracking(); // ✅ Reset tracking
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

    public Vector3 GetVelocity() => currentVelocity;
    public bool IsGrounded() => isGrounded;
}