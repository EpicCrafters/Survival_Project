using Mirror;
using UnityEngine;

public class PlayerHoldingItem : NetworkBehaviour
{
    [SerializeField] public Transform holdingPoint;
    [SerializeField] private ItemPlacer itemPlacer;
    [SerializeField] private WeaponAnimatorHandler weaponHandler;

    private GameObject currentHoldingItem;
    [SerializeField] private bool isHolding;
    public ItemData ItemData;
    public bool buildingType = false;

    [SyncVar(hook = nameof(OnItemChanged))]
    private int currentItemId;

    // ✅ NEW: Track the last requested item locally
    private int lastRequestedItemId;

    public void HoldingItem(ItemData itemData)
    {
        if (!isLocalPlayer) return;

        if (itemData == null)
        {
            Debug.LogWarning("ItemData null!");
            return;
        }

        // ✅ FIXED: Check if we're already holding this item
        if (ItemData != null && ItemData.id == itemData.id && currentHoldingItem != null)
        {
            // Item is already being held and displayed, no need to do anything
            return;
        }

        // ✅ FIXED: If switching back to the same item after clearing
        if (lastRequestedItemId == itemData.id && currentItemId == itemData.id)
        {
            // Force recreation by clearing first
            Clear();
            CreateHeldItem(itemData.id);
            return;
        }

        Clear();
        lastRequestedItemId = itemData.id;
        CmdSetHeldItem(itemData.id);
    }

    [Command]
    private void CmdSetHeldItem(int itemId)
    {
        currentItemId = itemId;
    }

    private void OnItemChanged(int oldId, int newId)
    {
        CreateHeldItem(newId);
    }

    // ✅ NEW: Separated the item creation logic
    private void CreateHeldItem(int itemId)
    {
        Clear();

        if (itemId == 0) return;

        ItemData data = ItemDatabase.Get(itemId);
        if (data == null || data.worldPrefab == null)
        {
            Debug.LogWarning("ItemData hoặc prefab null!");
            return;
        }

        ItemData = data;
        isHolding = true;

        GameObject newItem = Instantiate(data.worldPrefab, holdingPoint);
        newItem.transform.localPosition = Vector3.zero;
        newItem.transform.localRotation = Quaternion.identity;
        newItem.transform.localScale = Vector3.one;

        currentHoldingItem = newItem;

        if (data.type == ItemType.Weapon)
        {
            weaponHandler?.EquipWeapon(data);
        }

        Collider col = currentHoldingItem.GetComponentInChildren<Collider>();
        if (col != null) col.enabled = false;

        Rigidbody rb = currentHoldingItem.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        Item item = currentHoldingItem.GetComponent<Item>() ??
                    currentHoldingItem.GetComponentInParent<Item>() ??
                    currentHoldingItem.GetComponentInChildren<Item>();
        if (item != null) item.itemData = data;
        else Debug.LogWarning("Held prefab thiếu component Item!");

        ItemHitBox hitbox = currentHoldingItem.GetComponentInChildren<ItemHitBox>();
        if (hitbox != null) hitbox.SetItemData(data);

        if (data.itemPlace)
        {
            if (data.type == ItemType.BuildingPart)
            {
                buildingType = true;
                BuildManager.Instance.SetCurrentItem(data, this);
            }
            else
            {
                buildingType = false;
                itemPlacer.StartPlacing(data, this);
            }
        }

        if (data.type == ItemType.Tool)
        {
            buildingType = true;
            BuildManager.Instance.SetCurrentItem(data, this);
        }
    }

    public void Clear()
    {
        if (currentHoldingItem != null)
        {
            Destroy(currentHoldingItem);
            currentHoldingItem = null;
        }

        isHolding = false;
        ItemData = null;

        if (itemPlacer != null)
        {
            if (buildingType == false)
            {
                itemPlacer.CancelPlacing();
            }
            else
            {
                BuildManager.Instance.SetCurrentItem(null, this);
                BuildManager.Instance.EndVisualisingObject();
            }
        }
    }

    public void OnPlaced()
    {
        bool removed = InventoryManager.instance.RemoveItem(ItemData, 1);

        if (!removed)
        {
            Clear();
            return;
        }

        if (InventoryManager.instance.GetItemCount(ItemData) == 0)
        {
            Clear();
        }
    }

    public bool IsHolding() => isHolding;

    public GameObject GetCurrentHeldObject() => currentHoldingItem;

    public void RefreshHoldingItem(ItemData itemData, int currentCount)
    {
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
}