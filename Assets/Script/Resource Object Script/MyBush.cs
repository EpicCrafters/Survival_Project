using Mirror;
using UnityEngine;

/// <summary>
/// MyBush - simple harvestable bush.
/// - Players call Interact() to harvest (decrements remaining harvests and spawns a drop).
/// - On destroy, any remaining harvest items are dropped (one per remaining harvest).
/// - On destroy we call RequestDestroyAndReplace(null) so BaseResource/ResourceManager can handle authoritative flows;
///   if no manager exists we will fall back to local destroy (handled by BaseResource).
/// </summary>
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

    // runtime
    private int currentHarvestCount;
    private bool hasHarvestableItems = true;

    protected override void Awake()
    {
        base.Awake();

        // defensive clamp: ensure sensible starting values
        currentHarvestCount = Mathf.Max(0, maxHarvestCount);
        hasHarvestableItems = currentHarvestCount > 0;

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

    private void HarvestFromBush()
    {
        // decrement and spawn one harvested item
        currentHarvestCount = Mathf.Max(0, currentHarvestCount - 1);
        Debug.Log($"Harvested from {bushType}! Remaining: {currentHarvestCount}");

        SpawnHarvestedItem();

        if (currentHarvestCount <= 0)
        {
            hasHarvestableItems = false;
        }

        UpdateVisuals();
    }

    protected override void OnResourceDestroyed()
    {
        // --- CRITICAL: Only run visual effects on server ---
        if (!NetworkServer.active)
        {
            Debug.Log($"{name}: OnResourceDestroyed called on client, skipping drops (server will handle)");
            return;
        }

        // Drop remaining harvest items if any (spawn one per remaining harvest)
        if (hasHarvestableItems && currentHarvestCount > 0)
        {
            int remaining = currentHarvestCount;
            Debug.Log($"{name}: Dropping {remaining} remaining harvestable item(s) on destroy.");
            for (int i = 0; i < remaining; i++)
            {
                SpawnHarvestedItem();
            }
        }

        // Always drop some sticks (1..2)
        int stickCount = Random.Range(1, 3);
        if (stickDropPrefab != null)
        {
            SpawnDrops(stickDropPrefab, stickCount, transform.position);
        }
        else
        {
            Debug.LogWarning($"{name}: stickDropPrefab not assigned - cannot spawn sticks on destroy.");
        }

        // Ask base class to handle authoritative destroy/replace
        try
        {
            RequestDestroyAndReplace(null);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"{name}: Exception while requesting destroy/replace: {ex}. Falling back to local destroy.");
            DestroyResource();
        }
    }

    /// <summary>
    /// Spawn one harvested item according to bush type.
    /// </summary>
    private void SpawnHarvestedItem()
    {
        Transform dropPrefab = bushType switch
        {
            BushType.BerryBush => berryDropPrefab,
            BushType.FlowerBush => flowerDropPrefab,
            BushType.HerbBush => herbDropPrefab,
            _ => berryDropPrefab
        };

        if (dropPrefab == null)
        {
            Debug.LogWarning($"{name}: No drop prefab assigned for {bushType} (cannot spawn harvested item).");
            return;
        }

        Vector3 spawnPos = transform.position + Vector3.up * 1.0f;
        var drop = Instantiate(dropPrefab, spawnPos, Quaternion.identity);

        // Network spawn the drop
        NetworkServer.Spawn(drop.gameObject);
    }

    // Also update the Interact method to be server-authoritative:
    public virtual void Interact()
    {
        // Do not allow interactions while the object is already being destroyed/removed.
        if (isDestroyed || isBeingDestroyed)
        {
            Debug.Log($"{name}: Cannot interact - bush is being removed.");
            return;
        }

        if (!hasHarvestableItems || currentHarvestCount <= 0)
        {
            Debug.Log($"No more {bushType} to harvest.");
            return;
        }

        // If we're a client, we should send a command to the server
        if (!NetworkServer.active)
        {
            // You'll need to add a NetworkBehaviour component or use existing network system
            Debug.Log($"{name}: Client attempting to harvest bush - needs network command.");
            return;
        }

        HarvestFromBush();
    }

    private void UpdateVisuals()
    {
        if (berryVisualMesh != null)
        {
            // only show berry visual when harvestable items exist
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

    public void Interact(PlayerHoldingItem playerHoldingItem)
    {
        throw new System.NotImplementedException();
    }
}
