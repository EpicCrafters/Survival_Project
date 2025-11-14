using Mirror;
using UnityEngine;

public class PlayerHoldingItem : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] public Transform holdingPoint;
    [SerializeField] private ItemPlacer itemPlacer;
    [SerializeField] private WeaponAnimatorHandler weaponHandler;

    private GameObject currentHoldingItem;
    private bool isHolding;
    private bool isWeapon;
    public ItemData ItemData { get; private set; }
    public bool buildingType = false;

    [SyncVar(hook = nameof(OnItemChanged))]
    private int currentItemId = 0;

    // ===========================================================
    // Called when player wants to hold an item
    public void HoldingItem(ItemData itemData)
    {
        if (!isLocalPlayer) return;

        int newItemId = (itemData == null) ? 0 : itemData.id;

        // If already holding the same item, skip
        if (currentItemId == newItemId && currentHoldingItem != null)
            return;

        // Update locally first for responsiveness
        UpdateHeldItem(newItemId);

        // Send to server to sync with other clients
        CmdSetHeldItem(newItemId);
    }

    // ===========================================================
    [Command]
    private void CmdSetHeldItem(int itemId)
    {
        
        currentItemId = itemId;
    }

    // ===========================================================
    // Hook called when SyncVar changes (runs on ALL clients)
    private void OnItemChanged(int oldId, int newId)
    {
        

        // Local player already updated in HoldingItem(), skip to avoid double processing
        if (isLocalPlayer)
        {
            
            return;
        }

        // Update for remote clients
        UpdateHeldItem(newId);
    }

    // ===========================================================
    // Core method to update held item (used by both local and remote)
    private void UpdateHeldItem(int itemId)
    {
       

        // Clear current item
        ClearHeldItem();

        // Create new item if not empty
        if (itemId != 0)
        {
            CreateHeldItem(itemId);
        }
    }

    // ===========================================================
    // Create the held item visual
    private void CreateHeldItem(int itemId)
    {
        ItemData data = ItemDatabase.Get(itemId);
        if (data == null)
        {
            
            return;
        }

        GameObject prefab = data.heldPrefab ?? data.worldPrefab;
        if (prefab == null)
        {
            
            return;
        }

        GameObject newItem = Instantiate(prefab, holdingPoint);
        newItem.transform.localPosition = Vector3.zero;
        newItem.transform.localRotation = Quaternion.identity;
        newItem.transform.localScale = Vector3.one;

        currentHoldingItem = newItem;
        ItemData = data;
        isHolding = true;

        // Attach ItemHeld component
        var heldComp = newItem.GetComponent<ItemHeld>() ?? newItem.AddComponent<ItemHeld>();
        heldComp.Init(data, this);

        // Disable physics
        if (newItem.TryGetComponent<Collider>(out var col))
            col.enabled = false;

        if (newItem.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        // Set weapon animation ONLY for local player
        if (data.type == ItemType.Weapon && isLocalPlayer)
        {
            isWeapon = true;
            weaponHandler?.EquipWeapon(data);
        }

        
    }

    // ===========================================================
    // Clear held item
    private void ClearHeldItem()
    {
        

        if (currentHoldingItem != null)
        {
            Destroy(currentHoldingItem);
            currentHoldingItem = null;
        }

        isHolding = false;
        isWeapon = false;
        ItemData = null;

        // Cancel placement/building mode
        if (itemPlacer != null)
        {
            if (!buildingType)
                itemPlacer.CancelPlacing();
            else
            {
               // BuildManager.Instance?.SetCurrentItem(null, this);
                //BuildManager.Instance?.EndVisualisingObject();
            }
        }

        buildingType = false;
    }

    // ===========================================================
    // Public Clear method (for backwards compatibility)
    public void Clear()
    {
        if (!isLocalPlayer) return;

        HoldingItem(null);
    }

    // ===========================================================
    // Called when player places an item
    public void OnPlaced()
    {
        if (ItemData == null) return;

        // Remove item from inventory
        bool removed = InventoryManager.instance.RemoveItem(ItemData, 1);
        if (!removed)
        {
            Clear();
            return;
        }

        // Clear hand if no more items
        if (InventoryManager.instance.GetItemCount(ItemData) == 0)
            Clear();
    }

    // ===========================================================
    // Refresh held item when inventory changes
    public void RefreshHoldingItem(ItemData itemData, int currentCount)
    {
        if (!isLocalPlayer) return;

        if (currentCount <= 0 || itemData == null)
        {
            Clear();
            return;
        }

        if (currentHoldingItem == null)
        {
            HoldingItem(itemData);
        }
        else
        {
            Item heldItem = currentHoldingItem.GetComponent<Item>();
            if (heldItem == null || heldItem.itemData != itemData)
            {
                Clear();
                HoldingItem(itemData);
            }
        }
    }

    // ===========================================================
    // Getters
    public bool IsHolding() => isHolding;
    public bool IsAWeapon() => isWeapon;
    public GameObject GetCurrentHeldObject() => currentHoldingItem;
}