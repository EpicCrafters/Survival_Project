using Mirror;
using UnityEngine;

/// <summary>
/// MyRock - breakable rock that inherits from ChoppableBase
/// Follows same pattern as MyTree: vanishes and spawns networked fragments
/// </summary>
public class MyRock : ChoppableBase
{
    public enum RockType { SmallRock, MediumRock, LargeRock, Boulder }

    [Header("Rock Specific")]
    [SerializeField] private RockType rockType = RockType.MediumRock;
    [SerializeField] private Transform stonePrefab;
    [SerializeField] private string destructionEffectId = "rock_dust"; // ✅ New field
    


    [Header("Fragment Settings")]
    [SerializeField] private float fragmentUpForceMin = 0.6f;
    [SerializeField] private float fragmentUpForceMax = 1.8f;
    [SerializeField] private float fragmentScatterMin = 0.2f;
    [SerializeField] private float fragmentScatterMax = 1.0f;
    [SerializeField] private float fragmentTorqueMax = 1.0f;

    protected override int GetHealthAmount()
    {
        return rockType switch
        {
            RockType.SmallRock => 20,
            RockType.MediumRock => 30,
            RockType.LargeRock => 45,
            RockType.Boulder => 60,
            _ => 30
        };
    }

    protected override void SpawnChopResults()
    {
        int stoneCount = rockType switch
        {
            RockType.SmallRock => Random.Range(1, 3),
            RockType.MediumRock => Random.Range(2, 4),
            RockType.LargeRock => Random.Range(3, 6),
            RockType.Boulder => Random.Range(5, 8),
            _ => Random.Range(2, 4)
        };

        DebugLog($"Rock destroyed - spawning {stoneCount} stone fragments");

        // Spawn stone fragments
        if (stonePrefab != null)
        {
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
                SpawnNetworkedFragment(transform.position + offset, rot);
            }
        }

        // ✅ Play destruction effect using the configured effect ID
        if (WorldResourceManager.Instance != null && !string.IsNullOrEmpty(destructionEffectId))
        {
            WorldResourceManager.Instance.RpcPlayDestructionEffect(
                effectPosition.transform.position,
                effectPosition.transform.rotation,
                destructionEffectId
            );
        }
    }

    private void SpawnNetworkedFragment(Vector3 position, Quaternion rotation)
    {
        if (stonePrefab == null || !NetworkServer.active) return;

        var fragment = Instantiate(stonePrefab, position, rotation);
        var rb = fragment.GetComponent<Rigidbody>();

        if (rb == null)
        {
            rb = fragment.gameObject.AddComponent<Rigidbody>();
        }

        // Apply physics forces
        Vector3 upImpulse = Vector3.up * Random.Range(fragmentUpForceMin, fragmentUpForceMax);
        Vector3 scatter = Random.insideUnitSphere * Random.Range(fragmentScatterMin, fragmentScatterMax);
        rb.AddForce(upImpulse + scatter, ForceMode.Impulse);
        rb.AddTorque(Random.insideUnitSphere * Random.Range(0.1f, fragmentTorqueMax), ForceMode.Impulse);

        // Network spawn
        NetworkServer.Spawn(fragment.gameObject);
        DebugLog($"Spawned networked stone fragment");
    }

    protected override GameObject GetReplacementPrefab()
    {
        // Return null - rock vanishes after spawning fragments (no stump)
        return null;
    }

    public override ResourceType GetResourceType() => ResourceType.Rock;
}