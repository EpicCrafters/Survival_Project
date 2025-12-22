using System.Collections.Generic;
using UnityEngine;

public class CraftingManager : MonoBehaviour
{
    public static CraftingManager Instance { get; private set; }
    [SerializeField] private InventoryManager inventory;

    private Dictionary<ItemData, int> craftingBuffer = new();

    [Header("Crafting Slots")]
    public InventorySlot[] craftingSlots;

    [Header("Recipes")]
    public CraftingRecipe[] recipes;

    [Header("UI")]
    public Transform recipeListParent;
    public GameObject recipeButtonPrefab;

    private readonly List<CraftingRecipe> availableRecipes = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
    private void Start()
    {
        for (int i = 0; i < craftingSlots.Length; i++)
        {
            craftingSlots[i].index = i;
        }
    }

    // ================= CORE =================

    public void OnCraftingSlotChanged()
    {
        UpdateAvailableRecipes();
    }
    public void UpdateAvailableRecipes()
    {
        availableRecipes.Clear();

        foreach (var recipe in recipes)
        {
            if (CanCraft(recipe))
                availableRecipes.Add(recipe);
        }

        RefreshRecipeListUI();
    }

    private bool CanCraft(CraftingRecipe recipe)
    {
        foreach (var ing in recipe.ingredients)
        {
            if (CountInCraftingSlots(ing.item) < ing.amount)
                return false;
        }
        return true;
    }

    private int CountInCraftingSlots(ItemData item)
    {
        int total = 0;

        foreach (var slot in inventory.CraftingSlots)
        {
            var stack = inventory.controller.GetSlot(slot.index);
            if (stack != null && stack.data == item)
                total += stack.count;
        }

        return total;
    }

    // ================= BUFFER =================

    private void RebuildCraftingBuffer()
    {
        craftingBuffer.Clear();

        foreach (var slot in craftingSlots)
        {
            var ui = slot.GetComponentInChildren<InventoryItem>();
            if (ui == null) continue;

            // LẤY TỪ ItemStack, KHÔNG TỪ TEXT
            int count = ui.GetCount(); //  cần thêm hàm này

            if (!craftingBuffer.ContainsKey(ui.ItemData))
                craftingBuffer[ui.ItemData] = 0;

            craftingBuffer[ui.ItemData] += count;
        }
    }

    // ================= CRAFT =================
    public void Craft(CraftingRecipe recipe)
    {
        if (!CanCraft(recipe))
            return;

        // Trừ nguyên liệu TỪ CRAFTING SLOTS
        foreach (var ing in recipe.ingredients)
        {
            int remain = ing.amount;

            foreach (var slot in inventory.CraftingSlots)
            {
                int idx = slot.index;
                var stack = inventory.controller.GetSlot(idx);

                if (stack == null || stack.data != ing.item)
                    continue;

                int take = Mathf.Min(stack.count, remain);
                stack.count -= take;
                remain -= take;

                if (stack.count <= 0)
                    inventory.controller.slots[idx] = null;

                if (remain <= 0)
                    break;
            }
        }

        // Add result
        for (int i = 0; i < recipe.resultAmount; i++)
            inventory.AddItem(recipe.result);

        inventory.RedrawUI();
        UpdateAvailableRecipes();
    }

    public void ReturnItemsToInventory()
    {
        RebuildCraftingBuffer();

        foreach (var kv in craftingBuffer)
            InventoryManager.instance.controller.AddStack(
                new ItemStack(kv.Key, kv.Value)
            );

        ClearCraftingSlots();
        InventoryManager.instance.RedrawUI();
    }

    // ================= UI =================

    private void ClearCraftingSlots()
    {
        craftingBuffer.Clear();

        foreach (var slot in craftingSlots)
        {
            var ui = slot.GetComponentInChildren<InventoryItem>();
            if (ui != null)
                Destroy(ui.gameObject);
        }
    }

    private void RefreshRecipeListUI()
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
