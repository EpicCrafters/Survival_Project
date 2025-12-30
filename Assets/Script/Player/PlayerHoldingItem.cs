using Mirror;
using UnityEngine;

public class PlayerHoldingItem : NetworkBehaviour
{
    [Header("Hand References - Tham chiếu tay")]
    [SerializeField] public Transform holdingPoint;
    [SerializeField] public Transform leftHandHoldingPoint;

    [Header("References - Tham chiếu")]
    [SerializeField] public ItemPlacer itemPlacer;
    [SerializeField] private WeaponAnimatorHandler weaponHandler;
    [SerializeField] private BuildManager buildManager;

    // Biến private
    private PlayerCombat playerCombat; // ✅ Combat reference for item switching checks
    private GameObject currentHoldingItem;
    private bool isHolding;
    private bool isWeapon;
    private bool isRangedWeapon;
    public ItemData ItemData { get; private set; }
    public bool buildingType = false;

    private BowStringController cachedBowController;

    [SyncVar(hook = nameof(OnItemChanged))]
    private int currentItemId = 0;

    private void Awake()
    {
        // ✅ Find PlayerCombat component on same GameObject
        playerCombat = GetComponent<PlayerCombat>();

        if (playerCombat == null)
        {
            Debug.LogWarning("[PlayerHoldingItem] PlayerCombat component not found - item switching lock will not work!");
        }
    }

    public BowStringController GetBowController() => cachedBowController;

    /// <summary>
    /// ✅ IMPROVED: Check if player can switch items before changing
    /// </summary>
    public void HoldingItem(ItemData itemData)
    {
        if (!isLocalPlayer) return;

        // ✅ Check if combat is blocking item switching
        if (playerCombat != null && !playerCombat.CanSwitchItems())
        {
            Debug.Log("[PlayerHoldingItem] Cannot switch items during attack!");
            return;
        }

        int newItemId = (itemData == null) ? 0 : itemData.id;

        if (currentItemId == newItemId && currentHoldingItem != null)
            return;

        UpdateHeldItem(newItemId);
        CmdSetHeldItem(newItemId);
    }

    [Command]
    private void CmdSetHeldItem(int itemId)
    {
        currentItemId = itemId;
    }

    private void OnItemChanged(int oldId, int newId)
    {
        if (isLocalPlayer)
        {
            return;
        }

        UpdateHeldItem(newId);
    }

    private void UpdateHeldItem(int itemId)
    {
        ClearHeldItem();

        if (itemId != 0)
        {
            CreateHeldItem(itemId);
        }
    }

    private void CreateHeldItem(int itemId)
    {
        ItemData data = ItemDatabase.Get(itemId);
        if (data == null)
        {
            Debug.LogWarning($"[PlayerHoldingItem] ItemData với id {itemId} không tìm thấy");
            return;
        }

        GameObject prefab = data.heldPrefab ?? data.worldPrefab;
        if (prefab == null)
        {
            Debug.LogWarning($"[PlayerHoldingItem] Không có prefab cho {data.itemName}");
            return;
        }

        Transform targetHand = holdingPoint;

        if (data.type == ItemType.Weapon && data.weapon != null)
        {
            if (data.weapon.weaponType == WeaponType.Bow)
            {
                isRangedWeapon = true;
                if (leftHandHoldingPoint != null)
                {
                    targetHand = leftHandHoldingPoint;
                    Debug.Log($"[PlayerHoldingItem] Trang bị {data.itemName} (bow) vào tay TRÁI");
                }
                else
                {
                    Debug.LogWarning("[PlayerHoldingItem] leftHandHoldingPoint chưa được gán! Dùng tay phải.");
                }
            }
            else
            {
                Debug.Log($"[PlayerHoldingItem] Trang bị {data.itemName} ({data.weapon.weaponType}) vào tay PHẢI");
            }
        }

        GameObject newItem = Instantiate(prefab, targetHand);
        newItem.transform.localPosition = Vector3.zero;
        newItem.transform.localRotation = Quaternion.identity;
        newItem.transform.localScale = Vector3.one;

        currentHoldingItem = newItem;
        ItemData = data;
        isHolding = true;

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

        var heldComp = newItem.GetComponent<ItemHeld>() ?? newItem.AddComponent<ItemHeld>();
        heldComp.Init(data, this);

        if (newItem.TryGetComponent<Collider>(out var col))
            col.enabled = false;

        if (newItem.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        if (data.type == ItemType.Weapon && isLocalPlayer)
        {
            isWeapon = true;
            weaponHandler?.EquipWeapon(data);

            if (data.weapon != null && data.weapon.weaponType == WeaponType.Bow)
            {
                SetupBowIK(newItem);
            }
        }

        Debug.Log($"[PlayerHoldingItem] Đã tạo held item: {data.itemName} trên {targetHand.name}");
    }

    private void SetupBowIK(GameObject bowObject)
    {
        cachedBowController = bowObject.GetComponentInChildren<BowStringController>();

        if (cachedBowController == null)
        {
            Debug.LogWarning("[PlayerHoldingItem] ⚠️ Không tìm thấy BowStringController!");
            return;
        }

        Debug.Log($"[PlayerHoldingItem] ✅ Cached BowStringController: {cachedBowController.gameObject.name}");

        Transform rightHandHoldPoint = null;
        Transform rightHintPosition = null;

        if (cachedBowController.stringMiddleBone != null)
        {
            Transform offsetRightHand = cachedBowController.stringMiddleBone.Find("offsetRightHand");
            if (offsetRightHand != null)
            {
                rightHandHoldPoint = offsetRightHand.Find("rightHandHoldPoint");
            }

            Transform hintOffset = FindChildRecursive(cachedBowController.stringMiddleBone, "Offset Right Hint");
            if (hintOffset != null)
            {
                rightHintPosition = FindChildRecursive(hintOffset, "rightHintPosition", "RightHintPosition");
            }
        }

        PlayerIKController ikController = GetComponent<PlayerIKController>();
        if (ikController != null)
        {
            if (cachedBowController.rightHandIKTarget != null)
            {
                ikController.SetLeftHandIKTarget(cachedBowController.rightHandIKTarget);
                ikController.SetLeftHandIKEnabled(true);
                ikController.SetLeftHandIKWeight(1f);
                Debug.Log($"[PlayerHoldingItem] ✅ Setup left hand IK target: {cachedBowController.rightHandIKTarget.name}");
            }

            if (rightHandHoldPoint != null)
            {
                ikController.SetRightHandIKTarget(rightHandHoldPoint);
                ikController.SetRightHandIKWeight(1f);
                Debug.Log($"[PlayerHoldingItem] ✅ Setup right hand IK target: {rightHandHoldPoint.name}");
            }

            if (rightHintPosition != null)
            {
                ikController.SetRightHandPoleHintOverride(rightHintPosition);
                Debug.Log($"[PlayerHoldingItem] ✅ Setup right hint override: {rightHintPosition.name}");
            }
        }

        PlayerItemUseHandler useHandler = GetComponent<PlayerItemUseHandler>();
        if (useHandler != null)
        {
            if (cachedBowController.arrowSpawnPoint != null)
            {
                useHandler.SetArrowSpawnPoint(cachedBowController.arrowSpawnPoint);
                Debug.Log($"[PlayerHoldingItem] ✅ Arrow spawn point assigned: {cachedBowController.arrowSpawnPoint.name}");
            }
            else
            {
                Debug.LogWarning("[PlayerHoldingItem] ⚠️ Arrow spawn point is NULL on bow controller!");
            }

            if (cachedBowController.rightHandIKTarget != null)
            {
                useHandler.SetBowIKTarget(cachedBowController.rightHandIKTarget);
            }

            if (rightHandHoldPoint != null)
            {
                useHandler.SetBowStringOverride(rightHandHoldPoint);
            }

            if (rightHintPosition != null)
            {
                useHandler.SetBowRightHintOverride(rightHintPosition);
            }
        }
        else
        {
            Debug.LogWarning("[PlayerHoldingItem] ⚠️ PlayerItemUseHandler not found!");
        }
    }

    private Transform FindChildRecursive(Transform parent, params string[] names)
    {
        foreach (string name in names)
        {
            Transform found = parent.Find(name);
            if (found != null) return found;
        }

        foreach (Transform child in parent)
        {
            Transform found = FindChildRecursive(child, names);
            if (found != null) return found;
        }

        return null;
    }

    private void ClearHeldItem()
    {
        if (currentHoldingItem != null)
        {
            Destroy(currentHoldingItem);
            currentHoldingItem = null;
        }

        cachedBowController = null;
        isRangedWeapon = false;
        isHolding = false;
        isWeapon = false;
        ItemData = null;

        if (isLocalPlayer)
        {
            PlayerIKController ikController = GetComponent<PlayerIKController>();
            if (ikController != null)
            {
                ikController.ClearBowIK();
            }

            PlayerItemUseHandler useHandler = GetComponent<PlayerItemUseHandler>();
            if (useHandler != null)
            {
                useHandler.OnItemSwitched();
            }
        }

        if (itemPlacer != null)
        {
            if (!buildingType)
                itemPlacer.CancelPlacing();
            else
            {
                buildManager.SetCurrentItem(null, this);
                buildManager.EndVisualisingObject();
            }
        }

        buildingType = false;
    }

    public void Clear()
    {
        if (!isLocalPlayer) return;
        HoldingItem(null);
    }

    public void OnPlaced()
    {
        if (ItemData == null) return;

        bool removed = InventoryManager.instance.RemoveItem(ItemData, 1);
        if (!removed)
        {
            Clear();
            return;
        }

        if (InventoryManager.instance.GetItemCount(ItemData) == 0)
            Clear();
    }

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

    // Getters
    public bool IsHolding() => isHolding;
    public bool IsAWeapon() => isWeapon;
    public GameObject GetCurrentHeldObject() => currentHoldingItem;
    public ItemData GetCurrentItemData() => ItemData;
    public bool IsRangedWeapon() => isRangedWeapon;
}