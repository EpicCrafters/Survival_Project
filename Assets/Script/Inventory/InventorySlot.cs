using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventorySlot : SlotBase, IPointerClickHandler
{
    public Image image;
    public Color selectedColor;
    public Color notSelectedColor;

    public int index;   // index trong InventoryController

    private void Awake()
    {
        Deselect();
    }

    public void Select()
    {
        if (image != null)
            image.color = selectedColor;
    }

    public void Deselect()
    {
        if (image != null)
            image.color = notSelectedColor;
    }

    // ---------------------------------------------------------
    // LEFT CLICK → đặt split vào slot này
    // ---------------------------------------------------------
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
            return;

        var view = SystemManager.Instance.GetComponentInChildren<InventoryView>();
        if (view == null || !view.HasActiveSplit())
            return;

        var input = SystemManager.Instance.GetComponentInChildren<InventoryInput>();
        input?.RequestPlaceSplit(index);
    }

    // ---------------------------------------------------------
    // Drag–drop logic vẫn dùng SlotBase.OnDrop
    // Không cần override OnDrop trừ khi bạn muốn custom thêm
    // ---------------------------------------------------------
}
