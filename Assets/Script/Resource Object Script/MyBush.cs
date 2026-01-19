using Mirror;
using UnityEngine;

/// <summary>
/// MyBush - harvestable bush that gives berries directly to player inventory
/// - Interact harvests berries and adds to inventory (max 3 harvests)
/// - Automatically stacks with existing items
/// - Bush remains in world after all berries are harvested
/// </summary>
public class MyBush : ChoppableBase, Iinteractable
{
    public enum BushType { BerryBush, FlowerBush, HerbBush }

    [Header("Bush Specific")]
    [SerializeField] private BushType bushType = BushType.BerryBush;
    [SerializeField] private int maxHarvestCount = 3; // Number of times player can harvest
    [SerializeField] private GameObject berryVisualMesh;

    [Header("Item Reference")]
    [SerializeField] private ItemData harvestItemData; // Direct reference to the ItemData ScriptableObject

    [Header("Respawn Settings (Optional)")]
    [SerializeField] private bool canRespawn = true;
    [SerializeField] private float respawnTime = 60f; // Time in seconds to refill berries

    // Runtime state (server-side only)
    private int currentHarvestCount;
    private float respawnTimer = 0f;

    protected override void Awake()
    {
        base.Awake();

        // Initialize harvest count
        currentHarvestCount = maxHarvestCount;
        UpdateVisuals();
    }

    private void Update()
    {
        // Handle respawn on server
        if (NetworkServer.active && canRespawn && currentHarvestCount < maxHarvestCount)
        {
            respawnTimer += Time.deltaTime;
            if (respawnTimer >= respawnTime)
            {
                currentHarvestCount = maxHarvestCount;
                respawnTimer = 0f;
                UpdateVisuals();
                DebugLog("Bush berries respawned!");
            }
        }
    }

    protected override int GetHealthAmount()
    {
        // Bush doesn't take damage anymore, but we keep this for base class
        return 9999;
    }

    protected override void SpawnChopResults()
    {
        // Bush no longer drops items when destroyed
    }

    protected override GameObject GetReplacementPrefab()
    {
        // Bush remains in world
        return null;
    }

    public override ResourceType GetResourceType() => ResourceType.Bush;

    // ================= Interact Logic =================

    public void Interact(PlayerHoldingItem playerHoldingItem)
    {
        DebugLog("Interact called on bush");

        if (currentHarvestCount <= 0)
        {
            DebugLog("No berries left to harvest");
            return;
        }

        // Validate harvest item data
        if (harvestItemData == null)
        {
            DebugLog("ERROR: harvestItemData is not assigned in Inspector!");
            return;
        }

        // Get player's inventory - try multiple methods
        if (playerHoldingItem == null)
        {
            DebugLog("PlayerHoldingItem is null");
            return;
        }

        // Try to get InventoryData from the same GameObject
        InventoryData inventory = playerHoldingItem.GetComponent<InventoryData>();

        // If not found, try to get it from parent or children
        if (inventory == null)
        {
            inventory = playerHoldingItem.GetComponentInParent<InventoryData>();
        }

        if (inventory == null)
        {
            inventory = playerHoldingItem.GetComponentInChildren<InventoryData>();
        }

        if (inventory == null)
        {
            DebugLog("Could not find InventoryData component anywhere on player hierarchy");
            return;
        }

        // Verify the item exists in the database
        int itemId = harvestItemData.id;
        ItemData itemData = ItemDatabase.Get(itemId);
        if (itemData == null)
        {
            DebugLog($"ERROR: Item ID {itemId} not found in ItemDatabase!");
            return;
        }

        DebugLog($"Item found in database: {itemData.itemName} (ID: {itemId})");

        // Add berry to inventory with stacking (this is a Command that runs on server)
        inventory.CmdAddItemWithStacking(itemId, 1);

        // Only decrease count on server
        if (NetworkServer.active)
        {
            currentHarvestCount--;
            respawnTimer = 0f; // Reset respawn timer
            UpdateVisuals();
            DebugLog($"Harvested 1 {bushType}. Remaining harvests: {currentHarvestCount}");
        }
    }

    private void UpdateVisuals()
    {
        if (berryVisualMesh != null)
        {
            // Show berries only when harvest count > 0
            berryVisualMesh.SetActive(currentHarvestCount > 0);
        }
    }

    protected override void ValidateComponents()
    {
        if (berryVisualMesh == null)
            Debug.LogWarning($"{name}: berryVisualMesh not assigned!");

        if (harvestItemData == null)
            Debug.LogWarning($"{name}: harvestItemData not assigned! Please assign the ItemData ScriptableObject.");
    }

    // ================= Public Getters =================

    public int GetRemainingHarvests() => currentHarvestCount;
    public BushType GetBushType() => bushType;
    public bool HasBerries() => currentHarvestCount > 0;

    public void Interact()
    {
        // This empty method satisfies the interface but won't be called
        // The version with PlayerHoldingItem parameter will be used instead
    }
}