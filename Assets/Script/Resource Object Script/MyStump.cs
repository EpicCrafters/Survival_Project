using Mirror;
using UnityEngine;

public class MyStump : ChoppableBase
{
    [Header("Stump Spawns")]
    [SerializeField] private ItemData stickPrefab;
    [SerializeField] private int minSticks = 3;
    [SerializeField] private int maxSticks = 5;
    [SerializeField] private float dropRadius = 0.5f;

    protected override int GetHealthAmount() => 20;

    protected override void SpawnChopResults()
    {
        if (stickPrefab == null) return;

        int stickCount = UnityEngine.Random.Range(minSticks, maxSticks + 1);
        DebugLog($"Stump chopped - spawning {stickCount} sticks");

        // Only server spawns networked sticks
        if (!NetworkServer.active) return;

        for (int i = 0; i < stickCount; i++)
        {
            Vector3 randomOffset = new Vector3(
                UnityEngine.Random.Range(-dropRadius, dropRadius),
                0.1f,
                UnityEngine.Random.Range(-dropRadius, dropRadius)
            );

            Quaternion randomRot = Quaternion.Euler(0, UnityEngine.Random.Range(0f, 360f), 0);

            SpawnNetworkedObject(stickPrefab.worldPrefab.transform, transform.position + randomOffset, randomRot);
        }
    }

    protected override GameObject GetReplacementPrefab()
    {
        return null; // Stump disappears completely when chopped
    }
}
