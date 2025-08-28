using UnityEngine;

public class MyBush : BaseResource, Iinteractable
{
    public enum BushType { BerryBush, FlowerBush, HerbBush }

    [Header("Bush Specific")]
    [SerializeField] private BushType bushType = BushType.BerryBush;
    [SerializeField] private int maxHarvestCount = 3;
    [SerializeField] private GameObject berryVisualMesh;

    [Header("Bush Drops")]
    [SerializeField] private Transform berryDropPrefab;
    [SerializeField] private Transform stickDropPrefab;
    [SerializeField] private Transform flowerDropPrefab;
    [SerializeField] private Transform herbDropPrefab;

    private int currentHarvestCount;
    private bool hasHarvestableItems = true;

    protected override void Awake()
    {
        base.Awake();
        currentHarvestCount = maxHarvestCount;
        UpdateVisuals();
    }

    protected override void InitializeHealth()
    {
        int healthAmount = bushType switch
        {
            BushType.BerryBush => 10,
            BushType.FlowerBush => 8,
            BushType.HerbBush => 12,
            _ => 10
        };

        healthSystem = new HealthSystem(healthAmount);
        resourceType = ResourceType.Bush;
    }

    public virtual void Interact()
    {
        if (!hasHarvestableItems || currentHarvestCount <= 0)
        {
            Debug.Log($"No more {bushType} to harvest.");
            return;
        }

        HarvestFromBush();
    }

    private void HarvestFromBush()
    {
        currentHarvestCount--;
        Debug.Log($"Harvested from {bushType}! Remaining: {currentHarvestCount}");

        // Spawn harvested item
        SpawnHarvestedItem();

        if (currentHarvestCount <= 0)
        {
            hasHarvestableItems = false;
        }

        UpdateVisuals();
    }

    private void SpawnHarvestedItem()
    {
        Transform dropPrefab = bushType switch
        {
            BushType.BerryBush => berryDropPrefab,
            BushType.FlowerBush => flowerDropPrefab,
            BushType.HerbBush => herbDropPrefab,
            _ => berryDropPrefab
        };

        if (dropPrefab != null)
        {
            Vector3 spawnPos = transform.position + Vector3.up * 1.5f;
            Instantiate(dropPrefab, spawnPos, Quaternion.identity);
        }
    }

    protected override void OnResourceDestroyed()
    {
        // Drop remaining harvestable items if any
        if (hasHarvestableItems && currentHarvestCount > 0)
        {
            SpawnHarvestedItem();
        }

        // Always drop some sticks
        int stickCount = Random.Range(1, 3);
        SpawnDrops(stickDropPrefab, stickCount, transform.position);

        DestroyResource();
    }

    private void UpdateVisuals()
    {
        if (berryVisualMesh != null)
        {
            berryVisualMesh.SetActive(hasHarvestableItems && currentHarvestCount > 0);
        }
    }

    protected override void ValidateComponents()
    {
        if (bushType == BushType.BerryBush && (berryDropPrefab == null || berryVisualMesh == null))
            Debug.LogWarning($"{name}: Berry bush missing berryDropPrefab or berryVisualMesh!");

        if (bushType == BushType.FlowerBush && flowerDropPrefab == null)
            Debug.LogWarning($"{name}: Flower bush missing flowerDropPrefab!");

        if (bushType == BushType.HerbBush && herbDropPrefab == null)
            Debug.LogWarning($"{name}: Herb bush missing herbDropPrefab!");

        if (stickDropPrefab == null)
            Debug.LogWarning($"{name}: stickDropPrefab not assigned!");
    }

    public override ResourceType GetResourceType() => ResourceType.Bush;

    // Public getters for bush state
    public bool HasHarvestableItems() => hasHarvestableItems;
    public int GetRemainingHarvests() => currentHarvestCount;
    public BushType GetBushType() => bushType;
}