// BuildManager.cs (Mirror-compatible)
// NOTE: Requires Mirror package. Attach this component on the PlayerMovement object (the object that has authority).
// If you put it on a non-player object, Commands won't be accepted by server by default.
using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class BuildManager : NetworkBehaviour
{
    [Header("Sub-systems")]
    public BuildPreviewSystem previewSystem;
    public BuildPlacementSystem placementSystem;
    public BuildDestroySystem destroySystem;

    [Header("World grid")]
    [SerializeField] public float cellHeight = 9.0f;
    [SerializeField] public float cellWidth = 9.0f;

    // local runtime
    private ItemData currentItem;
    public PlayerHoldingItem playerHolding;

    // Mirror: mapping from item id (string) -> spawnable prefab.
    // You must populate this list in inspector with the same prefabs used for worldPrefab.
    [Header("Mirror spawnable prefabs (map itemId -> prefab)")]
    public List<GameObject> spawnablePrefabs = new List<GameObject>();
    public List<string> spawnablePrefabItemIds = new List<string>();

    // Quick lookup at runtime
    private Dictionary<string, GameObject> prefabById = new Dictionary<string, GameObject>();

    private void Awake()
    {
        if (previewSystem == null) previewSystem = GetComponent<BuildPreviewSystem>();
        if (placementSystem == null) placementSystem = GetComponent<BuildPlacementSystem>();
        if (destroySystem == null) destroySystem = GetComponent<BuildDestroySystem>();

        if (previewSystem != null) previewSystem.Initialize(this);
        if (placementSystem != null) placementSystem.Initialize(this);
        if (destroySystem != null) destroySystem.Initialize(this);

        // build lookup
        prefabById.Clear();
        //Cursor.lockState = CursorLockMode.Locked;
        int n = Math.Min(spawnablePrefabs.Count, spawnablePrefabItemIds.Count);
        for (int i = 0; i < n; i++)
        {
            if (!string.IsNullOrEmpty(spawnablePrefabItemIds[i]) && spawnablePrefabs[i] != null)
                prefabById[spawnablePrefabItemIds[i]] = spawnablePrefabs[i];
        }
    }

    private void Update()
    {
        if (currentItem == null) return;

        if (currentItem.type == ItemType.Tool)
        {
            destroySystem.UpdateDestroy(currentItem);
        }
        else
        {
            placementSystem.UpdatePlacement(currentItem);
        }
    }

    public void SetCurrentItem(ItemData item, PlayerHoldingItem player)
    {
        currentItem = item;
        playerHolding = player;

        // Đồng bộ: xóa preview cũ + reset rotation
        if (previewSystem != null) previewSystem.ClearPreview();
        if (placementSystem != null) placementSystem.ResetRotation();

        // Nếu không còn cầm tool (hoặc item == null) => clear highlight destroy (tránh "treo")
        if (destroySystem != null)
        {
            if (item == null || item.type != ItemType.Tool)
                destroySystem.ClearHighlight();
        }

        if (item != null && item.type == ItemType.BuildingPart)
        {
            previewSystem.StartPreview(item);
        }
    }

    public void EndVisualisingObject()
    {
        if (previewSystem != null) previewSystem.ClearPreview();
    }

    // Called by placementSystem when player left-click to confirm placement.
    // We decide: if we are on server, place immediately; if on client, send Command.
    public void ConfirmPlacement(Vector3 pos, float rotY, ItemData obj, Transform previewAnchor)
    {
        if (isServer)
            placementSystem.ServerPlaceObject(pos, rotY, obj, playerHolding, previewAnchor, this);
        else
            CmdRequestPlace(pos, rotY, obj.id, previewAnchor ? previewAnchor.GetComponent<NetworkIdentity>()?.netId ?? 0 : 0);
        playerHolding.OnPlaced();
    }

    [Command]
    void CmdRequestPlace(Vector3 pos, float rotY, int itemId, uint anchorNetId)
    {
        ItemData item = ItemDatabase.Get(itemId);
        if (item == null) return;

        Transform anchor = null;
        if (anchorNetId != 0 && NetworkServer.spawned.TryGetValue(anchorNetId, out var nid))
            anchor = nid.transform;

        placementSystem.ServerPlaceObject(pos, rotY, item, null, anchor, this);
    }


    // Client -> Server request to destroy by GUID.
    // The client shouldn't be able to secretly destroy others' stuff: server must validate permissions.
    public void RequestDestroy(BuildtObject target)
    {
        if (isServer) Destroy(target.gameObject);
        else CmdRequestDestroy(target.GetComponent<NetworkIdentity>()?.netId ?? 0);
    }

    [Command]
    void CmdRequestDestroy(uint netId)
    {
        if (NetworkServer.spawned.TryGetValue(netId, out var nid))
            NetworkServer.Destroy(nid.gameObject);
    }

    // Utility: find build object by GUID in the scene
    private BuildtObject FindBuildObjectByGuid(string guid)
    {
        var all = FindObjectsOfType<BuildtObject>();
        foreach (var b in all) if (b.guid == guid) return b;
        return null;
    }
}
