using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class Spawn : MonoBehaviour
{
    [Header("Spawn Settings")]
    public GameObject prefabToSpawn;   // Prefab to spawn
    public int amountToSpawn = 1;      // Number of prefabs to spawn
    public Vector3 spawnArea = Vector3.one; // Area in which objects can spawn randomly

    // Method to spawn objects
    public void SpawnObjects()
    {
        if (prefabToSpawn == null)
        {
            Debug.LogWarning("Prefab is not assigned!");
            return;
        }

        for (int i = 0; i < amountToSpawn; i++)
        {
            Vector3 randomPos = transform.position + new Vector3(
                Random.Range(-spawnArea.x / 2, spawnArea.x / 2),
                Random.Range(-spawnArea.y / 2, spawnArea.y / 2),
                Random.Range(-spawnArea.z / 2, spawnArea.z / 2)
            );

            Instantiate(prefabToSpawn, randomPos, Quaternion.identity);
        }
    }
}

#if UNITY_EDITOR
// Custom Inspector to add a button
[CustomEditor(typeof(Spawn))]
public class SpawnerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        Spawn spawner = (Spawn)target;
        if (GUILayout.Button("Spawn Now"))
        {
            spawner.SpawnObjects();
        }
    }
}
#endif
