using TMPro;
using UnityEngine;

public class CampFire : MonoBehaviour, Iinteractable
{
    [Header("Fuel Settings")]
    [SerializeField] private int maxFuel = 10;
    [SerializeField] private int currentFuel = 0;

    [Header("UI")]
    [SerializeField] private Canvas campfireCanvas;
    [SerializeField] private TextMeshProUGUI fuelText;

    private InventoryManager inventoryManager;
    private PlayerHoldingItem playerHoldingItem;

    private void Awake()
    {
        inventoryManager = InventoryManager.instance;
        playerHoldingItem = FindObjectOfType<PlayerHoldingItem>();
        HideUI();
    }

    private void UpdateFuelUI()
    {
        if (fuelText != null)
            fuelText.text = $"Fuel: {currentFuel} / {maxFuel}";
    }

    public void ShowUI()
    {
        if (campfireCanvas != null)
            campfireCanvas.enabled = true;
        UpdateFuelUI();
    }

    public void HideUI()
    {
        if (campfireCanvas != null)
            campfireCanvas.enabled = false;
    }

    public void Interact()
    {

        
        if (playerHoldingItem == null) return;

        GameObject heldObject = playerHoldingItem.GetCurrentHeldObject();
        if (heldObject == null) return;

        if (heldObject.TryGetComponent<Item>(out var item))
        {
            ItemData data = item.itemData;
            if (data.type == ItemType.Resource )
            {
                if (data.itemName == "Stick") 
                {
                    Debug.Log("Finded");
                    if (currentFuel < maxFuel)
                    {
                        currentFuel++;
                        UpdateFuelUI();

                       
                        Destroy(heldObject);
                    }
                    else
                    {
                        Debug.Log("Campfire fuel is full.");
                    }
                }
                else
                {
                    Debug.Log("Not find");
                }
            }
        }
    }
}
