using System.Collections.Generic;
using UnityEngine;

public class CraftingManager : MonoBehaviour
{
    public static CraftingManager Instance { get; private set; }

    [Header("Crafting Slots")]
    public CraftingSlot[] craftingSlots;  // 6 ô Craft

    [Header("Recipes")]
    public CraftingRecipe[] recipes;       // Tất cả công thức

    [Header("UI")]
    public Transform recipeListParent;     // ListRecipe (Grid Layout)
    public GameObject recipeButtonPrefab;  // Prefab RecipeButton

    private List<CraftingRecipe> availableRecipes = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void UpdateAvailableRecipes()
    {

        availableRecipes.Clear();

        foreach (var recipe in recipes)
        {
            //Debug.Log($"<color=yellow>Check recipe:</color> {recipe.result.name}");
            if (HasIngredients(recipe))
            {
                //Debug.Log($"✅ Đủ nguyên liệu cho: {recipe.result.name}");
                availableRecipes.Add(recipe);
            }
            else
            {
                //Debug.Log($"❌ Thiếu nguyên liệu cho: {recipe.result.name}");
            }
        }

        RefreshRecipeListUI();
    }

    private bool HasIngredients(CraftingRecipe recipe)
    {
        foreach (var ingredient in recipe.ingredients)
        {
            int count = CountInCraftingSlots(ingredient.item);
            //Debug.Log($"   🔍 {ingredient.item.name} cần {ingredient.amount}, đang có {count}");
            if (count < ingredient.amount)
                return false;
        }
        return true;
    }

    private int CountInCraftingSlots(ItemData item)
    {
        int total = 0;
        foreach (var slot in craftingSlots)
        {
            InventoryItem invItem = slot.GetComponentInChildren<InventoryItem>();
            if (invItem != null)//neu o trong da co vat pham
            {
                //Debug.Log($"[Count] Slot: {slot.name} chứa {invItem.item?.itemName ?? "NULL"} số lượng {invItem.count}");
            }
            if (invItem != null && invItem.item == item)//o trong chua co item
            {
                //Debug.Log("tim thay item");
                total += invItem.count;
            }
        }
        //Debug.Log($"[Count Result] Tổng {item.itemName}: {total}");
        return total;
    }


    private void RefreshRecipeListUI()
    {
        //Debug.Log($"<color=cyan>[Crafting]</color> Làm mới UI Recipe. Số recipe khả dụng: {availableRecipes.Count}");

        foreach (Transform child in recipeListParent)
            Destroy(child.gameObject);

        foreach (var recipe in availableRecipes)
        {
            GameObject buttonGO = Instantiate(recipeButtonPrefab, recipeListParent);
            //Debug.Log($"<color=green Spawn RecipeButton:</color> {recipe.result.name}");
            RecipeButton rb = buttonGO.GetComponent<RecipeButton>();
            rb.Setup(recipe, this);
        }
    }

    public void ReturnItemsToInventory()
    {
        //Debug.Log("tra item ve inventory");
        foreach (var slot in craftingSlots)
        {
            var invItem = slot.GetComponentInChildren<InventoryItem>();
            if (invItem != null)
            {
                for (int i = 0; i < invItem.count; i++)
                    InventoryManager.instance.AddItem(invItem.item);

                Destroy(invItem.gameObject);
            }
        }

        UpdateAvailableRecipes();
    }

    public void Craft(CraftingRecipe recipe)
    {
        if (!HasIngredients(recipe))
        {
            //Debug.LogWarning("Thiếu nguyên liệu!");
            return;
        }

        foreach (var ingredient in recipe.ingredients)
        {
            int amountLeft = ingredient.amount;

            foreach (var slot in craftingSlots)
            {
                var invItem = slot.GetComponentInChildren<InventoryItem>();
                if (invItem != null && invItem.item == ingredient.item)
                {
                    int used = Mathf.Min(amountLeft, invItem.count);
                    invItem.count -= used;
                    amountLeft -= used;

                    if (invItem.count <= 0)
                        Destroy(invItem.gameObject);
                    else
                        invItem.RefreshCount();

                    if (amountLeft <= 0) break;
                }
            }
        }

        for (int i = 0; i < recipe.resultAmount; i++)
        {
            InventoryManager.instance.AddItem(recipe.result);
        }

        //Debug.Log($"<color=green> Đã craft:</color> {recipe.result.name} x{recipe.resultAmount}");

        UpdateAvailableRecipes();
    }
}
