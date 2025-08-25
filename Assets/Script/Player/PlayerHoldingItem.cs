using NUnit.Framework.Interfaces;
using UnityEngine;

public class PlayerHoldingItem : MonoBehaviour
{
    [SerializeField] private Transform holdingPoint; // Vị trí để hiển thị vật phẩm đang cầm
    [SerializeField] private ItemPlacer itemPlacer;

    private GameObject currentHoldingItem; // GameObject hiện đang cầm
    [SerializeField] private GameObject hammerPrefab; // Prefab cây búa mặc định khi cầm building part

    [SerializeField] private bool isHolding; // Trạng thái có đang cầm hay không
    public ItemData ItemData;
    public bool buildingType = false;
    // Hàm gọi khi muốn cầm một vật phẩm mới
    public void HoldingItem(ItemData itemData)
    {

        Clear(); //  Xóa vật phẩm đang cầm cũ nếu có

        if (itemData != null && itemData.worldPrefab != null)
        {
            ItemData = itemData;
            isHolding = true;

            GameObject prefabToHold = itemData.worldPrefab;

            // Nếu là BuildingPart thì thay thế model trên tay bằng cây búa
            if (itemData.type == ItemType.BuildingPart)
            {
                prefabToHold = hammerPrefab;
            }

            // Tạo prefab trên tay
            currentHoldingItem = Instantiate(prefabToHold, holdingPoint);
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
            if (itemData.itemPlace)
            {
                if (itemData.type == ItemType.BuildingPart)
                {
                    //Debug.Log("Item là BuildingPart => tạm thời chưa bật ghost preview.");
                    buildingType = true;
                    BuildManager.Instance.SetCurrentItem(itemData, this);
                }
                else
                {
                    buildingType = false;
                    //Debug.Log("Bắt đầu đặt item (không phải BuildingPart).");
                    itemPlacer.StartPlacing(ItemData, this);
                }
            }
            if (itemData.type == ItemType.Tool)
            {
                buildingType = true;
                BuildManager.Instance.SetCurrentItem(itemData, this);
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
            ItemData = null;
            Destroy(currentHoldingItem);
            currentHoldingItem = null;
            if (itemPlacer != null)
            {
                if (buildingType == false)
                {
                    itemPlacer.CancelPlacing();  // Cancel ghost preview on clear
                }
                else
                {
                    BuildManager.Instance.SetCurrentItem(null, this);
                    BuildManager.Instance.EndVisualisingObject();
                }
            }

        }
    }
    public void OnPlaced()
    {

        bool removed = InventoryManager.instance.RemoveItem(ItemData, 1);

        if (!removed)
        {
            // Trường hợp không xóa được (ví dụ item đã hết trước đó)
            Clear();
            return;
        }

        // Kiểm tra lại xem trong kho còn item này không
        if (InventoryManager.instance.GetItemCount(ItemData) == 0)
        {
            // Nếu không còn thì clear item đang cầm
            Clear();
        }

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
