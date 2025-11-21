using UnityEngine;
using Mirror;

[RequireComponent(typeof(Rigidbody))]
public class ArrowProjectile : NetworkBehaviour
{
    [Header("Arrow Settings")]
    [SerializeField] private float lifetime = 10f;
    [SerializeField] private float gravityMultiplier = 1f;
    [SerializeField] private float stuckLifetime = 5f;

    [Header("Physics")]
    [SerializeField] private float minVelocityToStick = 2f;
    [SerializeField] private LayerMask damageableLayers;

    [Header("Visual")]
    [SerializeField] private TrailRenderer trailRenderer;
    [SerializeField] private GameObject impactParticles;

    [Header("Audio")]
    [SerializeField] private AudioClip hitFleshSound;
    [SerializeField] private AudioClip hitWoodSound;
    [SerializeField] private AudioClip hitStoneSound;

    // Components
    private Rigidbody rb;
    private Collider arrowCollider;
    private bool hasHit = false;
    private bool isStuck = false;
    private float spawnTime;

    // Network synced data
    [SyncVar] private GameObject shooter;
    [SyncVar] private int itemId;
    [SyncVar] private float damage;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        arrowCollider = GetComponent<Collider>();

        if (rb != null)
        {
            rb.useGravity = true;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        }
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        spawnTime = Time.time;

        // Destroy after lifetime
        Invoke(nameof(DestroyArrow), lifetime);
    }

    private void FixedUpdate()
    {
        if (!isServer || isStuck) return;

        // Point arrow in direction of travel
        if (rb != null && rb.linearVelocity.sqrMagnitude > 0.1f)
        {
            transform.rotation = Quaternion.LookRotation(rb.linearVelocity);
        }

        // Apply custom gravity
        if (rb != null && gravityMultiplier != 1f)
        {
            rb.AddForce(Physics.gravity * (gravityMultiplier - 1f), ForceMode.Acceleration);
        }
    }

    /// <summary>
    /// Initialize the arrow with shooter info and velocity
    /// </summary>
    public void Initialize(GameObject shooterObject, Vector3 velocity, float dmg, int weaponItemId = 0)
    {
        if (!isServer) return;

        shooter = shooterObject;
        damage = dmg;
        itemId = weaponItemId;

        // Make sure rigidbody is ready
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        if (rb != null)
        {
            // Ensure physics is enabled
            rb.isKinematic = false;
            rb.useGravity = true;

            // Set velocity and rotation
            rb.linearVelocity = velocity;
            transform.rotation = Quaternion.LookRotation(velocity.normalized);

            Debug.Log($"[Arrow] Velocity set: {rb.linearVelocity.magnitude}m/s, Direction: {velocity.normalized}");
        }
        else
        {
            Debug.LogError("[Arrow] Rigidbody is NULL! Cannot apply velocity!");
        }

        // Ignore collision with shooter
        if (shooter != null)
        {
            Collider[] shooterColliders = shooter.GetComponentsInChildren<Collider>();
            foreach (var col in shooterColliders)
            {
                if (arrowCollider != null)
                {
                    Physics.IgnoreCollision(arrowCollider, col);
                }
            }
        }

        Debug.Log($"[Arrow] Initialized: damage={damage}, velocity={velocity.magnitude}m/s");
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!isServer || hasHit) return;

        hasHit = true;

        // Get hit information
        ContactPoint contact = collision.contacts[0];
        Vector3 hitPoint = contact.point;
        Vector3 hitNormal = contact.normal;
        Vector3 hitDirection = rb != null ? rb.linearVelocity.normalized : transform.forward;

        // Check if we hit something damageable
        IDamageable damageable = collision.gameObject.GetComponent<IDamageable>();
        bool hitLivingTarget = damageable != null && !damageable.IsDead();

        if (damageable != null)
        {
            // Create hit info
            ItemData weaponData = itemId > 0 ? ItemDatabase.Get(itemId) : null;
            HitInfo hitInfo = new HitInfo(hitPoint, hitNormal, hitDirection, shooter, weaponData);

            // Deal damage
            int finalDamage = Mathf.RoundToInt(damage);
            damageable.Damage(finalDamage, hitInfo);

            Debug.Log($"[Arrow] Hit {collision.gameObject.name} for {finalDamage} damage");
        }

        // Determine if arrow should stick
        float currentSpeed = rb != null ? rb.linearVelocity.magnitude : 0f;
        bool shouldStick = currentSpeed >= minVelocityToStick && !hitLivingTarget;

        if (shouldStick)
        {
            StickToSurface(collision.gameObject, hitPoint, hitNormal);
        }
        else
        {
            // Arrow hit living target or too slow - destroy with effect
            SpawnImpactEffect(hitPoint, hitNormal);
            PlayImpactSound(collision.gameObject.tag, hitPoint);
            DestroyArrow();
        }
    }

    private void StickToSurface(GameObject surface, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (!isServer) return;

        isStuck = true;

        // Disable physics
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // Disable collider to prevent further collisions
        if (arrowCollider != null)
        {
            arrowCollider.enabled = false;
        }

        // Parent to surface if it can move
        Rigidbody surfaceRb = surface.GetComponent<Rigidbody>();
        if (surfaceRb != null)
        {
            transform.SetParent(surface.transform);
        }

        // Position arrow slightly embedded
        transform.position = hitPoint + transform.forward * 0.05f;

        // Disable trail
        if (trailRenderer != null)
        {
            trailRenderer.enabled = false;
        }

        // Play impact sound
        PlayImpactSound(surface.tag, hitPoint);

        // Reduce lifetime when stuck
        CancelInvoke(nameof(DestroyArrow));
        Invoke(nameof(DestroyArrow), stuckLifetime);

        Debug.Log($"[Arrow] Stuck to {surface.name}");
    }

    private void SpawnImpactEffect(Vector3 position, Vector3 normal)
    {
        if (impactParticles != null)
        {
            GameObject effect = Instantiate(impactParticles, position, Quaternion.LookRotation(normal));
            NetworkServer.Spawn(effect);
            Destroy(effect, 2f);
        }
    }

    private void PlayImpactSound(string surfaceTag, Vector3 position)
    {
        AudioClip clip = null;

        // Select sound based on surface type
        switch (surfaceTag)
        {
            case "Enemy":
            case "Player":
                clip = hitFleshSound;
                break;
            case "Wood":
                clip = hitWoodSound;
                break;
            case "Stone":
            case "Ground":
                clip = hitStoneSound;
                break;
            default:
                clip = hitWoodSound; // Default sound
                break;
        }

        if (clip != null)
        {
            // Play sound at hit location (using RPC for all clients)
            RpcPlaySound(clip.name, position);
        }
    }

    [ClientRpc]
    private void RpcPlaySound(string clipName, Vector3 position)
    {
        // You'll need to implement your audio system here
        // Example: AudioManager.PlaySoundAtPoint(clipName, position);
        Debug.Log($"[Arrow] Playing sound: {clipName} at {position}");
    }

    private void DestroyArrow()
    {
        if (isServer)
        {
            NetworkServer.Destroy(gameObject);
        }
    }

    // Public getters
    public bool IsStuck => isStuck;
    public float GetDamage() => damage;
    public GameObject GetShooter() => shooter;

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        // Visualize arrow direction
        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position, transform.forward * 0.3f);
    }
#endif
}