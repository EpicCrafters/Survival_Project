using UnityEngine;

public class PlayerHoldingItem : MonoBehaviour
{
    [SerializeField] private Transform HoldingPoint; // Điểm trên tay/nhân vật để giữ item
    private GameObject currentHoldingItem; // Đối tượng item đang được giữ hiện tại

    private Item currentItem; // Item đang được chọn từ hotbar

    private void Start()
    {
        UpdateHeldItem(); // Cập nhật item đang giữ khi game bắt đầu
    }

    private void Update()
    {
        // Lấy item được chọn từ hotbar
        Item selectedItem = InventoryManager.instance?.GetSelectedItem(false);

        // Nếu item thay đổi thì cập nhật lại vật phẩm trên tay
        if (selectedItem != currentItem)
        {
            currentItem = selectedItem;
            UpdateHeldItem();
        }
    }

    // Cập nhật vật phẩm đang giữ trên tay
    public void UpdateHeldItem()
    {
        // Nếu đang có item thì xóa đi trước
        if (currentHoldingItem != null)
        {
            Destroy(currentHoldingItem);
        }

        // Nếu có item mới và có prefab để cầm thì tạo ra item trên tay
        if (currentItem != null && currentItem.heldPrefab != null)
        {
            currentHoldingItem = Instantiate(currentItem.heldPrefab, HoldingPoint);
            currentHoldingItem.transform.localPosition = Vector3.zero; // Đặt vị trí về gốc
            currentHoldingItem.transform.localRotation = Quaternion.identity; // Xoay về mặc định

            // 🔽 Nếu có Rigidbody thì tắt vật lý để không bị rơi
            Rigidbody rb = currentHoldingItem.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }

            // 🔽 Nếu có Collider thì vô hiệu hóa để không va chạm
            Collider col = currentHoldingItem.GetComponent<Collider>();
            if (col != null)
            {
                col.enabled = false;
            }
        }
    }
}
