using UnityEngine;

public class PlayerHoldingItem : MonoBehaviour
{
    [SerializeField] private Transform holdingPoint;
    private GameObject currentHoldingItem;
    [SerializeField] private bool isHolding;



    public void HoldingItem(ItemData itemData)
    {
        Clear();

        if (itemData != null && itemData.worldPrefab != null)
        {
            isHolding = true;
            currentHoldingItem = Instantiate(itemData.worldPrefab, holdingPoint);
            //Debug.Log("Instantiated holding item: " + currentHoldingItem.name);
            currentHoldingItem.transform.localPosition = Vector3.zero;
            currentHoldingItem.transform.localRotation = Quaternion.identity;
            Item item = currentHoldingItem.GetComponent<Item>();

            if (item == null)
                item = currentHoldingItem.GetComponentInParent<Item>();

            if (item == null)
                item = currentHoldingItem.GetComponentInChildren<Item>();

            if (item != null)
            {
                item.itemData = itemData;
                //Debug.Log("Assigned ItemData to item: " + item.name);
            }
            else
            {
                Debug.LogWarning("Held prefab is missing Item component! Searched self, parents, and children.");
            }
            // Set Rigidbody if exists
            Collider col =currentHoldingItem.GetComponentInChildren<Collider>();//chỉnh collider
            if (col != null)
            {
                col.isTrigger = true;
            }
            Rigidbody rb = currentHoldingItem.GetComponent<Rigidbody>();//chỉnh rigibody
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }

            
            ItemHitBox hitbox = currentHoldingItem.GetComponentInChildren<ItemHitBox>();
            if (hitbox != null)
            {
                hitbox.SetItemData(itemData);
                hitbox.DisableHitbox();// Tắt Collider
                //Debug.Log("ItemData assigned to ItemHitBox.");
            }
            else
            {
                Debug.LogWarning("ItemHitBox not found on held item.");
            }

            Debug.Log($"Player is holding: {itemData.itemName}");
            Debug.Log($"Item Type: {itemData.type}");

            if (itemData.type == ItemType.Weapon)
            {
                Debug.Log($"Weapon Type: {itemData.weapon.weaponType}");
            }
            else if (itemData.type == ItemType.Tool)
            {
                Debug.Log($"Tool Type: {itemData.tool.toolType}");
            }
            else if (itemData.type == ItemType.Consumable)
            {
                Debug.Log($"Heals: {itemData.consumable.healAmount}, Fills: {itemData.consumable.fillAmount}");
            }
            else if (itemData.type == ItemType.Resource)
            {
                Debug.Log($"Stackable: {itemData.resource.stackable}, Max Stack: {itemData.resource.maxStack}");
            }
        }
        else
        {
            Debug.LogWarning("Not Working: ItemData or prefab is null");
        }
    }




    public void Clear()
    {
        if(currentHoldingItem!=null)
        {
            isHolding=false;
            Destroy(currentHoldingItem);
            currentHoldingItem = null;
        }
    }

    public bool IsHolding()
    {
        return isHolding;
    }

    public GameObject GetCurrentHeldObject()
    {
        return currentHoldingItem;
        
    }


}
