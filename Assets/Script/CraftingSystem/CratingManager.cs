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
    public void Bind(InventoryData data, InventorySlot[] craftingSlotUIs)
    {
        inventoryData = data;

        List<int> indices = new();
        foreach (var slot in craftingSlotUIs)
            indices.Add(slot.index);

        inventoryData.CmdRegisterCraftingSlots(indices.ToArray());
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

        // client chỉ gửi request
        int recipeIndex = System.Array.IndexOf(recipes, recipe);
        if (recipeIndex < 0) return;

        inventoryData.CmdCraft(recipeIndex);
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
    
}
