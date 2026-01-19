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

    [Header("Arrow Spawn")]
    public Transform arrowSpawnPoint;
    public GameObject arrowVisualPoint;

    [Header("Right Hand IK")]
    [Tooltip("Transform for RIGHT hand to follow string (auto-created as child of stringMiddleBone)")]
    public Transform rightHandIKTarget;
    public Transform rightHintPosition;

    [Header("Bow Animator")]
    [Tooltip("Animator on the bow prefab that controls string pull animation")]
    public Animator bowAnimator;

    [Tooltip("Speed at which bow snaps back to idle after release")]
    public float releaseSpeed = 10f;

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

    [Tooltip("Forward offset from arrow spawn point to avoid player colliders")]
    public float spawnForwardOffset = 0.5f;

    [Tooltip("Show debug rays for aiming")]
    public bool showAimDebug = true;

    [Header("Spawn Debug")]
    [Tooltip("Show visual debug sphere when arrow spawns")]
    public bool showSpawnDebug = true;

    [Tooltip("Duration to show spawn debug sphere")]
    public float spawnDebugDuration = 2f;

    // Animation parameter name - single blend tree parameter
    private const string ANIM_CHARGE_AMOUNT = "ChargeAmount";

    // Private state
    public bool isDrawing = false;
    private bool isReleasing = false;
    private float currentDrawAmount = 0f;
    private float targetDrawAmount = 0f;

    private void Awake()
    {
        // Auto-find camera if not assigned
        if (aimCamera == null)
            aimCamera = Camera.main;

        // Auto-find player root if not assigned
        if (playerRoot == null)
        {
            playerRoot = GetComponentInParent<PlayerMovement>()?.transform;
            if (playerRoot == null)
            {
                GameObject playerObj = GameObject.FindGameObjectWithTag("PlayerMovement");
                if (playerObj != null)
                    playerRoot = playerObj.transform;
            }

            if (playerRoot != null && showAimDebug)
                Debug.Log($"[BowString] Auto-found player root: {playerRoot.name}");
        }

        // Auto-find bow animator if not assigned
        if (bowAnimator == null)
        {
            bowAnimator = GetComponent<Animator>();
            if (bowAnimator == null)
            {
                Debug.LogWarning("[BowString] No Animator found on bow! String won't animate.");
            }
        }

        // Initialize animator to idle state
        if (bowAnimator != null)
        {
            bowAnimator.SetFloat(ANIM_CHARGE_AMOUNT, 0f);
        }
    }

    /// <summary>
    /// Start charging - begins the bow's charge animation
    /// </summary>
    public void StartDrawing()
    {
        if (isDrawing) return;

        isDrawing = true;
        isReleasing = false;
        currentDrawAmount = 0f;
        targetDrawAmount = 0f;
        arrowVisualPoint.SetActive(true);

        Debug.Log("[BowString] Started charging - bow animator will blend from 0 to 1");
    }

    /// <summary>
    /// Update charge amount (0 → 1) - updates target for blend tree
    /// Player animator and bow animator will sync via this value
    /// </summary>
    public void UpdateDrawAmount(float drawPercent)
    {
        if (!isDrawing) return;

        targetDrawAmount = Mathf.Clamp01(drawPercent);
    }

    /// <summary>
    /// Release the bow - bow will snap back to idle quickly
    /// </summary>
    public void Release()
    {
        if (!isDrawing) return;

        isDrawing = false;
        isReleasing = true;
        targetDrawAmount = 0f; // Target back to idle
        arrowVisualPoint.SetActive(false);

        Debug.Log("[BowString] Released - bow will snap back to idle (ChargeAmount → 0)");
    }

    /// <summary>
    /// Cancel drawing mid-charge
    /// </summary>
    public void CancelDraw()
    {
        if (!isDrawing && !isReleasing) return;

        isDrawing = false;
        isReleasing = true;
        targetDrawAmount = 0f;
        arrowVisualPoint.SetActive(false);

        Debug.Log("[BowString] Cancelled - bow will snap back to idle");
    }

    private void Update()
    {
        // Smooth lerp to target charge amount when charging
        if (isDrawing)
        {
            // Smooth increase when charging (slower, more controlled)
            currentDrawAmount = Mathf.Lerp(currentDrawAmount, targetDrawAmount, 5f * Time.deltaTime);
        }
        // Fast snap back to idle when releasing
        else if (isReleasing)
        {
            // Fast decrease when releasing (quick snap-back)
            currentDrawAmount = Mathf.Lerp(currentDrawAmount, 0f, releaseSpeed * Time.deltaTime);

            // Stop releasing when nearly at idle
            if (currentDrawAmount < 0.01f)
            {
                currentDrawAmount = 0f;
                isReleasing = false;
            }
        }

        // Update bow animator blend tree parameter
        if (bowAnimator != null)
        {
            bowAnimator.SetFloat(ANIM_CHARGE_AMOUNT, currentDrawAmount);
        }
    }

    /// <summary>
    /// Calculate realistic arrow spawn data with aiming constraints
    /// </summary>
    public (Vector3 spawnPos, Vector3 shootDir, Quaternion spawnRot) GetRealisticArrowSpawnData()
    {
        Vector3 baseSpawnPos = arrowSpawnPoint != null ? arrowSpawnPoint.position : stringMiddleBone.position;

        if (aimCamera == null)
        {
            Vector3 fallbackDir = arrowSpawnPoint != null ? arrowSpawnPoint.forward : transform.forward;
            Vector3 offsetSpawnPos = baseSpawnPos + fallbackDir * spawnForwardOffset;
            return (offsetSpawnPos, fallbackDir, Quaternion.LookRotation(fallbackDir));
        }

        Vector3 cameraForward = aimCamera.transform.forward;
        Vector3 finalSpawnPos = baseSpawnPos + cameraForward * spawnForwardOffset;

        Ray aimRay = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        Vector3 targetPoint;

        Collider[] playerColliders = playerRoot != null ?
            playerRoot.GetComponentsInChildren<Collider>() : new Collider[0];

        RaycastHit[] hits = Physics.RaycastAll(aimRay, maxAimDistance, aimLayerMask);
        RaycastHit validHit = default;
        bool foundValidHit = false;
        float closestDistance = maxAimDistance;

        foreach (RaycastHit hit in hits)
        {
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

        targetPoint = foundValidHit ? validHit.point : aimRay.GetPoint(maxAimDistance);

        Vector3 rawShootDir = (targetPoint - finalSpawnPos).normalized;

        if (showAimDebug)
        {
            Debug.DrawRay(aimCamera.transform.position, aimRay.direction * maxAimDistance, Color.cyan, 0.1f);
            Debug.DrawLine(baseSpawnPos, finalSpawnPos, Color.white, 0.1f);
            Debug.DrawLine(finalSpawnPos, targetPoint, Color.yellow, 0.1f);
            Debug.DrawRay(finalSpawnPos, rawShootDir * 5f, Color.red, 0.1f);
        }

        // CONSTRAINT 1: Check if target is too close
        float distanceToTarget = Vector3.Distance(finalSpawnPos, targetPoint);
        if (distanceToTarget < minAimDistance)
        {
            if (showAimDebug)
                Debug.Log($"[Bow] Target too close ({distanceToTarget:F2}m), using camera forward");
            return (finalSpawnPos, cameraForward, Quaternion.LookRotation(cameraForward));
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
                Debug.Log($"[Bow] Angle clamped from {angleFromCamera:F1}° to {maxAimAngleDeviation}°");

            return (finalSpawnPos, clampedDir, Quaternion.LookRotation(clampedDir));
        }

        // CONSTRAINT 3: Prevent backwards shooting
        float dotProduct = Vector3.Dot(cameraForward, rawShootDir);
        if (dotProduct < 0)
        {
            if (showAimDebug)
                Debug.Log("[Bow] Would shoot backwards, using camera forward");
            return (finalSpawnPos, cameraForward, Quaternion.LookRotation(cameraForward));
        }

        return (finalSpawnPos, rawShootDir, Quaternion.LookRotation(rawShootDir));
    }

    public Vector3 GetArrowSpawnPosition()
    {
        Vector3 basePos = arrowSpawnPoint != null ? arrowSpawnPoint.position : stringMiddleBone.position;

        if (aimCamera != null)
        {
            return basePos + aimCamera.transform.forward * spawnForwardOffset;
        }
        else
        {
            Vector3 forward = arrowSpawnPoint != null ? arrowSpawnPoint.forward : transform.forward;
            return basePos + forward * spawnForwardOffset;
        }
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

    public void DebugArrowSpawn(Vector3 spawnPosition, Vector3 shootDirection)
    {
        if (!showSpawnDebug) return;

        Debug.DrawRay(spawnPosition, Vector3.up * 0.5f, Color.green, spawnDebugDuration);
        Debug.DrawRay(spawnPosition, Vector3.down * 0.5f, Color.green, spawnDebugDuration);
        Debug.DrawRay(spawnPosition, Vector3.left * 0.5f, Color.green, spawnDebugDuration);
        Debug.DrawRay(spawnPosition, Vector3.right * 0.5f, Color.green, spawnDebugDuration);
        Debug.DrawRay(spawnPosition, Vector3.forward * 0.5f, Color.green, spawnDebugDuration);
        Debug.DrawRay(spawnPosition, Vector3.back * 0.5f, Color.green, spawnDebugDuration);
        Debug.DrawRay(spawnPosition, shootDirection * 10f, Color.red, spawnDebugDuration);

        Debug.Log($"[BowString] 🏹 ARROW SPAWNED at {spawnPosition} | Direction: {shootDirection}");
    }

    private void OnDisable()
    {
        // Reset when switching weapons
        if (isDrawing || isReleasing)
        {
            if (bowAnimator != null)
            {
                bowAnimator.SetFloat(ANIM_CHARGE_AMOUNT, 0f);
            }

            isDrawing = false;
            isReleasing = false;
            currentDrawAmount = 0f;
            targetDrawAmount = 0f;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying || !showAimDebug || aimCamera == null) return;

        Vector3 basePos = arrowSpawnPoint != null ? arrowSpawnPoint.position : transform.position;
        Vector3 cameraForward = aimCamera.transform.forward;
        Vector3 offsetPos = basePos + cameraForward * spawnForwardOffset;

        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(basePos, 0.05f);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(offsetPos, 0.05f);

        Gizmos.color = Color.white;
        Gizmos.DrawLine(basePos, offsetPos);

        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(offsetPos, cameraForward * 10f);

        Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
        Gizmos.DrawWireSphere(offsetPos, minAimDistance);
    }

    public bool IsDrawing => isDrawing;
    public float CurrentDrawAmount => currentDrawAmount;

    public void SetPlayerRoot(Transform player)
    {
        playerRoot = player;
    }
}