using System.Collections.Generic;
using UnityEngine;

public class CraftingManager : MonoBehaviour
{
    public static CraftingManager Instance;

    private InventoryData inventoryData;

    [Header("Crafting Slots (UI)")]
    [SerializeField] private InventorySlot[] craftingSlots;

    [Header("Recipes")]
    public CraftingRecipe[] recipes;

    [Header("UI")]
    public Transform recipeListParent;
    public GameObject recipeButtonPrefab;

    private readonly List<CraftingRecipe> availableRecipes = new();
    private InventorySlot[] inventorySlots; // slot thường (không crafting)

    public void Bind(
        InventoryData data,
        InventorySlot[] craftingSlotUIs,
        InventorySlot[] inventorySlotUIs
    )
    {
        inventoryData = data;
        craftingSlots = craftingSlotUIs;
        inventorySlots = inventorySlotUIs;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
    

    // ================= CORE =================

    public void OnCraftingSlotChanged()
    {
        UpdateAvailableRecipes();
    }

    void UpdateAvailableRecipes()
    {
        if (inventoryData == null) return;

        availableRecipes.Clear();

        foreach (var recipe in recipes)
        {
            if (CanCraft(recipe))
                availableRecipes.Add(recipe);
        }

        RefreshRecipeListUI();
    }

    bool CanCraft(CraftingRecipe recipe)
    {
        foreach (var ing in recipe.ingredients)
        {
            if (CountItemInCrafting(ing.item) < ing.amount)
                return false;
        }
        return true;
    }
    int CountItemInCrafting(ItemData item)
    {
        if (inventoryData == null || craftingSlots == null)
            return 0;

        int total = 0;

        foreach (var slotUI in craftingSlots)
        {
            int index = slotUI.index;
            var slot = inventoryData.GetSlot(index);

            if (slot.itemId == item.id)
                total += slot.count;
        }

        return total;
    }

    // ================= CRAFT =================

    public void Craft(CraftingRecipe recipe)
    {
        if (inventoryData == null) return;
        if (!CanCraft(recipe)) return;

        ConsumeIngredients(recipe);
        AddResult(recipe.result, recipe.resultAmount);
    }

    void ConsumeIngredients(CraftingRecipe recipe)
    {
        foreach (var ing in recipe.ingredients)
        {
            int remain = ing.amount;

            foreach (var slotUI in craftingSlots)
            {
                int index = slotUI.index;
                var slot = inventoryData.GetSlot(index);

                if (slot.itemId != ing.item.id) continue;

                int take = Mathf.Min(slot.count, remain);
                slot.count -= take;
                remain -= take;

                inventoryData.slots[index] =
                    slot.count <= 0
                        ? new InventoryData.SlotState { itemId = -1, count = 0 }
                        : slot;
            }
        }
    }
    void AddResult(ItemData result, int amount)
    {
        // 1️ Gộp vào slot inventory thường
        foreach (var invSlotUI in inventorySlots)
        {
            if (amount <= 0) break;

            int index = invSlotUI.index;
            var slot = inventoryData.GetSlot(index);

            if (slot.itemId == result.id)
            {
                slot.count += amount;
                inventoryData.slots[index] = slot;
                return;
            }
        }

        // 2️ Nếu chưa có, tìm slot trống
        foreach (var invSlotUI in inventorySlots)
        {
            if (amount <= 0) break;

            int index = invSlotUI.index;
            var slot = inventoryData.GetSlot(index);

            if (slot.itemId < 0)
            {
                inventoryData.slots[index] =
                    new InventoryData.SlotState
                    {
                        itemId = result.id,
                        count = amount
                    };
                return;
            }
        }
    }

    void RefreshRecipeListUI()
    {
        foreach (Transform child in recipeListParent)
            Destroy(child.gameObject);

        foreach (var recipe in availableRecipes)
        {
            var go = Instantiate(recipeButtonPrefab, recipeListParent);
            go.GetComponent<RecipeButton>().Setup(recipe, this);
        }
    }
    public void ReturnCraftingItems()
    {
        if (inventoryData == null) return;

        foreach (var craftSlotUI in craftingSlots)
        {
            int craftIndex = craftSlotUI.index;
            var craftSlot = inventoryData.GetSlot(craftIndex);

            if (craftSlot.itemId < 0 || craftSlot.count <= 0)
                continue;

            int remain = craftSlot.count;

            // 1️ Thử gộp vào slot inventory thường
            foreach (var invSlotUI in inventorySlots)
            {
                if (remain <= 0) break;

                int invIndex = invSlotUI.index;
                var invSlot = inventoryData.GetSlot(invIndex);

                if (invSlot.itemId == craftSlot.itemId)
                {
                    invSlot.count += remain;
                    inventoryData.slots[invIndex] = invSlot;
                    remain = 0;
                }
            }

            // 2️ Nếu chưa hết, tìm slot trống
            foreach (var invSlotUI in inventorySlots)
            {
                if (remain <= 0) break;

                int invIndex = invSlotUI.index;
                var invSlot = inventoryData.GetSlot(invIndex);

                if (invSlot.itemId < 0)
                {
                    inventoryData.slots[invIndex] =
                        new InventoryData.SlotState
                        {
                            itemId = craftSlot.itemId,
                            count = remain
                        };
                    remain = 0;
                }
            }

            // 3️ Clear crafting slot
            inventoryData.slots[craftIndex] =
                new InventoryData.SlotState { itemId = -1, count = 0 };
        }
    }

}
