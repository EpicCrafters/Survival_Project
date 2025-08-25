#if UNITY_EDITOR
using UnityEngine;

public class MyTree : MonoBehaviour, IDamageable, IMinenable
{
    public enum Type { Tree, Log, LogHaft, Stump }
    public enum Size { Small, Medium, Large }

    [SerializeField] private Size treeSize;
    [SerializeField] private Type treeType;
    [SerializeField] private Transform fxTreeDestroyed;
    public int itemDrop;
    [SerializeField] private Transform particleSpawnPosition;

    [SerializeField] private Transform treeLog;
    [SerializeField] private Transform treeLogHalf;
    [SerializeField] private Transform treeStump;
    [SerializeField] private Transform stickPrefab;

    private HealthSystem healthSystem;

    [HideInInspector] public UVMapTreeSpawner.TreeData treeData;
    [HideInInspector] public UVMapTreeSpawner spawner;

    private void Awake()
    {
        int healthAmount = treeType switch
        {
            Type.Tree => 30,
            _ => 50
        };
        healthSystem = new HealthSystem(healthAmount);
        healthSystem.OnDead += HealthSystem_OnDead;
    }

    private void HealthSystem_OnDead()
    {
        if (treeData != null)
            treeData.isCut = true; // Mark the tree as cut

        switch (treeType)
        {
            case Type.Tree:
                Instantiate(treeLog, transform.position + transform.up * 0.2f, Quaternion.Euler(Random.Range(-1.5f, 1.5f), 0, Random.Range(-1.5f, 1.5f)));
                Instantiate(treeStump, transform.position, transform.rotation);
                Instantiate(fxTreeDestroyed, particleSpawnPosition.position, particleSpawnPosition.rotation);
                break;

            case Type.Log:
                int halfLogCount = treeSize switch
                {
                    Size.Small => 2,
                    Size.Medium => 4,
                    Size.Large => 6
                };
                float halfLogOffset = 13.0f;
                for (int i = 0; i < halfLogCount; i++)
                {
                    Vector3 offset = transform.up * halfLogOffset * i;
                    Quaternion rotation = Quaternion.LookRotation(transform.forward, transform.up) * Quaternion.Euler(0, Random.Range(0f, 360f), 0);
                    Instantiate(treeLogHalf, transform.position + offset, rotation);
                }
                break;

            case Type.LogHaft:
            case Type.Stump:
                for (int i = 0; i < itemDrop; i++)
                {
                    Vector3 offset = new Vector3(Random.Range(-0.2f, 0.2f), 0.1f, Random.Range(-0.2f, 0.2f));
                    Quaternion randomRot = Quaternion.Euler(0, Random.Range(0, 360), 0);
                    Instantiate(stickPrefab, transform.position + offset, randomRot);
                }
                break;
        }

        Destroy(gameObject);
    }

    public void Damage(int amount)
    {
        healthSystem.Damage(amount);
    }

    public ResourceType GetResourceType() => ResourceType.Tree;
    public HealthSystem GetHealthSystem() => healthSystem;
}
#endif