using UnityEngine;
using System.Collections;
using Mirror;

/// <summary>
/// Simplified MyBush - NOT a NetworkBehaviour
/// All state changes go through WorldResourceManager
/// All inventory additions go through InventoryData.ServerAddItem
/// </summary>
public class MyBush : ChoppableBase, Iinteractable, IWorldResource
{
    // ==================== BUSH STATE ENUM ====================
    public enum BushState
    {
        Full,           // Has berries
        Empty,          // No berries, can be broken
        Destroyed,      // Destroyed/respawning
    }

    [Header("Bush Settings")]
    [SerializeField] private int maxBerries = 3;
    [SerializeField] private GameObject berryVisualMesh;
    [SerializeField] private int bushHealth = 20;

    [Header("Item Drops")]
    [SerializeField] private ItemData berryItemData;
    [SerializeField] private ItemData stickItemData;
    [SerializeField] private int minSticks = 1;
    [SerializeField] private int maxSticks = 3;
    [SerializeField] private float dropRadius = 0.5f;

    [Header("Respawn Settings")]
    [SerializeField] private bool canRespawn = true;
    [SerializeField] private float respawnTime = 120f;

    // ==================== LOCAL STATE ====================
    private BushState currentState = BushState.Full;
    private int currentBerries;
    private Coroutine respawnCoroutine;
    private bool isBeingChopped = false;
    private NetworkConnectionToClient lastDamager;

    // ==================== INITIALIZATION ====================
    protected override void Awake()
    {
        base.Awake();

        if (string.IsNullOrEmpty(UniqueId))
        {
            GenerateUniqueIdIfMissing();
        }

        currentBerries = maxBerries;
    }

    private void Start()
    {
        // Register on both server and client
        if (WorldResourceManager.Instance != null)
        {
            WorldResourceManager.Instance.RegisterResource(UniqueId, this);

            // Server initializes state
            if (NetworkServer.active)
            {
                WorldResourceManager.Instance.InitializeResourceState(
                    UniqueId,
                    currentBerries,
                    bushHealth,
                    BushState.Full
                );
            }
        }

        UpdateVisuals();
    }

    // ==================== INTERACTION ====================
    public void Interact(PlayerHoldingItem playerHoldingItem)
    {
        if (playerHoldingItem == null) return;
        if (WorldResourceManager.Instance == null) return;

        bool isHoldingAxe = IsPlayerHoldingAxe(playerHoldingItem);

        if (isHoldingAxe)
        {
            isBeingChopped = true;
            return;
        }

        // Single unified command - manager decides what to do
        WorldResourceManager.Instance.CmdHarvestBush(UniqueId);
    }

    // ==================== SERVER-SIDE ACTIONS ====================
    /// <summary>
    /// Called by WorldResourceManager on server
    /// Picks one berry and updates state
    /// Uses centralized InventoryData.ServerAddItem
    /// </summary>
    [Server]
    public void ServerHarvestBerry(InventoryData inventory)
    {
        if (currentBerries <= 0 || berryItemData == null || inventory == null)
        {
            Debug.LogWarning($"[SERVER] Cannot harvest berry from {UniqueId}");
            return;
        }

        // ✅ USE CENTRALIZED INVENTORY METHOD
        int itemsAdded = inventory.ServerAddItem(berryItemData.id, 1);

        if (itemsAdded > 0)
        {
            currentBerries--;

            // Update state: Still Full if berries remain, Empty if all picked
            BushState newState = currentBerries > 0 ? BushState.Full : BushState.Empty;

            WorldResourceManager.Instance.UpdateResourceState(
                UniqueId,
                currentBerries,
                healthSystem?.GetHealth() ?? bushHealth,
                newState
            );

            DebugLog($"[SERVER] Harvested berry. Remaining: {currentBerries}, State: {newState}");
        }
        else
        {
            // Inventory full - just don't pick the berry
            DebugLog($"[SERVER] Inventory full, cannot harvest berry");
        }
    }

    /// <summary>
    /// Called when player interacts with empty bush (no berries)
    /// Gives sticks and destroys the bush
    /// Uses centralized InventoryData.ServerAddItem
    /// </summary>
    [Server]
    public void ServerBreakBush(InventoryData inventory)
    {
        if (stickItemData == null || inventory == null) return;

        // Mark as being destroyed FIRST to prevent OnResourceDestroyed from firing
        isBeingDestroyed = true;

        // Give sticks to player using centralized method
        int stickCount = Random.Range(minSticks, maxSticks + 1);

        // ✅ USE CENTRALIZED INVENTORY METHOD
        int itemsAdded = inventory.ServerAddItem(stickItemData.id, stickCount);

        DebugLog($"[SERVER] Broke empty bush, added {itemsAdded}/{stickCount} sticks to inventory");

        // Destroy bush (with optional respawn) - NO item spawning
        DestroyBushDirect(allowRespawn: canRespawn);
    }

    // ==================== DAMAGE & DESTROY ====================
    public override void Damage(int amount)
    {
       
        base.Damage(amount);

        if (NetworkServer.active && WorldResourceManager.Instance != null)
        {
            WorldResourceManager.Instance.UpdateResourceState(
                UniqueId,
                currentBerries,
                healthSystem?.GetHealth() ?? 0,
                currentState
            );
        }
    }

    protected override void OnResourceDestroyed()
    {
        if (isBeingDestroyed || isDestroyed) return;
        if (!NetworkServer.active) return;

        DebugLog("[SERVER] Bush chopped down - spawning items to world");

        // Mark as being destroyed to prevent double-calls
        isBeingDestroyed = true;

        // ✅ GET AUTHORITATIVE STATE FROM MANAGER
        int authoritativeBerryCount = currentBerries;

        if (WorldResourceManager.Instance != null &&
            WorldResourceManager.Instance.TryGetResourceState(UniqueId, out var serverState))
        {
            // Use the manager's authoritative berry count
            authoritativeBerryCount = serverState.berryCount;
            DebugLog($"[SERVER] Using authoritative berry count from manager: {authoritativeBerryCount}");
        }
        else
        {
            DebugLog($"[SERVER] Using local berry count (manager state not found): {currentBerries}");
        }

        // When destroyed by damage/chopping: ALWAYS spawn items to world
        // Spawn sticks
        SpawnSticks();
        DebugLog($"[SERVER] Spawned sticks to world");

        // Spawn remaining berries if any (using authoritative count)
        if (authoritativeBerryCount > 0)
        {
            SpawnBerries(authoritativeBerryCount);
            DebugLog($"[SERVER] Spawned {authoritativeBerryCount} berries to world");
        }
        else
        {
            DebugLog($"[SERVER] No berries to spawn (count: {authoritativeBerryCount})");
        }

        DestroyBushDirect(allowRespawn: true);
    }


    // ==================== DAMAGER TRACKING ====================
    [Server]
    public void SetLastDamager(NetworkConnectionToClient damager)
    {
        lastDamager = damager;
    }

    // ==================== DESTROY & RESPAWN ====================
    /// <summary>
    /// Direct destruction without triggering OnResourceDestroyed callback
    /// Used when we've already handled item drops manually
    /// </summary>
    [Server]
    private void DestroyBushDirect(bool allowRespawn)
    {
        currentBerries = 0;
        currentState = BushState.Destroyed;
        lastDamager = null;

        // Set health to 0 WITHOUT triggering the death callback
        if (healthSystem != null)
        {
            healthSystem.SetHealth(0);
        }

        WorldResourceManager.Instance.UpdateResourceState(
            UniqueId,
            currentBerries,
            0,
            BushState.Destroyed
        );

        if (allowRespawn && canRespawn)
        {
            if (respawnCoroutine != null)
            {
                StopCoroutine(respawnCoroutine);
            }
            respawnCoroutine = StartCoroutine(RespawnBush());
        }
        else
        {
            isDestroyed = true;
            gameObject.SetActive(false);
        }
    }

    [Server]
    private IEnumerator RespawnBush()
    {
        gameObject.SetActive(false);
        yield return new WaitForSeconds(respawnTime);

        currentBerries = maxBerries;
        currentState = BushState.Full;

        if (healthSystem != null)
        {
            healthSystem.SetHealth(bushHealth);
        }

        isBeingChopped = false;
        isBeingDestroyed = false;
        isDestroyed = false;

        WorldResourceManager.Instance.UpdateResourceState(
            UniqueId,
            currentBerries,
            bushHealth,
            BushState.Full
        );

        gameObject.SetActive(true);
        UpdateVisuals();

        DebugLog("[SERVER] Bush respawned!");
        respawnCoroutine = null;
    }

    // ==================== SPAWN LOOT ====================
    [Server]
    private void SpawnSticks()
    {
        if (stickItemData == null) return;

        int stickCount = Random.Range(minSticks, maxSticks + 1);
        SpawnItems(stickItemData, stickCount);
    }

    [Server]
    private void SpawnBerries(int count)
    {
        if (berryItemData == null || count <= 0) return;
        SpawnItems(berryItemData, count);
    }

    /// <summary>
    /// Generic method to spawn items in the world
    /// </summary>
    [Server]
    private void SpawnItems(ItemData itemData, int count)
    {
        if (itemData == null || count <= 0) return;

        for (int i = 0; i < count; i++)
        {
            Vector3 offset = new Vector3(
                Random.Range(-dropRadius, dropRadius),
                0.1f,
                Random.Range(-dropRadius, dropRadius)
            );
            Quaternion rot = Quaternion.Euler(0, Random.Range(0f, 360f), 0);

            SpawnNetworkedObject(itemData.worldPrefab.transform, transform.position + offset, rot);
        }
    }

    // ==================== WORLD RESOURCE INTERFACE ====================
    public string GetUniqueId() => UniqueId;

    /// <summary>
    /// Called by WorldResourceManager when state updates from server
    /// </summary>
    public void ApplyState(WorldResourceManager.ResourceState state)
    {
        currentBerries = state.berryCount;
        currentState = state.bushState;

        if (healthSystem != null)
        {
            healthSystem.SetHealth(state.health);
        }

        // Handle destruction state
        if (state.bushState == BushState.Destroyed)
        {
            gameObject.SetActive(false);
        }
        else
        {
            gameObject.SetActive(true);
        }

        // FORCE visual update immediately
        UpdateVisuals();

        string role = NetworkServer.active ? "HOST" : "CLIENT";
        string stateStr = state.bushState == BushState.Full ? "Full (has berries)" :
                         state.bushState == BushState.Empty ? "Empty (no berries)" : "Destroyed";
        Debug.Log($"[{role}] Applied state: {currentBerries} berries, {state.health} hp, {stateStr}, Berry Mesh Active: {berryVisualMesh?.activeSelf}");
    }

    // ==================== GETTERS ====================
    public int GetCurrentBerries() => currentBerries;
    public BushState GetCurrentState() => currentState;

    // ==================== HELPERS ====================
    private bool IsPlayerHoldingAxe(PlayerHoldingItem playerHoldingItem)
    {
        if (playerHoldingItem == null || !playerHoldingItem.IsHolding())
            return false;

        ItemData heldItemData = playerHoldingItem.ItemData;

        if (heldItemData == null)
        {
            GameObject heldObject = playerHoldingItem.GetCurrentHeldObject();
            if (heldObject != null && heldObject.TryGetComponent<Item>(out var heldItem))
                heldItemData = heldItem.itemData;
        }

        if (heldItemData == null || heldItemData.type != ItemType.Tool)
            return false;

        return heldItemData.tool.toolType == ToolType.Axe;
    }

    private void UpdateVisuals()
    {
        if (berryVisualMesh != null)
        {
            // Show berries only when count > 0
            // Hide berries when empty (but bush still exists)
            berryVisualMesh.SetActive(currentBerries > 0);
        }
    }

    // ==================== ABSTRACT IMPLEMENTATIONS ====================
    protected override int GetHealthAmount() => bushHealth;
    protected override void SpawnChopResults() { }
    protected override GameObject GetReplacementPrefab() => null;
    public override ResourceType GetResourceType() => ResourceType.Bush;
    public void Interact() { }

    protected override void ValidateComponents()
    {
        if (berryVisualMesh == null)
            Debug.LogWarning($"{name}: berryVisualMesh not assigned!");
        if (berryItemData == null)
            Debug.LogWarning($"{name}: berryItemData not assigned!");
        if (stickItemData == null)
            Debug.LogWarning($"{name}: stickItemData not assigned!");
    }

    // ==================== CLEANUP ====================
    protected override void OnDestroy()
    {
        base.OnDestroy();

        if (WorldResourceManager.Instance != null)
        {
            WorldResourceManager.Instance.UnregisterResource(UniqueId);
        }

        if (respawnCoroutine != null)
        {
            StopCoroutine(respawnCoroutine);
        }
    }
}