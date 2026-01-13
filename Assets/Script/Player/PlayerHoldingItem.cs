using Mirror;
using UnityEngine;

public class PlayerHoldingItem : NetworkBehaviour
{
    [Header("Hand References - Tham chiếu tay")]
    [SerializeField] public Transform holdingPoint; // Tay phải
    [SerializeField] public Transform leftHandHoldingPoint; // Tay trái

    [Header("References - Tham chiếu")]
    [SerializeField] public ItemPlacer itemPlacer;
    [SerializeField] private WeaponAnimatorHandler weaponHandler;
    [SerializeField] private BuildManager buildManager;
    private BowStringController cachedBowController;
    // Biến private
    private GameObject currentHoldingItem;
    private bool isHolding;
    private bool isWeapon;
    private bool isRangedWeapon;
    public ItemData ItemData { get; private set; }
    public bool buildingType = false;

    [SyncVar(hook = nameof(OnItemChanged))]
    private int currentItemId = 0;

    // ===========================================================
    // Được gọi khi người chơi muốn cầm một item
    // ===========================================================
    public void HoldingItem(ItemData itemData)
    {
        if (!isLocalPlayer) return;

        int newItemId = (itemData == null) ? 0 : itemData.id;

        // Nếu đang cầm item giống nhau rồi thì skip
        if (currentItemId == newItemId && currentHoldingItem != null)
            return;

        // Update local trước để responsive
        UpdateHeldItem(newItemId);

        // Gửi lên server để sync với client khác
        CmdSetHeldItem(newItemId);
    }

    // ===========================================================
    [Command]
    private void CmdSetHeldItem(int itemId)
    {
        currentItemId = itemId;
    }

    // ===========================================================
    // Hook được gọi khi SyncVar thay đổi (chạy trên TẤT CẢ client)
    // ===========================================================
    private void OnItemChanged(int oldId, int newId)
    {
        // Local player đã update trong HoldingItem() rồi, skip để tránh xử lý 2 lần
        if (isLocalPlayer)
        {
            return;
        }

        // Update cho remote client
        UpdateHeldItem(newId);
    }

    // ===========================================================
    // Phương thức core để update held item (dùng bởi cả local và remote)
    // ===========================================================
    private void UpdateHeldItem(int itemId)
    {
        // Xóa item hiện tại
        ClearHeldItem();

        // Tạo item mới nếu không rỗng
        if (itemId != 0)
        {
            CreateHeldItem(itemId);
        }
    }
    public BowStringController GetBowController() => cachedBowController;
    // ===========================================================
    // Tạo visual của held item
    // ===========================================================
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

        // ✅ CHỌN TAY DỰA TRÊN LOẠI VŨ KHÍ
        Transform targetHand = holdingPoint; // Mặc định: tay phải

        // Chỉ kiểm tra loại vũ khí nếu nó thực sự là weapon với weapon stats hợp lệ
        if (data.type == ItemType.Weapon && data.weapon != null)
        {
            if (data.weapon.weaponType == WeaponType.Bow)
            {
                isRangedWeapon = true;
                // Cung đi vào tay TRÁI
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
                // Tất cả vũ khí khác (sword, axe, spear) đi vào tay PHẢI
                Debug.Log($"[PlayerHoldingItem] Trang bị {data.itemName} ({data.weapon.weaponType}) vào tay PHẢI");
            }
        }

        // Instantiate item trên tay đã chọn
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

        // Gắn ItemHeld component
        var heldComp = newItem.GetComponent<ItemHeld>() ?? newItem.AddComponent<ItemHeld>();
        heldComp.Init(data, this);

        // Tắt physics
        if (newItem.TryGetComponent<Collider>(out var col))
            col.enabled = false;

        if (newItem.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        // Set weapon animation CHỈ cho local player
        if (data.type == ItemType.Weapon && isLocalPlayer)
        {
            isWeapon = true;
            weaponHandler?.EquipWeapon(data);

            // ✅ SETUP BOW IK NẾU LÀ CUNG
            if (data.weapon != null && data.weapon.weaponType == WeaponType.Bow)
            {
                SetupBowIK(newItem);
            }
        }

        Debug.Log($"[PlayerHoldingItem] Đã tạo held item: {data.itemName} trên {targetHand.name}");
    }

    // ===========================================================
    // ✅ UPDATED: Setup IK target khi cung được trang bị
    // ===========================================================
    private void SetupBowIK(GameObject bowObject)
    {
        BowStringController bowController = bowObject.GetComponentInChildren<BowStringController>();
        if (bowController == null)
        {
            Debug.LogWarning("[PlayerHoldingItem] Không tìm thấy BowStringController trên cung!");
            return;
        }

        // ✅ CACHE THE BOW CONTROLLER
        cachedBowController = bowController;
        Debug.Log("[PlayerHoldingItem] ✅ Cached BowStringController");

        // Rest of your code...
        PlayerItemUseHandler useHandler = GetComponent<PlayerItemUseHandler>();
        if (useHandler != null)
        {
            useHandler.SetArrowSpawnPoint(bowController.arrowSpawnPoint);
            if (bowController.rightHandIKTarget != null)
            {
                useHandler.SetBowIKTarget(bowController.rightHandIKTarget);
            }
        }
    }

    // ===========================================================
    // ✅ Helper để tìm child theo nhiều tên có thể có
    // ===========================================================
    private Transform FindChildRecursive(Transform parent, params string[] names)
    {
        foreach (string name in names)
        {
            Transform found = parent.Find(name);
            if (found != null) return found;
        }

        // Tìm trong children đệ quy
        foreach (Transform child in parent)
        {
            Transform found = FindChildRecursive(child, names);
            if (found != null) return found;
        }

        return null;
    }

    // ===========================================================
    // Xóa held item
    // ===========================================================
    public void ClearHeldItem()
    {
        if (currentHoldingItem != null)
        {
            Destroy(currentHoldingItem);
            currentHoldingItem = null;
        }

       

        // ✅ ADD THIS
        cachedBowController = null;

        isRangedWeapon = false;
        isRangedWeapon = false;
        isHolding = false;
        isWeapon = false;
        ItemData = null;

        // ✅ Xóa bow IK khi đổi item
        if (isLocalPlayer)
        {
            // Xóa IK controller
            PlayerIKController ikController = GetComponent<PlayerIKController>();
            if (ikController != null)
            {
                ikController.ClearBowIK();
            }

            // Thông báo cho PlayerItemUseHandler rằng item đã được đổi
            PlayerItemUseHandler useHandler = GetComponent<PlayerItemUseHandler>();
            if (useHandler != null)
            {
                useHandler.OnItemSwitched();
            }
        }

        // Hủy placement/building mode
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

    // ===========================================================
    // Public Clear method (để tương thích ngược)
    // ===========================================================
    public void Clear()
    {
        if (!isLocalPlayer) return;
        HoldingItem(null);
    }

    // ===========================================================
    // Được gọi khi người chơi đặt một item
    // ===========================================================
    public void OnPlaced()
    {
        if (ItemData == null) return;

        var input = SystemManager.Instance.GetComponentInChildren<InventoryInput>();
        input?.RequestConsumeHeldItem(1);

    }

    // ===========================================================
    // Refresh held item khi inventory thay đổi
    // ===========================================================
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
    // ===========================================================
    public bool IsHolding() => isHolding;
    public bool IsAWeapon() => isWeapon;
    public GameObject GetCurrentHeldObject() => currentHoldingItem;

    public ItemData GetCurrentItemData()
    {
        return ItemData;
    }
   
    public bool IsRangedWeapon() => isRangedWeapon;
}