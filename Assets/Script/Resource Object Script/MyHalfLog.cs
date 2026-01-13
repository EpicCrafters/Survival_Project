using UnityEngine;

public class MyHalfLog : NetworkedChoppable
{
    [Header("Half Log Spawns")]
    [SerializeField] private ItemData stickPrefab; // Changed from ItemData to Transform
    [SerializeField] private int minSticks = 2;
    [SerializeField] private int maxSticks = 3;
    [SerializeField] private float dropRadius = 0.5f;

    protected override int GetHealthAmount() => 15;

    protected override void OnResourceDestroyed()
    {
        if (stickPrefab == null) return;

        int stickCount = UnityEngine.Random.Range(minSticks, maxSticks + 1);
        DebugLog($"Half log chopped - spawning {stickCount} sticks");

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
}