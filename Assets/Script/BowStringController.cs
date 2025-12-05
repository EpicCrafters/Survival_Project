using UnityEngine;

public class BowStringController : MonoBehaviour
{
    [Header("Projectile Data")]
    [Tooltip("Defines projectile stats, damage ranges, and prefabs")]
    public ProjectileData projectileData;

    [Header("String Bones (Assign in Inspector)")]
    [Tooltip("Top attachment point of the string")]
    public Transform stringTopBone;

    [Tooltip("Middle bone - arrow attaches here, RIGHT HAND IK target")]
    public Transform stringMiddleBone;

    [Tooltip("Bottom attachment point of the string")]
    public Transform stringBottomBone;

    [Header("Draw Settings")]
    [Tooltip("How far back the string can be pulled (in local space)")]
    public float maxDrawDistance = 0.3f;

    [Tooltip("Speed of string snap back animation")]
    public float releaseSpeed = 10f;

    [Header("Arrow Spawn")]
    public Transform arrowSpawnPoint;
    public Transform arrowVisualPoint;

    [Header("Right Hand IK")]
    [Tooltip("Transform for RIGHT hand to follow string (auto-created as child of stringMiddleBone)")]
    public Transform rightHandIKTarget;
    public Transform rightHintPosition;

    [Header("Realistic Aiming")]
    [Tooltip("Camera used for aiming calculation")]
    public Camera aimCamera;

    [Tooltip("Max distance to raycast for aim target")]
    public float maxAimDistance = 200f;

    [Tooltip("Layer mask for aiming raycast")]
    public LayerMask aimLayerMask = ~0;

    [Tooltip("Reference to player transform to ignore player colliders")]
    public Transform playerRoot;

    [Header("Aim Constraints")]
    [Tooltip("Minimum distance before arrow spawn - prevents shooting backwards")]
    public float minAimDistance = 2f;

    [Tooltip("Maximum angle deviation from camera forward (prevents extreme angles)")]
    public float maxAimAngleDeviation = 85f;

    [Tooltip("Show debug rays for aiming")]
    public bool showAimDebug = true;

    // Private state
    private Vector3 middleBoneRestPosition;
    private Vector3 middleBoneRestLocalPosition;
    private GameObject currentArrowVisual;
    public bool isDrawing = false;
    public bool isReleasing = false;
    private float currentDrawAmount = 0f;

    private void Awake()
    {
        // Lưu vị trí ban đầu của xương giữa
        if (stringMiddleBone != null)
        {
            middleBoneRestPosition = stringMiddleBone.position;
            middleBoneRestLocalPosition = stringMiddleBone.localPosition;
        }

        // Tự tìm camera nếu chưa gán
        if (aimCamera == null)
            aimCamera = Camera.main;

        // Tự tìm player root nếu chưa gán
        if (playerRoot == null)
        {
            playerRoot = GetComponentInParent<Player>()?.transform;
            if (playerRoot == null)
            {
                // Try to find by tag if Player component not found
                GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj != null)
                    playerRoot = playerObj.transform;
            }

            if (playerRoot != null && showAimDebug)
                Debug.Log($"[BowString] Auto-found player root: {playerRoot.name}");
        }
    }

    // Bắt đầu kéo cung
    public void StartDrawing()
    {
        if (isDrawing) return;

        isDrawing = true;
        isReleasing = false;
        currentDrawAmount = 0f;

        // Spawn mũi tên visual (mô phỏng khi kéo dây cung)
        if (projectileData != null && projectileData.visualPrefab != null && arrowVisualPoint != null)
        {
            currentArrowVisual = Instantiate(projectileData.visualPrefab, arrowVisualPoint);
            currentArrowVisual.transform.localPosition = Vector3.zero;
            currentArrowVisual.transform.localRotation = Quaternion.identity;
        }
        else
        {
            Debug.LogWarning("[BowString] Không có Projectile Data hoặc Visual Prefab!");
        }
    }

    // Update % kéo cung (0 → 1)
    public void UpdateDrawAmount(float drawPercent)
    {
        if (!isDrawing || stringMiddleBone == null) return;

        currentDrawAmount = Mathf.Clamp01(drawPercent);

        // Kéo xương giữa dọc theo -Y local
        Vector3 pullOffset = -Vector3.up * (maxDrawDistance * currentDrawAmount);
        stringMiddleBone.localPosition = middleBoneRestLocalPosition + pullOffset;
    }

    // Thả dây cung
    public void Release()
    {
        if (!isDrawing) return;

        isDrawing = false;
        isReleasing = true;

        // Xóa mũi tên giả
        if (currentArrowVisual != null)
            Destroy(currentArrowVisual);
    }

    // Hủy kéo cung giữa chừng
    public void CancelDraw()
    {
        if (!isDrawing && !isReleasing) return;

        isDrawing = false;
        isReleasing = true;

        if (currentArrowVisual != null)
            Destroy(currentArrowVisual);
    }

    private void Update()
    {
        // Animation dây cung bật về vị trí cũ
        if (isReleasing && stringMiddleBone != null)
        {
            stringMiddleBone.localPosition = Vector3.Lerp(
                stringMiddleBone.localPosition,
                middleBoneRestLocalPosition,
                releaseSpeed * Time.deltaTime
            );
        }
    }

    /// <summary>
    /// Tính toán hướng bắn thực tế dựa trên ray từ camera với constraints
    /// </summary>
    public (Vector3 spawnPos, Vector3 shootDir, Quaternion spawnRot) GetRealisticArrowSpawnData()
    {
        Vector3 spawnPos = arrowSpawnPoint != null ? arrowSpawnPoint.position : stringMiddleBone.position;

        if (aimCamera == null)
        {
            Vector3 fallbackDir = arrowSpawnPoint != null ? arrowSpawnPoint.forward : transform.forward;
            return (spawnPos, fallbackDir, Quaternion.LookRotation(fallbackDir));
        }

        // CRITICAL FIX: Move spawn point FORWARD to avoid player colliders
        Vector3 cameraForward = aimCamera.transform.forward;
        Vector3 safeSpawnPos = spawnPos + cameraForward * 0.5f; // Move 0.5m forward

        Ray aimRay = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        Vector3 targetPoint;

        // Build ignore list for player
        Collider[] playerColliders = playerRoot != null ?
            playerRoot.GetComponentsInChildren<Collider>() : new Collider[0];

        // Raycast for target
        RaycastHit[] hits = Physics.RaycastAll(aimRay, maxAimDistance, aimLayerMask);
        RaycastHit validHit = default;
        bool foundValidHit = false;
        float closestDistance = maxAimDistance;

        foreach (RaycastHit hit in hits)
        {
            // Skip if this is a player collider
            bool isPlayerCollider = false;
            foreach (Collider playerCol in playerColliders)
            {
                if (hit.collider == playerCol)
                {
                    isPlayerCollider = true;
                    if (showAimDebug)
                        Debug.Log($"[Bow] Ignored player collider: {hit.collider.name}");
                    break;
                }
            }

            if (isPlayerCollider) continue;

            if (hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
                validHit = hit;
                foundValidHit = true;
            }
        }

        if (foundValidHit)
        {
            targetPoint = validHit.point;
        }
        else
        {
            targetPoint = aimRay.GetPoint(maxAimDistance);
        }

        // Calculate shoot direction from SAFE spawn position
        Vector3 rawShootDir = (targetPoint - safeSpawnPos).normalized;

        if (showAimDebug)
        {
            Debug.DrawRay(aimCamera.transform.position, aimRay.direction * maxAimDistance, Color.cyan, 0.1f);
            Debug.DrawLine(safeSpawnPos, targetPoint, Color.yellow, 0.1f);
            Debug.DrawRay(safeSpawnPos, rawShootDir * 5f, Color.red, 0.1f);
        }

        // CONSTRAINT 1: Check if target is too close
        float distanceToTarget = Vector3.Distance(safeSpawnPos, targetPoint);
        if (distanceToTarget < minAimDistance)
        {
            Vector3 safeDir = cameraForward;
            if (showAimDebug)
            {
                Debug.DrawRay(safeSpawnPos, safeDir * 5f, Color.green, 0.1f);
                Debug.Log($"[Bow] Target too close ({distanceToTarget:F2}m), using camera forward");
            }
            return (safeSpawnPos, safeDir, Quaternion.LookRotation(safeDir));
        }

        // CONSTRAINT 2: Check angle deviation
        float angleFromCamera = Vector3.Angle(cameraForward, rawShootDir);
        if (angleFromCamera > maxAimAngleDeviation)
        {
            Vector3 clampedDir = Vector3.RotateTowards(
                cameraForward,
                rawShootDir,
                maxAimAngleDeviation * Mathf.Deg2Rad,
                0f
            );

            if (showAimDebug)
            {
                Debug.DrawRay(safeSpawnPos, clampedDir * 5f, Color.magenta, 0.1f);
                Debug.Log($"[Bow] Angle clamped from {angleFromCamera:F1}° to {maxAimAngleDeviation}°");
            }

            return (safeSpawnPos, clampedDir, Quaternion.LookRotation(clampedDir));
        }

        // CONSTRAINT 3: Prevent backwards shooting
        float dotProduct = Vector3.Dot(cameraForward, rawShootDir);
        if (dotProduct < 0)
        {
            Vector3 safeDir = cameraForward;
            if (showAimDebug)
            {
                Debug.DrawRay(safeSpawnPos, safeDir * 5f, Color.blue, 0.1f);
                Debug.Log($"[Bow] Would shoot backwards, using camera forward");
            }
            return (safeSpawnPos, safeDir, Quaternion.LookRotation(safeDir));
        }

        return (safeSpawnPos, rawShootDir, Quaternion.LookRotation(rawShootDir));
    }

    public Vector3 GetArrowSpawnPosition()
    {
        return arrowSpawnPoint != null ? arrowSpawnPoint.position : stringMiddleBone.position;
    }

    public Quaternion GetArrowSpawnRotation()
    {
        var data = GetRealisticArrowSpawnData();
        return data.spawnRot;
    }

    public ProjectileData GetProjectileData()
    {
        return projectileData;
    }

    private void OnDisable()
    {
        // Reset khi đổi vũ khí
        if (isDrawing || isReleasing)
        {
            if (stringMiddleBone != null)
                stringMiddleBone.localPosition = middleBoneRestLocalPosition;

            if (currentArrowVisual != null)
                Destroy(currentArrowVisual);

            isDrawing = false;
            isReleasing = false;
            currentDrawAmount = 0f;
        }
    }

    // Debug visualization in editor
    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying || !showAimDebug || aimCamera == null) return;

        Vector3 spawnPos = arrowSpawnPoint != null ? arrowSpawnPoint.position : transform.position;
        Vector3 cameraForward = aimCamera.transform.forward;

        // Draw camera forward
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(spawnPos, cameraForward * 10f);

        // Draw min distance sphere
        Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
        Gizmos.DrawWireSphere(spawnPos, minAimDistance);

        // Draw max angle cone (approximate)
        Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
        Vector3 right = Vector3.Cross(cameraForward, Vector3.up).normalized;
        Vector3 maxAngleDir = Quaternion.AngleAxis(maxAimAngleDeviation, right) * cameraForward;
        Gizmos.DrawRay(spawnPos, maxAngleDir * 10f);
    }

    public bool IsDrawing => isDrawing;
    public float CurrentDrawAmount => currentDrawAmount;

    /// <summary>
    /// Set the player root transform to ignore player colliders in aiming raycast
    /// Call this when equipping the bow
    /// </summary>
    public void SetPlayerRoot(Transform player)
    {
        playerRoot = player;
    }
}