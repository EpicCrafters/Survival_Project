using UnityEngine;

public class MyTree : ChoppableBase
{
    [Header("Tree Spawns")]
    [SerializeField] private Transform treeLogPrefab;
    [SerializeField] private Transform treeStumpPrefab;

    protected override int GetHealthAmount() => 30;

    protected override void SpawnChopResults()
    {
        DebugLog("Tree chopped - spawning Log and Stump");

        // Spawn Log (networked)
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

        // Spawn Stump (local only - not networked)
        if (treeStumpPrefab != null)
        {
            var stumpObj = Instantiate(treeStumpPrefab, transform.position, transform.rotation);

            // Give stump a uniqueId so ItemHitBox can track it
            if (stumpObj.TryGetComponent<MyStump>(out var stump))
            {
                string stumpId = System.Guid.NewGuid().ToString("N");
                stump.SetUniqueId(stumpId);
            }
        }
    }

    protected override GameObject GetReplacementPrefab()
    {
        // Return null - we don't want replacement system, we spawn manually
        return null;
    }
}
