using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Scriptable Objects/Crafting Recipe")]
public class CraftingRecipe : ScriptableObject
{
    public List<ItemAmount> ingredients;
    public ItemData result;
    public int resultAmount = 1;
    //internal object resultItem;
}
[System.Serializable]
public class ItemAmount
{
    public ItemData item;
    public int amount = 1;

    [Tooltip("Có bị tiêu hao khi craft không (tool = false)")]
    public bool consume = true;
}

