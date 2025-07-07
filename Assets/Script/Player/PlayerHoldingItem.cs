using UnityEngine;

public class PlayerHoldingItem : MonoBehaviour
{
    [SerializeField] private Transform holdingPoint;
    private GameObject currentHoldingItem;
    [SerializeField] private bool isHolding;
   


    public void HoldingItem(ItemData itemData)
    {
        Clear();
        if(itemData!=null&& itemData.worldPrefab != null)
        {

            isHolding = true;
            currentHoldingItem = Instantiate(itemData.worldPrefab, holdingPoint);
            currentHoldingItem.transform.localPosition = Vector3.zero;
            currentHoldingItem.transform.localRotation= Quaternion.identity;
            Rigidbody rb = currentHoldingItem.GetComponent<Rigidbody>();
            if(rb != null )
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }
            Debug.Log($"Player is holding:{itemData.itemName}");
            Debug.Log($"Item Type: {itemData.type}");

            if (itemData.type == ItemType.Weapon)
            {
                Debug.Log($"Weapon Type: {itemData.weapon.weaponType}");
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
            Debug.LogWarning("Not Working");
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
   
}
