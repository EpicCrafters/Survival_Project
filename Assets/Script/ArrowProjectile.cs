using UnityEngine;
using Mirror;
using System.Collections;
using System.Collections.Generic;

public class ArrowProjectile : NetworkBehaviour
{
    [Header("Projectile Data")]
    [SerializeField] private ProjectileData projectileData;

    [Header("Visual")]
    [SerializeField] private TrailRenderer trailRenderer;
    [SerializeField] private GameObject impactParticles;
    [SerializeField] private GameObject arrowStuckVisualPrefab;

    [Header("Audio")]
    [SerializeField] private AudioClip hitFleshSound;
    [SerializeField] private AudioClip hitWoodSound;
    [SerializeField] private AudioClip hitStoneSound;

    [Header("Hit Effects")]
    [SerializeField]
    [Tooltip("Bật/tắt hiệu ứng khi đánh trúng")]
    private bool enableHitEffects = true;

    [SerializeField]
    [Tooltip("Bật để phát effect ngay cả khi đánh trúng bề mặt thường (không có IDamageable)")]
    private bool enableSurfaceHitEffects = true;

    [Header("Raycast Settings")]
    [SerializeField] private LayerMask ignoreStickLayers;
    [SerializeField] private LayerMask damageableLayers;
    [SerializeField] private float raycastRadius = 0.1f;
    [SerializeField] private bool showDebugRay = true;
    [SerializeField] private float arrowTipOffset = 0.5f;

    [Header("Stick Settings")]
    [SerializeField] private float penetrationDepth = 0.15f;
    [SerializeField][Range(0f, 1f)] private float surfaceAlignmentStrength = 0.3f;
    [SerializeField] private float maxStickAngle = 85f;

    private bool hasHit = false;
    private bool isStuck = false;
    private bool isInitialized = false;

    private Vector3 currentPosition;
    private Vector3 previousPosition;
    private Vector3 currentTipPosition;
    private Vector3 previousTipPosition;
    private Vector3 velocity;

    [SyncVar] private GameObject shooter;
    [SyncVar] private int itemId;
    [SyncVar] private float damage;
    [SyncVar] private float speed;
    [SyncVar(hook = nameof(OnVelocityChanged))] private Vector3 syncedVelocity;

    private float gravityMultiplier = 1f;
    private const float DEFAULT_DAMAGE = 10f;
    private const float DEFAULT_SPEED = 20f;
    private const float DEFAULT_LIFETIME = 10f;
    private const float DEFAULT_STUCK_LIFETIME = 5f;
    private Collider lastHitCollider;

    #region Initialization

    private void Awake()
    {
        currentPosition = transform.position;
        previousPosition = transform.position;
    }

    public override void OnStartServer()
    {
        float lifetime = projectileData != null ? projectileData.lifetime : DEFAULT_LIFETIME;
        Invoke(nameof(DestroyArrow), lifetime);
    }

    public override void OnStartClient()
    {
        if (!isServer && isInitialized)
        {
            velocity = syncedVelocity;
        }
    }

    #endregion

    #region Update Loop

    private void FixedUpdate()
    {
        if (isStuck || !isInitialized) return;

        previousPosition = currentPosition;
        previousTipPosition = currentTipPosition;

        ApplyGravity();

        currentPosition += velocity * Time.fixedDeltaTime;
        currentTipPosition = currentPosition + transform.forward * arrowTipOffset;

        if (isServer)
        {
            CheckForHit();
        }

        transform.position = currentPosition;
        UpdateArrowRotation();
    }

    private void ApplyGravity()
    {
        float gravity = projectileData != null ? projectileData.gravityMultiplier : 1f;
        velocity += Physics.gravity * gravity * Time.fixedDeltaTime;
    }

    private void UpdateArrowRotation()
    {
        if (velocity.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.LookRotation(velocity.normalized);
        }
    }

    #endregion

    #region Initialization & Velocity

    public void Initialize(GameObject shooterObject, Vector3 initialVelocity, float chargePercent,
        int weaponItemId = 0, ProjectileData data = null)
    {
        if (!isServer) return;

        shooter = shooterObject;
        itemId = weaponItemId;
        isInitialized = true;

        if (data != null)
        {
            projectileData = data;
        }

        CalculateProjectileStats(chargePercent);
        gravityMultiplier = projectileData != null ? projectileData.gravityMultiplier : 1f;

        velocity = initialVelocity;
        syncedVelocity = initialVelocity;
        currentPosition = transform.position;
        previousPosition = transform.position;

        currentTipPosition = transform.position + transform.forward * arrowTipOffset;
        previousTipPosition = currentTipPosition;

        RpcApplyVelocity(initialVelocity);
        IgnoreShooterCollision();
    }

    private void CalculateProjectileStats(float chargePercent)
    {
        if (projectileData != null)
        {
            var stats = projectileData.GetProjectileStats(chargePercent);
            damage = stats.damage;
            speed = stats.speed;
        }
        else
        {
            damage = DEFAULT_DAMAGE;
            speed = DEFAULT_SPEED;
        }
    }

    private void IgnoreShooterCollision()
    {
        if (shooter == null) return;
        StartCoroutine(IgnoreShooterForTime(0.1f));
    }

    private System.Collections.IEnumerator IgnoreShooterForTime(float time)
    {
        float elapsed = 0f;
        while (elapsed < time)
        {
            elapsed += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }
    }

    [ClientRpc]
    private void RpcApplyVelocity(Vector3 initialVelocity)
    {
        if (!isServer)
        {
            velocity = initialVelocity;
            currentPosition = transform.position;
            previousPosition = transform.position;
            currentTipPosition = transform.position + transform.forward * arrowTipOffset;
            previousTipPosition = currentTipPosition;
            isInitialized = true;
        }
    }

    private void OnVelocityChanged(Vector3 oldVelocity, Vector3 newVelocity)
    {
        if (!isServer && isInitialized)
        {
            velocity = newVelocity;
        }
    }

    #endregion

    #region Raycast Hit Detection

    private void CheckForHit()
    {
        Vector3 direction = currentTipPosition - previousTipPosition;
        float distance = direction.magnitude;

        if (distance < 0.001f) return;

        Vector3 normalizedDir = direction.normalized;

        if (showDebugRay)
        {
            Debug.DrawRay(previousTipPosition, direction, Color.red, 2f);
            Debug.DrawLine(previousTipPosition, currentTipPosition, Color.yellow, 2f);
        }

        int layerMask = ~ignoreStickLayers.value;
        RaycastHit hit;

        bool didHit = Physics.SphereCast(
            previousTipPosition,
            raycastRadius,
            normalizedDir,
            out hit,
            distance,
            layerMask,
            QueryTriggerInteraction.Collide
        );

        if (didHit)
        {
            if (shooter != null && hit.collider.transform.IsChildOf(shooter.transform))
            {
                return;
            }

            if (showDebugRay)
            {
                Debug.DrawRay(hit.point, hit.normal * 0.5f, Color.green, 5f);
                Debug.Log($"Arrow hit: {hit.collider.name}");
            }

            ProcessHit(hit);
        }
    }

    private void ProcessHit(RaycastHit hit)
    {
        if (hasHit) return;
        hasHit = true;

        lastHitCollider = hit.collider;

        Vector3 hitPoint = hit.point;
        Vector3 hitNormal = hit.normal;
        Vector3 hitDirection = velocity.normalized;

        bool canDamage = IsLayerInMask(hit.collider.gameObject.layer, damageableLayers);
        bool dealtDamage = false;

        // Apply damage if possible
        if (canDamage)
        {
            dealtDamage = ApplyDamage(hit.collider.gameObject, hitPoint, hitNormal, hitDirection);
        }

        // ⚡ CALL RPC TO PLAY EFFECTS ON ALL CLIENTS ⚡
        string surfaceTag = hit.collider.tag;
        RpcPlayHitEffects(hitPoint, hitNormal, dealtDamage, surfaceTag, hit.collider.gameObject.name);

        StickToSurface(hit.collider.transform, hitPoint, hitNormal, hitDirection);
    }

    private bool ApplyDamage(GameObject target, Vector3 hitPoint, Vector3 hitNormal, Vector3 hitDirection)
    {
        IDamageable damageable = target.GetComponent<IDamageable>();
        if (damageable == null)
        {
            damageable = target.GetComponentInParent<IDamageable>();
        }

        if (damageable == null)
        {
            Debug.LogWarning($"[Arrow] Hit damageable layer but no IDamageable component found on {target.name}");
            return false;
        }

        ItemData weaponData = itemId > 0 ? ItemDatabase.Get(itemId) : null;
        HitInfo info = new HitInfo(hitPoint, hitNormal, hitDirection, shooter, weaponData);
        damageable.Damage(Mathf.RoundToInt(damage), info);

        Debug.Log($"[Arrow] Applied {damage} damage to {target.name}");
        return true;
    }

    /// <summary>
    /// ⚡ NEW RPC - Plays hit effects on all clients
    /// </summary>
    [ClientRpc]
    private void RpcPlayHitEffects(Vector3 hitPoint, Vector3 hitNormal, bool dealtDamage, string surfaceTag, string hitObjectName)
    {
        // Play impact particles
        PlayImpactParticles(hitPoint, hitNormal);

        // Play impact sound (using tag to determine sound type)
        AudioClip clip = GetImpactSoundForTag(surfaceTag);
        if (clip != null)
        {
            AudioSource.PlayClipAtPoint(clip, hitPoint, 0.5f);
        }

        // Play HitEffectManager effects if available
        if (enableHitEffects && HitEffectManager.Instance != null)
        {
            // Try to find the collider by name (not perfect but works for most cases)
            Collider[] nearbyColliders = Physics.OverlapSphere(hitPoint, 0.5f);
            Collider targetCollider = null;

            foreach (var col in nearbyColliders)
            {
                if (col.gameObject.name == hitObjectName)
                {
                    targetCollider = col;
                    break;
                }
            }

            if (dealtDamage && targetCollider != null)
            {
                // Blood effect for damageable targets
                HitEffectManager.Instance.PlayHitEffect(hitPoint, hitNormal, targetCollider, targetCollider.gameObject);
                Debug.Log($"[Arrow Client] Played blood effect");
            }
            else if (!dealtDamage && enableSurfaceHitEffects && targetCollider != null)
            {
                // Surface effect for non-damageable targets
                HitEffectManager.Instance.PlayHitEffect(hitPoint, hitNormal, targetCollider, targetCollider.gameObject);
                Debug.Log($"[Arrow Client] Played surface effect");
            }
        }
    }

    #endregion

    #region Stick To Surface

    private void StickToSurface(Transform hitTransform, Vector3 hitPoint, Vector3 hitNormal, Vector3 hitDirection)
    {
        if (!isServer) return;

        isStuck = true;

        Quaternion stuckRotation = CalculateStickRotation(hitDirection, hitNormal);
        Vector3 adjustedHitPoint = CalculatePenetrationPoint(hitPoint, hitDirection, hitNormal);

        currentPosition = adjustedHitPoint;
        transform.position = adjustedHitPoint;
        transform.rotation = stuckRotation;

        // Create stuck visual on server
        GameObject stuckVisual = CreateStuckVisual(hitTransform, adjustedHitPoint, stuckRotation);

        NetworkIdentity surfaceNid = hitTransform.GetComponentInParent<NetworkIdentity>();

        string transformPath = "";
        if (surfaceNid != null)
        {
            transformPath = GetTransformPath(hitTransform, surfaceNid.transform);
        }

        // Send to all clients
        RpcStickToSurface(
            surfaceNid != null ? surfaceNid.netId : 0,
            transformPath,
            adjustedHitPoint,
            stuckRotation,
            hitNormal,
            hitDirection
        );

        // ⚡ INCREASED DELAY - Give time for RPC to reach clients
        CancelInvoke(nameof(DestroyArrow));
        float stuckTime = projectileData != null ? projectileData.stuckLifetime : DEFAULT_STUCK_LIFETIME;
        Invoke(nameof(DestroyArrow), stuckTime);
    }

    private string GetTransformPath(Transform target, Transform root)
    {
        if (target == null || root == null || target == root)
            return "";

        List<string> path = new List<string>();
        Transform current = target;

        while (current != null && current != root)
        {
            path.Insert(0, current.name);
            current = current.parent;
        }

        return string.Join("/", path);
    }

    private Quaternion CalculateStickRotation(Vector3 hitDirection, Vector3 hitNormal)
    {
        Vector3 intoSurface = -hitNormal;
        float angleToSurface = Vector3.Angle(hitDirection, intoSurface);

        float dynamicBlend = surfaceAlignmentStrength;

        if (angleToSurface > maxStickAngle)
        {
            float excessAngle = angleToSurface - maxStickAngle;
            dynamicBlend = Mathf.Lerp(surfaceAlignmentStrength, 0.8f, excessAngle / (180f - maxStickAngle));
        }

        Vector3 blendedDirection = Vector3.Slerp(hitDirection, intoSurface, dynamicBlend).normalized;
        return Quaternion.LookRotation(blendedDirection);
    }

    private Vector3 CalculatePenetrationPoint(Vector3 hitPoint, Vector3 hitDirection, Vector3 hitNormal)
    {
        Vector3 intoSurface = -hitNormal;
        float angleToSurface = Vector3.Angle(hitDirection, intoSurface);

        float dynamicBlend = surfaceAlignmentStrength;
        if (angleToSurface > maxStickAngle)
        {
            float excessAngle = angleToSurface - maxStickAngle;
            dynamicBlend = Mathf.Lerp(surfaceAlignmentStrength, 0.8f, excessAngle / (180f - maxStickAngle));
        }

        Vector3 blendedDirection = Vector3.Slerp(hitDirection, intoSurface, dynamicBlend).normalized;
        Vector3 penetrationOffset = blendedDirection * penetrationDepth;

        return hitPoint + penetrationOffset;
    }

    private GameObject CreateStuckVisual(Transform parentTransform, Vector3 hitPoint, Quaternion stuckRotation)
    {
        if (arrowStuckVisualPrefab == null) return null;

        GameObject stuckVisual = Instantiate(arrowStuckVisualPrefab, hitPoint, stuckRotation);

        stuckVisual.transform.SetParent(parentTransform, true);
        stuckVisual.transform.position = hitPoint;
        stuckVisual.transform.rotation = stuckRotation;

        Transform arrowModel = stuckVisual.transform.Find("ArrowModel");
        if (arrowModel != null)
        {
            Vector3 parentScale = parentTransform.lossyScale;
            arrowModel.localScale = new Vector3(
                1f / parentScale.x,
                1f / parentScale.y,
                1f / parentScale.z
            );
        }

        float stuckTime = projectileData != null ? projectileData.stuckLifetime : DEFAULT_STUCK_LIFETIME;
        Destroy(stuckVisual, stuckTime);

        return stuckVisual;
    }

    [ClientRpc]
    private void RpcStickToSurface(uint surfaceNetId, string transformPath, Vector3 hitPoint,
        Quaternion stuckRotation, Vector3 hitNormal, Vector3 hitDirection)
    {
        if (isServer) return; // Server already handled this

        isStuck = true;
        currentPosition = hitPoint;
        transform.position = hitPoint;
        transform.rotation = stuckRotation;

        Transform targetTransform = null;

        if (surfaceNetId != 0 && NetworkClient.spawned.TryGetValue(surfaceNetId, out var surfaceIdentity))
        {
            if (!string.IsNullOrEmpty(transformPath))
            {
                targetTransform = surfaceIdentity.transform.Find(transformPath);

                if (targetTransform == null)
                {
                    targetTransform = surfaceIdentity.transform;
                }
            }
            else
            {
                targetTransform = surfaceIdentity.transform;
            }
        }

        if (targetTransform != null)
        {
            CreateStuckVisual(targetTransform, hitPoint, stuckRotation);
        }
        else
        {
            // Fallback: create visual without parenting
            Debug.LogWarning("[Arrow] Could not find parent transform, creating unparented visual");
            if (arrowStuckVisualPrefab != null)
            {
                GameObject stuckVisual = Instantiate(arrowStuckVisualPrefab, hitPoint, stuckRotation);
                float stuckTime = projectileData != null ? projectileData.stuckLifetime : DEFAULT_STUCK_LIFETIME;
                Destroy(stuckVisual, stuckTime);
            }
        }
    }

    #endregion

    #region Audio & Effects

    private AudioClip GetImpactSoundForTag(string surfaceTag)
    {
        switch (surfaceTag)
        {
            case "Enemy":
            case "Player":
                return hitFleshSound;
            case "Stone":
            case "Ground":
                return hitStoneSound;
            default:
                return hitWoodSound;
        }
    }

    private void PlayImpactParticles(Vector3 position, Vector3 normal)
    {
        if (impactParticles == null) return;

        GameObject particles = Instantiate(impactParticles, position, Quaternion.LookRotation(normal));
        Destroy(particles, 2f);
    }

    #endregion

    #region Utility

    private bool IsLayerInMask(int layer, LayerMask mask)
    {
        return ((1 << layer) & mask.value) != 0;
    }

    private void DestroyArrow()
    {
        if (isServer)
        {
            NetworkServer.Destroy(gameObject);
        }
    }

    #endregion

    #region Debug Visualization

    private void OnDrawGizmos()
    {
        if (!showDebugRay) return;

        Vector3 tipPos = transform.position + transform.forward * arrowTipOffset;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(tipPos, 0.05f);

        Gizmos.color = Color.blue;
        Gizmos.DrawLine(transform.position, tipPos);

        if (Application.isPlaying)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawRay(tipPos, velocity.normalized * 0.5f);

            Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
            Gizmos.DrawWireSphere(tipPos, raycastRadius);
        }
    }

    #endregion

    #region Public API

    public bool IsStuck => isStuck;
    public GameObject Shooter => shooter;
    public float Damage => damage;
    public Vector3 Velocity => velocity;

    #endregion
}