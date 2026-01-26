using Mirror;
using UnityEngine;

public class MyTree : ChoppableBase
{
    [Header("Tree Spawns")]
    [SerializeField] private Transform treeLogPrefab;
    [SerializeField] private Transform treeStumpPrefab;
    [SerializeField] private string destructionEffectId = "tree_leaves";

    protected override int GetHealthAmount() => 30;

    protected override void SpawnChopResults()
    {
        // Only server spawns the networked log
        if (!NetworkServer.active) return;

        DebugLog("Server: Spawning networked log");

        if (treeLogPrefab != null)
        {
            Vector3 logPos = transform.position + transform.up * 0.2f;
            Quaternion logRot = Quaternion.Euler(
                UnityEngine.Random.Range(-2f, 2f),
                UnityEngine.Random.Range(0f, 360f),
                UnityEngine.Random.Range(-2f, 2f)
            );
            SpawnNetworkedObject(treeLogPrefab, logPos, logRot);
        }
    }

    /// <summary>
    /// Spawn stump locally on ALL clients (including host)
    /// Called automatically by ChoppableBase during destruction
    /// </summary>
    protected override void SpawnLocalStump()
    {
        if (treeStumpPrefab == null) return;

        var stumpObj = Instantiate(treeStumpPrefab, transform.position, transform.rotation);

        if (stumpObj.TryGetComponent<MyStump>(out var stump))
        {
            string stumpId = UniqueId + "_stump";
            stump.SetUniqueId(stumpId);
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

    protected override GameObject GetReplacementPrefab()
    {
        return null; // We handle stump manually
    }
}