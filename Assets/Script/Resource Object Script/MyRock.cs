using Mirror;
using UnityEngine;

/// <summary>
/// MyRock - simple breakable rock.
/// - On death: spawn stone fragments + optional dust VFX, then request authoritative destroy via BaseResource.
/// - Does not set isBeingDestroyed itself; RequestDestroyAndReplace will set that flag when appropriate.
/// </summary>
public class MyRock : BaseResource, IMinenable
{
    public enum RockType { SmallRock, MediumRock, LargeRock, Boulder }

    [Header("Rock Specific")]
    [SerializeField] private RockType rockType = RockType.MediumRock; // current rock type
    [SerializeField] private Transform stonePrefab;                   // prefab for stone fragments

    [Header("Rock Effects")]
    [SerializeField] private Transform dustEffectPrefab;              // dust VFX prefab

    [Header("Fragment Settings (optional)")]
    [SerializeField] private float fragmentUpForceMin = 0.6f;
    [SerializeField] private float fragmentUpForceMax = 1.8f;
    [SerializeField] private float fragmentScatterMin = 0.2f;
    [SerializeField] private float fragmentScatterMax = 1.0f;
    [SerializeField] private float fragmentTorqueMax = 1.0f;

    protected override void InitializeHealth()
    {
        int healthAmount = rockType switch
        {
            RockType.SmallRock => 20,
            RockType.MediumRock => 30,
            RockType.LargeRock => 45,
            RockType.Boulder => 60,
            _ => 30
        };

        healthSystem = new HealthSystem(healthAmount);
        resourceType = ResourceType.Rock;
    }

    /// <summary>
    /// Called when the health system reports death.
    /// Spawn immediate visual feedback then request authoritative destroy/replace via BaseResource.
    /// </summary>
    /// <summary>
    /// Called when the health system reports death.
    /// Spawn immediate visual feedback then request authoritative destroy/replace via BaseResource.
    /// </summary>
    protected override void OnResourceDestroyed()
    {
        // Defensive early return: don't run twice if destruction already in progress
        if (isBeingDestroyed || isDestroyed)
        {
            Debug.LogWarning($"{name}: OnResourceDestroyed called but already being destroyed/removed.");
            return;
        }

        // --- CRITICAL: Only run visual effects on server ---
        if (!NetworkServer.active)
        {
            Debug.Log($"{name}: OnResourceDestroyed called on client, skipping visual effects (server will handle)");
            return;
        }

        // 1) spawn fragments with simple physics - SERVER ONLY
        SpawnStonesWithPhysics();

        // 2) spawn dust effect if assigned - SERVER ONLY
        SpawnDustEffect();

        // 3) request manager-driven destroy/replace (if manager exists).
        try
        {
            RequestDestroyAndReplace(null); // no visual replacement prefab for rock by default
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"{name}: RequestDestroyAndReplace threw: {ex}. Falling back to local destroy.");
            DestroyResource();
        }
    }

    /// <summary>
    /// Spawns stone fragments and applies forces/torque for immediate visual feedback.
    /// We instantiate each fragment individually so we can add Rigidbody impulses.
    /// </summary>
    private void SpawnStonesWithPhysics()
    {
        // Only server should spawn physics objects
        if (!NetworkServer.active) return;

        if (stonePrefab == null)
        {
            Debug.LogWarning($"{name}: stonePrefab not assigned - no fragments will be spawned.");
            return;
        }

        int stoneCount = rockType switch
        {
            RockType.SmallRock => Random.Range(1, 3),   // 1-2
            RockType.MediumRock => Random.Range(2, 4),  // 2-3
            RockType.LargeRock => Random.Range(3, 6),   // 3-5
            RockType.Boulder => Random.Range(5, 8),     // 5-7
            _ => Random.Range(2, 4)
        };

        for (int i = 0; i < stoneCount; i++)
        {
            Vector3 offset = new Vector3(
                Random.Range(-dropRadius, dropRadius),
                dropHeight + Random.Range(0f, 0.3f),
                Random.Range(-dropRadius, dropRadius)
            );

            Quaternion rot = Quaternion.Euler(
                Random.Range(0f, 360f),
                Random.Range(0f, 360f),
                Random.Range(0f, 360f)
            );

            var frag = Instantiate(stonePrefab, transform.position + offset, rot);
            if (frag == null) continue;

            GameObject fragGo = frag.gameObject;

            // Network spawn the fragment
            NetworkServer.Spawn(fragGo);

            // Ensure it has a Rigidbody so physics applies
            var rb = fragGo.GetComponent<Rigidbody>() ?? fragGo.AddComponent<Rigidbody>();

            // Apply upward + scattered impulse and some torque
            Vector3 upImpulse = Vector3.up * Random.Range(fragmentUpForceMin, fragmentUpForceMax);
            Vector3 scatter = Random.insideUnitSphere * Random.Range(fragmentScatterMin, fragmentScatterMax);
            rb.AddForce(upImpulse + scatter, ForceMode.Impulse);
            rb.AddTorque(Random.insideUnitSphere * Random.Range(0.1f, fragmentTorqueMax), ForceMode.Impulse);
        }
    }

    private void SpawnDustEffect()
    {
        // Only server should spawn effects
        if (!NetworkServer.active) return;

        if (dustEffectPrefab == null) return;
        var dust = Instantiate(dustEffectPrefab, transform.position, transform.rotation);

        // Network spawn the dust effect
        NetworkServer.Spawn(dust.gameObject);
    }

    protected override void OnDamageReceived(int amount)
    {
        base.OnDamageReceived(amount);
        // optional: small rock-chunk spawn or crack sound/particles could go here
    }

    protected override void ValidateComponents()
    {
        if (stonePrefab == null)
            Debug.LogWarning($"{name}: stonePrefab not assigned!");
        if (dustEffectPrefab == null)
            Debug.LogWarning($"{name}: dustEffectPrefab not assigned (optional).");
    }

    public override ResourceType GetResourceType() => ResourceType.Rock;
}
