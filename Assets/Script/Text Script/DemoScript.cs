using UnityEngine;

public class DemoScript : MonoBehaviour
{
  public InventoryManager inventoryManager;
    public ItemData[] itemToPickup;


    public void PickupItem(int id)
    {
      bool result=  inventoryManager.AddItem(itemToPickup[id]);
        if (result == true)
        {
            Debug.Log("ItemData added");
        }
        else
        {
            Debug.Log("Not Added");
        }
    }


    public void GetSelectedItem()
    {
        ItemData receivedItem =inventoryManager.GetSelectedItem(false);
        if(receivedItem != null)
        {
            Debug.Log("received item:" + receivedItem);
        }
        else
        {
            Debug.Log("No item received");
        }
    }

    public void UsedSelectedItem()
    {
        ItemData receivedItem = inventoryManager.GetSelectedItem(true);
        if (receivedItem != null)
        {
            Debug.Log("used item:" + receivedItem);
        }
        else
        {
            Debug.Log("No item use");
        }
    }
}
