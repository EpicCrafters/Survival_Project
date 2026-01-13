using System.Collections.Generic;
using System.Linq;
using Mirror;
using UnityEngine;
using UnityEngine.UI;


public class InventoryManager : MonoBehaviour
{
    public static InventoryManager instance;
    public bool useNewInventoryView = true;

    public InventoryController controller;
    //public InventorySplitState splitState = new InventorySplitState();
    public bool isDraggingItem;
    public bool pendingRedraw;

    [Header("UI Prefabs")]
    public InventoryItem ghostUI;
    private InventoryItem ghostInstance;
    public GameObject inventoryItemPrefab;
    [SerializeField] private Canvas uiCanvas;
    [SerializeField] private RectTransform inventoryRoot;

    [Header("Slot Groups")]
    [SerializeField] private InventorySlot[] hotbarSlots;
    [SerializeField] private InventorySlot[] mainInventorySlots;
    [SerializeField] private InventorySlot[] craftingSlots;
    public IReadOnlyList<InventorySlot> CraftingSlots => craftingSlots;


    [Header("Input")]
    [SerializeField] private GameInput gameInput;

    //Replace Add Item Data old
    [Header("Starter Items")]
    [SerializeField] private List<StarterItem> starterItems = new List<StarterItem>();

    [SerializeField] private Transform playerTransform;
   
    // Optional counter map
    private Dictionary<ItemData, int> itemCounts = new Dictionary<ItemData, int>();
    private void Awake()
    {
        
        instance = this;

    }

    // ---------------------------
    // ADD ITEM
    // ---------------------------
    public bool AddItem(ItemData itemData)
    {

        
        return true;
    }

    // ---------------------------
    // REMOVE ITEM
    // ---------------------------
    public bool RemoveItem(ItemData itemData, int amount)
    {
        
        return true;
    }

    
    public int GetItemCount(ItemData itemData)
    {
        if (itemCounts.TryGetValue(itemData, out int value))
            return value;

        return 0;
    }
   
}
