using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RecipeButton : MonoBehaviour
{
    public Image icon;
    public TMP_Text nameText;
    public Button craftButton;

    private CraftingRecipe recipe;
    private CraftingManager craftingManager;

    public void Setup(CraftingRecipe recipe, CraftingManager manager)
    {
        this.recipe = recipe;
        this.craftingManager = manager;

        icon.sprite = recipe.result.image;
        nameText.text = recipe.result.itemName;

        craftButton.onClick.RemoveAllListeners();
        craftButton.onClick.AddListener(OnCraft);
    }

    private void OnCraft()
    {
        craftingManager.Craft(recipe);
    }
}
