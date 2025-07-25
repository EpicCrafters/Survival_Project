using NUnit.Framework.Interfaces;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager instance;


    public int IndexSlotBar = 0;



    [SerializeField] private InventorySlot[] hotbarSlots; // Các ô trong hotbar (thanh nhanh)
    [SerializeField] private InventorySlot[] mainInventorySlots; // Các ô trong kho chính
    [SerializeField] private GameObject inventoryItemPrefab; // Prefab itemData để hiển thị trong slot
    [SerializeField] private GameInput gameInput; // Script nhận input từ người chơi
    //test
    [SerializeField] private ItemData stick;
    [SerializeField] private ItemData stone;
    [SerializeField] private ItemData camfire;
    //[SerializeField] private Button sortButton; // Nút sắp xếp kho đồ

    private Dictionary<ItemData, int> itemCounts = new Dictionary<ItemData, int>(); // Lưu số lượng từng loại itemData
    private int selectedHotbarIndex = -1; // Vị trí hiện tại đang chọn trong hotbar



    [SerializeField] private PlayerHoldingItem playerHolding;

    private void Awake()
    {
        instance = this; // Thiết lập singleton
    }

    private void Start()
    {
        ChangeHotbarSlot(IndexSlotBar); // Chọn ô đầu tiên của hotbar
        AddItem(camfire);
        for (int i = 0; i < 7; i++)
        {
            AddItem(stick);
            AddItem(stone);
        }

        //if (sortButton != null)
        //    sortButton.onClick.AddListener(SortItems); // Gắn sự kiện bấm nút sắp xếp
    }

    private void OnEnable()
    {
        // Gắn sự kiện cuộn chuột và phím số khi bật UI
        if (gameInput != null)
        {
            gameInput.OnScroll += HandleScroll;
            gameInput.OnNumberKeyPressed += HandleNumberKey;
        }
    }

    private void OnDisable()
    {
        // Gỡ sự kiện khi tắt UI
        if (gameInput != null)
        {
            gameInput.OnScroll -= HandleScroll;
            gameInput.OnNumberKeyPressed -= HandleNumberKey;
        }
    }

    // Thêm vật phẩm vào kho
    public bool AddItem(ItemData itemData)
    {
        if (itemCounts.ContainsKey(itemData))
            itemCounts[itemData]++;
        else
            itemCounts[itemData] = 1;

        // Thử gộp vào slot đã có sẵn
        if (TryStackItem(itemData, hotbarSlots) || TryStackItem(itemData, mainInventorySlots))
            return true;

        // Nếu không gộp được, thêm vào slot trống
        if (TryAddNewItem(itemData, hotbarSlots) || TryAddNewItem(itemData, mainInventorySlots))
            return true;

        return false; // Không còn chỗ trống
    }

    // Thử gộp itemData vào các slot đã có sẵn cùng loại
    private bool TryStackItem(ItemData item, InventorySlot[] slots)
    {
        foreach (var slot in slots)
        {
            InventoryItem itemInSlot = slot.GetComponentInChildren<InventoryItem>();
            if (itemInSlot != null &&
                itemInSlot.item == item &&
                item.resource.stackable &&
                itemInSlot.count < item.resource.maxStack)
            {
                itemInSlot.count++;
                itemInSlot.RefreshCount(); // Cập nhật hiển thị số lượng
                return true;
            }
        }
        return false;
    }

    // Thử thêm itemData vào slot trống
    private bool TryAddNewItem(ItemData item, InventorySlot[] slots)
    {
        foreach (var slot in slots)
        {
            if (slot.GetComponentInChildren<InventoryItem>() == null)
            {
                SpawnNewItem(item, slot);
                return true;
            }
        }
        return false;
    }

    // Tạo itemData mới và gắn vào slot
    private void SpawnNewItem(ItemData item, InventorySlot slot)
    {
        GameObject newItemGO = Instantiate(inventoryItemPrefab, slot.transform);
        InventoryItem inventoryItem = newItemGO.GetComponent<InventoryItem>();
        inventoryItem.InitialiseItem(item);


        for (int i = 0; i < hotbarSlots.Length; i++)
        {
            if (slot == hotbarSlots[i] && i == selectedHotbarIndex)
            {
                playerHolding.HoldingItem(item);
                break;
            }
        }
    }

    // Đổi slot đang chọn trong hotbar
    public void ChangeHotbarSlot(int index)
    {
        if (hotbarSlots == null || index < 0 || index >= hotbarSlots.Length)
            return;

        if (selectedHotbarIndex >= 0 && selectedHotbarIndex < hotbarSlots.Length)
            hotbarSlots[selectedHotbarIndex].Deselect();

        hotbarSlots[index].Select();
        selectedHotbarIndex = index;

        InventoryItem selectedItem = hotbarSlots[index].GetComponentInChildren<InventoryItem>();
        if (selectedItem != null)
        {
            playerHolding.HoldingItem(selectedItem.item);
        }
        else
        {
            playerHolding.Clear();
        }


    }

    // Cuộn qua các ô hotbar bằng chuột
    private void ScrollSlot(int direction)
    {
        if (hotbarSlots == null || hotbarSlots.Length == 0)
            return;

        int newSlot = (selectedHotbarIndex + direction + hotbarSlots.Length) % hotbarSlots.Length;
        IndexSlotBar = newSlot;
        ChangeHotbarSlot(IndexSlotBar);
    }

    // Xử lý sự kiện cuộn chuột
    private void HandleScroll(object sender, float scrollValue)
    {
        if (scrollValue > 0)
        {
            ScrollSlot(-1); // Cuộn lên
        }
        else if (scrollValue < 0)
        {
            ScrollSlot(1); // Cuộn xuống
        }
    }

    // Xử lý khi nhấn phím số 1–9 để chọn hotbar
    private void HandleNumberKey(object sender, int index)
    {
        if (index >= 0 && index < hotbarSlots.Length)
        {
            IndexSlotBar = index;
            ChangeHotbarSlot(IndexSlotBar);
        }
    }

    // Sắp xếp lại tất cả itemData trong kho
    public void SortItems()
    {
        List<ItemData> allItems = new List<ItemData>();

        // Gom tất cả itemData từ hotbar và inventory
        CollectItems(hotbarSlots, allItems);
        CollectItems(mainInventorySlots, allItems);

        // Xóa hết slot
        ClearSlots(hotbarSlots);
        ClearSlots(mainInventorySlots);

        // Thêm lại itemData để sắp xếp
        foreach (var item in allItems)
            AddItem(item);
    }

    // Gom tất cả itemData từ các slot vào danh sách
    private void CollectItems(InventorySlot[] slots, List<ItemData> list)
    {
        foreach (var slot in slots)
        {
            InventoryItem itemInSlot = slot.GetComponentInChildren<InventoryItem>();
            if (itemInSlot != null)
            {
                for (int i = 0; i < itemInSlot.count; i++)
                    list.Add(itemInSlot.item);
                Destroy(itemInSlot.gameObject); // Xóa object cũ
            }
        }
    }

    // Xóa toàn bộ itemData trong các slot
    private void ClearSlots(InventorySlot[] slots)
    {
        foreach (var slot in slots)
        {
            InventoryItem item = slot.GetComponentInChildren<InventoryItem>();
            if (item != null)
                Destroy(item.gameObject);
        }
    }

    // Xử lý khi kéo thả itemData giữa các ô
    public void OnItemDropped(InventorySlot fromSlot, InventorySlot toSlot)
    {
        InventoryItem fromItem = fromSlot.GetComponentInChildren<InventoryItem>();
        InventoryItem toItem = toSlot.GetComponentInChildren<InventoryItem>();

        if (fromItem == null) return;

        if (toItem == null)
        {
            fromItem.transform.SetParent(toSlot.transform); // Di chuyển sang slot trống
        }
        else if (fromItem.item == toItem.item && fromItem.item.resource.stackable)
        {
            int transferable = Mathf.Min(fromItem.count, toItem.item.resource.maxStack - toItem.count);
            fromItem.count -= transferable;
            toItem.count += transferable;
            toItem.RefreshCount();

            if (fromItem.count <= 0)
                Destroy(fromItem.gameObject);
            else
                fromItem.RefreshCount();
        }
        else
        {
            // Đổi chỗ 2 itemData khác loại
            Transform temp = toItem.transform;
            toItem.transform.SetParent(fromSlot.transform);
            fromItem.transform.SetParent(toSlot.transform);
        }
    }

    // Tạo một itemData mới với số lượng cụ thể (dùng khi tách itemData)
    public void SpawnSplitItem(ItemData item, int amount)
    {
        if (!TrySpawn(item, amount, mainInventorySlots))
            TrySpawn(item, amount, hotbarSlots);

    }

    // Lấy itemData đang được chọn (dùng cho hotbar hoặc kho)
    public ItemData GetSelectedItem(bool fromInventory)
    {
        InventorySlot[] source = fromInventory ? mainInventorySlots : hotbarSlots;
        int index = fromInventory ? 0 : selectedHotbarIndex;

        if (index >= 0 && index < source.Length)
        {
            InventoryItem itemUI = source[index].GetComponentInChildren<InventoryItem>();
            return itemUI != null ? itemUI.item : null;
        }

        return null;
    }

    // Thử spawn itemData mới vào slot trống
    private bool TrySpawn(ItemData item, int amount, InventorySlot[] slots)
    {
        foreach (var slot in slots)
        {
            if (slot.GetComponentInChildren<InventoryItem>() == null)
            {
                GameObject newItemGO = Instantiate(inventoryItemPrefab, slot.transform);
                InventoryItem inventoryItem = newItemGO.GetComponent<InventoryItem>();
                inventoryItem.InitialiseItem(item);
                inventoryItem.count = amount;
                inventoryItem.RefreshCount();
                return true;
            }
        }
        return false;
    }
    // Hàm xóa số lượng item khỏi kho
    public bool RemoveItem(ItemData itemData, int amount)
    {
        // Nếu không đủ item để xóa thì trả về false
        if (!itemCounts.ContainsKey(itemData) || itemCounts[itemData] < amount)
            return false;

        itemCounts[itemData] -= amount; // Trừ số lượng
        if (itemCounts[itemData] <= 0)
            itemCounts.Remove(itemData); // Xóa key nếu không còn item

        UpdateInventoryUIAfterRemove(itemData, amount); // Cập nhật UI sau khi xóa
        return true;
    }

    // Cập nhật UI sau khi xóa item
    private void UpdateInventoryUIAfterRemove(ItemData itemData, int amount)
    {
        int remaining = amount;

        // Xóa trên hotbar 
        foreach (var slot in hotbarSlots)
        {
            InventoryItem itemUI = slot.GetComponentInChildren<InventoryItem>();
            if (itemUI != null && itemUI.item == itemData)
            {
                int removeCount = Mathf.Min(itemUI.count, remaining);
                itemUI.count -= removeCount;
                remaining -= removeCount;
                if (itemUI.count <= 0)
                    Destroy(itemUI.gameObject);
                else
                    itemUI.RefreshCount();

                if (remaining <= 0)
                    break;
            }
        }


    }

    // Hàm lấy số lượng hiện có của item
    public int GetItemCount(ItemData itemData)
    {
        if (itemCounts.TryGetValue(itemData, out int count))
        {
            return count;
        }
        return 0;
    }
}
