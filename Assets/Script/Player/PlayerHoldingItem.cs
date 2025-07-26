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
       
        Clear(); //  Xóa vật phẩm đang cầm cũ nếu có

        if (itemData != null && itemData.worldPrefab != null)
        {
            ItemData = itemData;
            isHolding = true;

            // Tạo prefab mới tại vị trí holdingPoint
            currentHoldingItem = Instantiate(itemData.worldPrefab, holdingPoint);
            currentHoldingItem.transform.localPosition = Vector3.zero;
            currentHoldingItem.transform.localRotation = Quaternion.identity;

            // Gán dữ liệu item
            Item item = currentHoldingItem.GetComponent<Item>();
            if (item == null)
                item = currentHoldingItem.GetComponentInParent<Item>();
            if (item == null)
                item = currentHoldingItem.GetComponentInChildren<Item>();

            if (item != null)
            {
                item.itemData = itemData;
            }
            else
            {
                Debug.LogWarning("Held prefab thiếu component Item!");
            }

            //Tắt collider 
            Collider col = currentHoldingItem.GetComponentInChildren<Collider>();
            if (col != null)
            {
                col.enabled = false;
            }

            //Tắt vật lý 
            Rigidbody rb = currentHoldingItem.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }

            // Gán dữ liệu cho HitBox nếu có
            ItemHitBox hitbox = currentHoldingItem.GetComponentInChildren<ItemHitBox>();
            if (hitbox != null)
            {
                hitbox.SetItemData(itemData);
            }

            // Nếu item có thể đặt được => bật ghost preview
            if (itemPlacer != null)
            {
                if (itemData.itemName == "Camfire") // hoặc: itemData.canBePlaced
                {
                    itemPlacer.StartPlacing(itemData, this);
                }
            }
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
            if (itemPlacer != null)
            {
                itemPlacer.CancelPlacing();  // Cancel ghost preview on clear
            }
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
