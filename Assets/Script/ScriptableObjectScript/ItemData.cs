using JetBrains.Annotations;
using System.Collections.Generic;
using UnityEngine;

public enum ItemType
{
    Weapon,
    Tool,
    Consumable,
    Resource,
    BuildingPart
}


public enum WeaponType
{
    Sword,
    Spear,
    BattleAxe
}

public enum ToolType
{
    Hammer,
    Pickaxe,
    Axe
}

public enum BuildingPartType
{
    Foundation,
    Wall,
    Floor,
    Roof,
    Door,
    Window
}



[System.Serializable]
public class ComboData
{
    public AnimationClip animation;
    public int damage;
    public float cooldown;
}

[System.Serializable]
public class WeaponStats
{
    public WeaponType weaponType;
    public int damage;
    public float range;

    [Header("Combo Settings")]
    public ComboData[] combos;
}




[System.Serializable]

public class ToolStats
{
    public ToolType toolType;
    public int damage;
}

[System.Serializable]
public class ConsumableStats
{
    public int healAmount;
    public int fillAmount;
}

[System.Serializable]
public class ResourceStats
{
    public bool stackable;
    public int maxStack = 10;
    
}
[System.Serializable]
public class BuildingStats
{
    public BuildingPartType partType;
    public bool stackable;
    public int maxStack = 5;
    public bool snapToGridEdge;
    public List<ItemData> ignorObject;
    public LayerMask groundMask;
}

[CreateAssetMenu(menuName = "Scriptable Objects/Item")]
public class ItemData : ScriptableObject
{
    [Header("Basic Info")]
    public string itemName;
    public Sprite image;
    public ItemType type;
    public GameObject worldPrefab;
    public bool itemPlace;
    public bool snapToGrid = true;
    public float gridSize = 2f;

    [Header("Weapon")]
    public WeaponStats weapon;

    [Header("Tool")]
    public ToolStats tool;

    [Header("Consumable")]
    public ConsumableStats consumable;

    [Header("Resource")]
    public ResourceStats resource;

    [Header("Building")]
    public BuildingStats building;

#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(ItemData))]
    public class ItemDataEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            UnityEditor.EditorGUILayout.PropertyField(serializedObject.FindProperty("itemName"));
            UnityEditor.EditorGUILayout.PropertyField(serializedObject.FindProperty("image"));
            UnityEditor.EditorGUILayout.PropertyField(serializedObject.FindProperty("worldPrefab"));
            UnityEditor.EditorGUILayout.PropertyField(serializedObject.FindProperty("type"));
            UnityEditor.EditorGUILayout.PropertyField(serializedObject.FindProperty("itemPlace"));

            var itemTypeProp = serializedObject.FindProperty("type");
            var selectedType = (ItemType)itemTypeProp.enumValueIndex;

            switch (selectedType)
            {
                case ItemType.Weapon:
                    UnityEditor.EditorGUILayout.PropertyField(serializedObject.FindProperty("weapon"), true);
                    break;
                case ItemType.Tool:
                    UnityEditor.EditorGUILayout.PropertyField(serializedObject.FindProperty("tool"), true);
                    break;
                case ItemType.Consumable:
                    UnityEditor.EditorGUILayout.PropertyField(serializedObject.FindProperty("consumable"), true);
                    break;
                case ItemType.Resource:
                    UnityEditor.EditorGUILayout.PropertyField(serializedObject.FindProperty("resource"), true);
                    break;
                case ItemType.BuildingPart:
                    UnityEditor.EditorGUILayout.PropertyField(serializedObject.FindProperty("building"), true);
                    break;
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
#endif
}
