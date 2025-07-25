using UnityEngine;

public class PlayerHoldingItem : MonoBehaviour
{
    [SerializeField] private Transform holdingPoint; // Vị trí để hiển thị vật phẩm đang cầm
    [SerializeField] private ItemPlacer itemPlacer;

    private GameObject currentHoldingItem; // GameObject hiện đang cầm
    [SerializeField] private bool isHolding; // Trạng thái có đang cầm hay không
    public ItemData ItemData;
    // Hàm gọi khi muốn cầm một vật phẩm mới
    public void HoldingItem(ItemData itemData)
    {
        Clear(); // Xóa vật phẩm đang cầm cũ nếu có

        if (itemData != null && itemData.worldPrefab != null)
        {
            ItemData=itemData;
            isHolding = true;
            // Tạo mới prefab vật phẩm tại vị trí holdingPoint
            currentHoldingItem = Instantiate(itemData.worldPrefab, holdingPoint);
            currentHoldingItem.transform.localPosition = Vector3.zero;
            currentHoldingItem.transform.localRotation = Quaternion.identity;

            // Lấy component Item từ prefab hoặc cha/con
            Item item = currentHoldingItem.GetComponent<Item>();
            if (item == null)
                item = currentHoldingItem.GetComponentInParent<Item>();
            if (item == null)
                item = currentHoldingItem.GetComponentInChildren<Item>();

            if (item != null)
            {
                item.itemData = itemData; // Gán dữ liệu item cho prefab
            }
            else
            {
                Debug.LogWarning("Held prefab thiếu component Item!");
            }

            // Tắt vật lý cho vật phẩm đang cầm
            Rigidbody rb = currentHoldingItem.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }

            // Gán dữ liệu cho hitbox (nếu có)
            ItemHitBox hitbox = currentHoldingItem.GetComponentInChildren<ItemHitBox>();
            if (hitbox != null)
            {
                hitbox.SetItemData(itemData);
            }

            // Nếu item này có thể đặt xuống => bật ghost preview
            if (itemPlacer != null)
            {
                if(itemData.itemName== "Camfire")
                {
                    //Debug.Log("bat dau Dat item");
                    itemPlacer.StartPlacing(itemData, this);
                }           
            }

            //Debug.Log($"Player đang cầm: {itemData.itemName}");
        }
        else
        {
            Debug.LogWarning("ItemData hoặc prefab null!");
        }
    }

    // Xóa vật phẩm đang cầm 
    public void Clear()
    {
        if (currentHoldingItem != null)
        {
            isHolding = false;
            ItemData=null;
            Destroy(currentHoldingItem);
            currentHoldingItem = null;
        }
    }
    public void OnPlaced()
    {
        // Gọi khi đã đặt thành
        InventoryManager.instance.RemoveItem(ItemData,1);
        Clear();
        
    }
    // Kiểm tra có đang cầm vật phẩm không
    public bool IsHolding()
    {
        return isHolding;
    }

    // Lấy GameObject vật phẩm đang cầm
    public GameObject GetCurrentHeldObject()
    {
        return currentHoldingItem;
    }

    // Cập nhật vật phẩm đang cầm theo số lượng mới trong kho
    public void RefreshHoldingItem(ItemData itemData, int currentCount)
    {
        if (currentCount <= 0 || itemData == null)
        {
            Clear(); // Nếu không còn item thì xóa
            return;
        }

        if (currentHoldingItem == null)
        {
            HoldingItem(itemData); // Nếu chưa cầm thì tạo mới
        }
        else
        {
            Item heldItem = currentHoldingItem.GetComponent<Item>();
            // Nếu vật phẩm khác với đang cầm thì đổi mới
            if (heldItem == null || heldItem.itemData != itemData)
            {
                Clear();
                HoldingItem(itemData);
            }
        }

    }
}
