using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(menuName = "Scriptable Objects/Item")]
public class Item : ScriptableObject
{
    [Header("Basic Info")]
    public string itemName;
    public Sprite image;

    [Header("Item Settings")]
    public int damage;
    public ItemType type;
    public List<ActionType> actions = new List<ActionType>();

    public Vector2Int range = new Vector2Int(5, 4);

    [Header("Stacking")]
    public bool stackable;
    public int maxStack = 10;

    

    [Header("Carrying Behavior")]
    public CarryMode carryMode;


    public GameObject heldPrefab;
}

public enum ItemType
{
    Tool,
    Building,
    Resource,
    Consumable,
}

public enum ActionType
{
    None,
    Attack,
    Chop,
    Mine,
    Craft,
    Eat
}

public enum CarryMode
{
    Inventory,   // Goes into inventory (e.g. food, axe)
    Carryable,   // Held physically (e.g. log, rock)
    Both         // Can be stored or carried (e.g. stick)
}
