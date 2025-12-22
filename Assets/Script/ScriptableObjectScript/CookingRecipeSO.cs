using UnityEngine;
using System.Collections.Generic;

// ==========================================
//  CAMPFIRE RECIPE (Simple: 1 input -> 1-2 outputs)
// ==========================================
[CreateAssetMenu(fileName = "New Campfire Recipe", menuName = "Cooking/Campfire Recipe")]
public class CampfireRecipeSO : ScriptableObject
{
    [Header("Recipe Info")]
    public string recipeName;

    [Header("Ingredient (Input)")]
    [Tooltip("Single ingredient needed (e.g., raw meat, raw fish)")]
    public ItemData rawIngredient;

    [Header("Result (Output)")]
    [Tooltip("Item produced when successfully cooked")]
    public ItemData cookedResult;

    [Header("Cooking Times")]
    [Tooltip("Time to cook the item")]
    public float cookTime = 8f;

    [Header("Burning")]
    [Tooltip("Can this recipe burn?")]
    public bool canBurn = true;
    [Tooltip("Additional time AFTER cooking before it burns (e.g., 7 means it burns 7 seconds after becoming cooked)")]
    public float burnTime = 7f;
    [Tooltip("Item produced when burned")]
    public ItemData burnedResult;

    [Header("Visual Prefabs")]
    [Tooltip("Prefab to show while raw/cooking")]
    public GameObject rawVisualPrefab;
    [Tooltip("Prefab to show when cooked")]
    public GameObject cookedVisualPrefab;
    [Tooltip("Prefab to show when burned")]
    public GameObject burnedVisualPrefab;

    /// <summary>
    /// Check if the given item can be cooked with this recipe
    /// </summary>
    public bool CanAcceptIngredient(ItemData item)
    {
        return rawIngredient != null && item != null && rawIngredient.id == item.id;
    }
}

// ==========================================
//  LEGACY SUPPORT - For future complex recipes
// ==========================================
[System.Serializable]
public class RecipeIngredient
{
    public ItemData item;
    public int amount = 1;
}