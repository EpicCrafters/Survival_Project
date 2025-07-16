using UnityEngine;
using UnityEngine.EventSystems;

public class SlotBase : MonoBehaviour, IDropHandler
{

    public virtual void OnDrop(PointerEventData eventData)
    {
        GameObject dropped = eventData.pointerDrag;
        InventoryItem droppedItem = dropped.GetComponent<InventoryItem>();
        InventoryItem existingItem = GetComponentInChildren<InventoryItem>();

        if (existingItem == null)
        {
            // Trường hợp 1: Slot đang TRỐNG
            // Đặt vật phẩm vào slot này.
            droppedItem.transform.SetParent(transform); //  Gán parent ngay lập tức
            droppedItem.parentAfterDrag = transform;
        }
        else if (existingItem.item == droppedItem.item && droppedItem.item.resource.stackable)
        {
            // Trường hợp 2: Slot đã CÓ vật phẩm cùng loại & có thể cộng dồn (stackable)
            int total = existingItem.count + droppedItem.count; // Tổng số stack khi gộp
            int maxStack = droppedItem.item.resource.maxStack;  // Giới hạn stack tối đa

            if (total <= maxStack)
            {
                // Tổng số lượng nhỏ hơn hoặc bằng max stack
                // Gộp hết và xoá item được kéo
                existingItem.count = total;
                existingItem.RefreshCount();
                Destroy(droppedItem.gameObject);
            }
            else
            {
                // Tổng vượt quá max stack
                // Gộp tối đa, phần dư trả lại con chuột (parent cũ)
                int remaining = total - maxStack;
                existingItem.count = maxStack;
                existingItem.RefreshCount();

                droppedItem.count = remaining;
                droppedItem.RefreshCount();

                droppedItem.transform.SetParent(droppedItem.parentAfterDrag);
            }
        }
        else
        {
            // Trường hợp 3: Slot đã có item KHÁC loại đổi vị trí.
            Transform oldParent = droppedItem.parentAfterDrag; // Slot cũ

            // Đưa item có sẵn về slot cũ
            existingItem.transform.SetParent(oldParent);
            existingItem.parentAfterDrag = oldParent;

            // Đưa item đang kéo vào slot mới
            droppedItem.transform.SetParent(transform);
            droppedItem.parentAfterDrag = transform;
        }

    }

}