using UnityEngine;
using Mirror;
using System.Collections.Generic;

/// <summary>
/// WorldResourceManager - Central network authority for non-networked resources
/// Uses enum state for clean bush management
/// </summary>
public class WorldResourceManager : NetworkBehaviour
{
    public static WorldResourceManager Instance { get; private set; }
    [Header("Destruction Effects")]
    [SerializeField] private List<DestructionEffectData> destructionEffects = new List<DestructionEffectData>();

    // ==================== RESOURCE STATE STRUCT ====================
    [System.Serializable]
    public class DestructionEffectData
    {
        public string effectId;
        public ParticleSystem effectPrefab;
    }
    [System.Serializable]
    public struct ResourceState
    {
        public int berryCount;
        public int health;
        public MyBush.BushState bushState;
    }

    // Local registration cache (both server and client)
    private Dictionary<string, IWorldResource> registeredResources = new Dictionary<string, IWorldResource>();
    private Dictionary<string, ParticleSystem> effectLookup = new Dictionary<string, ParticleSystem>();

    // Server-side state tracking
    private Dictionary<string, ResourceState> serverStates = new Dictionary<string, ResourceState>();
    private void Start()
    {
        
        BuildEffectLookup();
    }
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ==================== REGISTRATION ====================
    public void RegisterResource(string uniqueId, IWorldResource resource)
    {
        if (string.IsNullOrEmpty(uniqueId)) return;

        if (!registeredResources.ContainsKey(uniqueId))
        {
            registeredResources[uniqueId] = resource;
            Debug.Log($"[{(isServer ? "SERVER" : "CLIENT")}] Registered: {uniqueId}");

            // Client requests current state from server
            if (!isServer)
            {
                CmdRequestResourceState(uniqueId);
            }
        }
    }
    private void BuildEffectLookup()
    {
        effectLookup.Clear();
        foreach (var effectData in destructionEffects)
        {
            if (!string.IsNullOrEmpty(effectData.effectId) && effectData.effectPrefab != null)
            {
                effectLookup[effectData.effectId.ToLower()] = effectData.effectPrefab;
            }
        }
        Debug.Log($"[WorldResourceManager] Loaded {effectLookup.Count} destruction effects");
    }

    [ClientRpc]
    public void RpcPlayDestructionEffect(Vector3 position, Quaternion rotation, string effectId)
    {
        if (string.IsNullOrEmpty(effectId))
        {
            Debug.LogWarning($"[{(isServer ? "HOST" : "CLIENT")}] Empty effectId provided");
            return;
        }

        string lookupKey = effectId.ToLower();

        if (effectLookup.TryGetValue(lookupKey, out ParticleSystem effectPrefab))
        {
            if (effectPrefab != null)
            {
                var effect = Instantiate(effectPrefab, position, rotation);

                // Auto-destroy after particle lifetime + 1 second buffer
                var main = effectPrefab.main;
                float lifetime = main.duration + main.startLifetime.constantMax + 1f;
                Destroy(effect.gameObject, lifetime);

                Debug.Log($"[{(isServer ? "HOST" : "CLIENT")}] Playing effect '{effectId}' at {position}");
            }
            else
            {
                Debug.LogWarning($"[{(isServer ? "HOST" : "CLIENT")}] Effect prefab is null for '{effectId}'");
            }
        }
        else
        {
            Debug.LogWarning($"[{(isServer ? "HOST" : "CLIENT")}] No effect found for ID: '{effectId}'. Available effects: {string.Join(", ", effectLookup.Keys)}");
        }
    }
    public void UnregisterResource(string uniqueId)
    {
        if (registeredResources.ContainsKey(uniqueId))
        {
            registeredResources.Remove(uniqueId);
        }
    }

    // ==================== SERVER INITIALIZATION ====================
    [Server]
    public void InitializeResourceState(string uniqueId, int berryCount, int health, MyBush.BushState state)
    {
        if (string.IsNullOrEmpty(uniqueId)) return;

        serverStates[uniqueId] = new ResourceState
        {
            berryCount = berryCount,
            health = health,
            bushState = state
        };

        Debug.Log($"[SERVER] Initialized {uniqueId}: berries={berryCount}, state={state}");
    }

    // ==================== STATE SYNC ====================
    [Command(requiresAuthority = false)]
    private void CmdRequestResourceState(string uniqueId, NetworkConnectionToClient sender = null)
    {
        if (sender == null) return;

        Debug.Log($"[SERVER] State request for {uniqueId}");

        if (serverStates.TryGetValue(uniqueId, out ResourceState state))
        {
            TargetSyncResourceState(sender, uniqueId, state.berryCount, state.health, state.bushState);
        }
        else
        {
            // Try to find and initialize
            MyBush bush = FindBushByUniqueId(uniqueId);
            if (bush != null)
            {
                int berries = bush.GetCurrentBerries();
                MyBush.BushState bushState = bush.GetCurrentState();
                InitializeResourceState(uniqueId, berries, 20, bushState);
                TargetSyncResourceState(sender, uniqueId, berries, 20, bushState);
            }
            else
            {
                Debug.LogWarning($"[SERVER] Bush {uniqueId} not found");
            }
        }
    }

    [TargetRpc]
    private void TargetSyncResourceState(NetworkConnection target, string uniqueId, int berryCount, int health, MyBush.BushState state)
    {
        if (isServer) return;

        Debug.Log($"[CLIENT] Received state for {uniqueId}: berries={berryCount}, state={state}");

        if (registeredResources.TryGetValue(uniqueId, out IWorldResource resource))
        {
            ResourceState newState = new ResourceState
            {
                berryCount = berryCount,
                health = health,
                bushState = state
            };
            resource.ApplyState(newState);
        }
    }

    // ==================== STATE UPDATES ====================
    [Server]
    public void UpdateResourceState(string uniqueId, int berryCount, int health, MyBush.BushState state)
    {
        if (string.IsNullOrEmpty(uniqueId)) return;

        ResourceState newState = new ResourceState
        {
            berryCount = berryCount,
            health = health,
            bushState = state
        };

        serverStates[uniqueId] = newState;

        Debug.Log($"[SERVER] Updated {uniqueId}: berries={berryCount}, hp={health}, state={state}");

        // ✅ Apply to host immediately (before RPC)
        if (registeredResources.TryGetValue(uniqueId, out IWorldResource resource))
        {
            resource.ApplyState(newState);
        }

        // Then broadcast to remote clients
        RpcUpdateResourceState(uniqueId, berryCount, health, state);
    }

    [ClientRpc]
    private void RpcUpdateResourceState(string uniqueId, int berryCount, int health, MyBush.BushState state)
    {
        // ✅ Apply to ALL clients INCLUDING host
        Debug.Log($"[{(isServer ? "HOST" : "CLIENT")}] RPC update for {uniqueId}: berries={berryCount}, state={state}");

        if (registeredResources.TryGetValue(uniqueId, out IWorldResource resource))
        {
            ResourceState newState = new ResourceState
            {
                berryCount = berryCount,
                health = health,
                bushState = state
            };
            resource.ApplyState(newState);
        }
        else
        {
            Debug.LogWarning($"[{(isServer ? "HOST" : "CLIENT")}] Resource {uniqueId} not registered yet");
        }
    }

    // ==================== UNIFIED HARVEST COMMAND ====================
    /// <summary>
    /// Single command that handles the harvest interaction
    /// Logic:
    /// 1. If bush has berries → Pick one berry
    /// 2. If bush is empty (no berries) → Break bush for sticks and destroy it
    /// 3. If bush is destroyed → Do nothing
    /// </summary>
    [Command(requiresAuthority = false)]
    public void CmdHarvestBush(string bushId, NetworkConnectionToClient sender = null)
    {
        if (sender == null) return;

        MyBush bush = FindBushByUniqueId(bushId);
        if (bush == null)
        {
            Debug.LogWarning($"[SERVER] Bush {bushId} not found");
            return;
        }

        var inventory = sender.identity?.GetComponentInChildren<InventoryData>();
        if (inventory == null)
        {
            Debug.LogWarning($"[SERVER] No inventory found for sender");
            return;
        }

        // ✅ DECISION TREE:
        MyBush.BushState currentState = bush.GetCurrentState();
        int berryCount = bush.GetCurrentBerries();

        if (currentState == MyBush.BushState.Destroyed)
        {
            Debug.LogWarning($"[SERVER] Bush {bushId} is destroyed, cannot harvest");
            return;
        }

        // Case 1: Bush has berries → Pick one berry
        if (berryCount > 0)
        {
            Debug.Log($"[SERVER] Picking berry from {bushId} ({berryCount} berries available)");
            bush.ServerHarvestBerry(inventory);
        }
        // Case 2: Bush is empty (no berries) → Break bush for sticks
        else if (currentState == MyBush.BushState.Empty)
        {
            Debug.Log($"[SERVER] Breaking empty bush {bushId} for sticks");
            bush.ServerBreakBush(inventory);
        }
        else
        {
            Debug.LogWarning($"[SERVER] Bush {bushId} in unexpected state: {currentState}");
        }
    }
    [ClientRpc]
    public void RpcDestroyTree(string treeId)
    {
        if (isServer) return; // Server already destroyed it locally

        Debug.Log($"[CLIENT] RPC destroy tree {treeId}");

        // Find and destroy the tree locally on client
        ChoppableBase[] trees = FindObjectsOfType<ChoppableBase>();
        foreach (var tree in trees)
        {
            if (tree != null && tree.UniqueId == treeId)
            {
                // Trigger client-side destruction
                tree.ClientSideDestroy();
                break;
            }
        }
    }
    // ==================== DAMAGE COMMAND ====================
    [Command(requiresAuthority = false)]
    public void CmdDamageResource(string resourceId, int damage, NetworkConnectionToClient sender = null)
    {
        ChoppableBase resource = FindResourceByUniqueId(resourceId);
        if (resource == null)
        {
            Debug.LogWarning($"[SERVER] Resource {resourceId} not found");
            return;
        }

        // ✅ Store the damager so we know who to give items to
        if (resource is MyBush bush && sender != null)
        {
            bush.SetLastDamager(sender);
        }

        resource.Damage(damage);
    }

    // ==================== HELPER METHODS ====================
    private MyBush FindBushByUniqueId(string uniqueId)
    {
        if (string.IsNullOrEmpty(uniqueId)) return null;

        // Check registered resources first (faster)
        if (registeredResources.TryGetValue(uniqueId, out IWorldResource resource) && resource is MyBush bush)
        {
            return bush;
        }

        // Fallback: search scene
        MyBush[] bushes = FindObjectsOfType<MyBush>();
        foreach (var b in bushes)
        {
            if (b.UniqueId == uniqueId)
            {
                return b;
            }
        }
        return null;
    }

    private ChoppableBase FindResourceByUniqueId(string uniqueId)
    {
        if (string.IsNullOrEmpty(uniqueId)) return null;

        ChoppableBase[] resources = FindObjectsOfType<ChoppableBase>();
        foreach (var resource in resources)
        {
            if (resource.UniqueId == uniqueId)
            {
                return resource;
            }
        }
        return null;
    }

    // ==================== QUERY ====================
    public bool TryGetResourceState(string uniqueId, out ResourceState state)
    {
        return serverStates.TryGetValue(uniqueId, out state);
    }

    public bool IsResourceDestroyed(string uniqueId)
    {
        if (serverStates.TryGetValue(uniqueId, out var state))
        {
            return state.bushState == MyBush.BushState.Destroyed;
        }
        return false;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}

// ==================== INTERFACE ====================
public interface IWorldResource
{
    string GetUniqueId();
    void ApplyState(WorldResourceManager.ResourceState state);
}