using Mirror;
using NUnit.Framework.Interfaces;
using UnityEngine;

public class PlayerHoldingItem : NetworkBehaviour
{
    [Header("Hand References")]
    [SerializeField] public Transform holdingPoint; // Right hand
    [SerializeField] public Transform leftHandHoldingPoint; // Left hand (NEW!)

    [Header("References")]
    [SerializeField] public ItemPlacer itemPlacer;
    [SerializeField] private BuildManager buildManager;
    [SerializeField] private WeaponAnimatorHandler weaponHandler;
    //[SerializeField] private GameObject hammerPrefab;

    [SerializeField] private GameObject currentHoldingItem;
    private bool isHolding;
    private bool isWeapon;
    private bool isRangedWeapon;
    public ItemData ItemData { get; private set; }
    public bool buildingType = false;

    public ItemData TestItemData;

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
            Debug.Log(itemId);
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
            Debug.LogWarning($"[PlayerHoldingItem] ItemData with id {itemId} not found");
            return;
        }
        Debug.Log("Picking item: " + data.id + " - " + data.itemName);
        GameObject prefab = data.heldPrefab ?? data.worldPrefab;

        if (prefab == null)
        {
            Debug.LogWarning($"[PlayerHoldingItem] No prefab found for {data.itemName}");
            return;
        }

        //  CHOOSE HAND BASED ON WEAPON TYPE
        Transform targetHand = holdingPoint; // Default: right hand

        // Only check weapon type if it's actually a weapon with valid weapon stats
        if (data.type == ItemType.Weapon && data.weapon != null)
        {
            if (data.weapon.weaponType == WeaponType.Bow)
            {
                isRangedWeapon = true;
                // Bow goes to LEFT hand
                if (leftHandHoldingPoint != null)
                {
                    targetHand = leftHandHoldingPoint;
                    Debug.Log($"[PlayerHoldingItem] Equipping {data.itemName} (bow) to LEFT hand");
                }
                else
                {
                    Debug.LogWarning("[PlayerHoldingItem] leftHandHoldingPoint not assigned! Using right hand.");
                }
            }
            else
            {
                // All other weapons (sword, axe, spear) go to RIGHT hand
                Debug.Log($"[PlayerHoldingItem] Equipping {data.itemName} ({data.weapon.weaponType}) to RIGHT hand");
            }
        }

        // Instantiate item on the chosen hand
        GameObject newItem = Instantiate(prefab, targetHand);
        newItem.transform.localPosition = Vector3.zero;
        newItem.transform.localRotation = Quaternion.identity;
        newItem.transform.localScale = Vector3.one;

        currentHoldingItem = newItem;
        ItemData = data;
        isHolding = true;


        // LOGIC ĐẶT ITEM 
        if (data.itemPlace)
        {
            if (data.type == ItemType.BuildingPart)
            {
                buildingType = true;
                buildManager.SetCurrentItem(data, this);
            }
            else
            {
                buildingType = false;
                itemPlacer.StartPlacing(data, this);
            }
        }
        else if (data.type == ItemType.Tool)
        {
            buildingType = true;
            buildManager.SetCurrentItem(data, this);
        }


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

            //  SETUP BOW IK IF IT'S A BOW
            if (data.weapon != null &&
                (data.weapon.weaponType == WeaponType.Bow))
            {
                SetupBowIK(newItem);
            }
        }

        Debug.Log($"[PlayerHoldingItem] Created held item: {data.itemName} on {targetHand.name}");
    }

    // ===========================================================
    //  NEW: Setup IK target when bow is equipped
    private void SetupBowIK(GameObject bowObject)
    {
        BowStringController bowController = bowObject.GetComponentInChildren<BowStringController>();
        if (bowController == null)
        {
            Debug.LogWarning("[PlayerHoldingItem] BowStringController not found on bow!");
            return;
        }

        // Send IK target
        PlayerItemUseHandler useHandler = GetComponent<PlayerItemUseHandler>();
        if (useHandler != null)
        {
            useHandler.SetBowIKTarget(bowController.rightHandIKTarget);

            //  NEW: send arrow spawn point to handler
            useHandler.SetArrowSpawnPoint(bowController.arrowSpawnPoint);

            Debug.Log("[PlayerHoldingItem] Bow IK + ArrowSpawnPoint assigned");
        }
        else
        {
            Debug.LogWarning("[PlayerHoldingItem] PlayerItemUseHandler not found!");
        }
    }




    // ===========================================================
    //  NEW: Helper to find child by multiple possible names
    private Transform FindChildRecursive(Transform parent, params string[] names)
    {
        foreach (string name in names)
        {
            Transform found = parent.Find(name);
            if (found != null) return found;
        }

        // Search children recursively
        foreach (Transform child in parent)
        {
            Transform found = FindChildRecursive(child, names);
            if (found != null) return found;
        }

        return null;
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
        isRangedWeapon = false;
        isHolding = false;
        isWeapon = false;
        ItemData = null;

        //  NOTIFY PlayerItemUseHandler that item was switched
        if (isLocalPlayer)
        {
            PlayerItemUseHandler useHandler = GetComponent<PlayerItemUseHandler>();
            if (useHandler != null)
            {
                useHandler.OnItemSwitched();
            }
        }

        // Cancel placement/building mode

        if (itemPlacer != null)
        {
            if (!buildingType)
            {
                itemPlacer.CancelPlacing();
            }
            else
            {
                buildManager.SetCurrentItem(null, this);
                buildManager.EndVisualisingObject();
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

    public bool IsRangedWeapon() => isRangedWeapon;
}