using System.Collections.Generic;
using UnityEngine;

public enum ItemType { Weapon, Tool, Consumable, Resource, BuildingPart }
public enum WeaponType { Sword, Spear, BattleAxe, Bow }
public enum ToolType { Hammer, Pickaxe, Axe }
public enum BuildingPartType { Foundation, Wall, Floor, Roof, Door, Window }

[System.Serializable]
public class ComboData
{
    public AnimationClip animation;
}

[System.Serializable]
public class KnockbackSettings
{
    [Tooltip("Lực knockback ngang (forward force)")]
    public float horizontalForce = 800f;

    [Tooltip("Lực knockback hướng lên (upward force)")]
    public float upwardForce = 200f;

    [Tooltip("Bán kính tìm ragdoll bone gần hit point")]
    public float boneSearchRadius = 1.5f;

    [Tooltip("Có apply knockback khi giết chết không?")]
    public bool enableKnockback = true;
}

[System.Serializable]
public class WeaponStats
{
    public WeaponType weaponType;
    public int damage;
    public float range;

    [Header("Combo Settings")]
    public ComboData[] combos;

    [Header("Bow/Ranged Settings (Only for Bow/Crossbow)")]
    [Tooltip("Projectile data containing damage ranges, speed, and prefabs")]
    public ProjectileData projectileData;

    [Tooltip("Maximum time to fully charge the bow (affects charge percentage)")]
    public float maxChargeTime = 1.5f;

    [Header("Legacy Bow Settings (Deprecated - Use ProjectileData instead)")]
    [Tooltip("⚠️ DEPRECATED: Use projectileData.projectilePrefab instead")]
    public GameObject arrowProjectilePrefab;

    [Tooltip("⚠️ DEPRECATED: Use projectileData.visualPrefab instead")]
    public GameObject arrowVisualPrefab;

    [Header("Knockback Settings")]
    public KnockbackSettings knockback = new KnockbackSettings();

    public ProjectileData GetProjectileData()
    {
        if (projectileData != null)
            return projectileData;

        if (arrowProjectilePrefab != null)
        {
            Debug.LogWarning("[WeaponStats] Using legacy arrow prefabs. Please migrate to ProjectileData!");

            ProjectileData tempData = ScriptableObject.CreateInstance<ProjectileData>();
            tempData.projectileName = "Legacy Arrow";
            tempData.projectilePrefab = arrowProjectilePrefab;
            tempData.visualPrefab = arrowVisualPrefab;
            tempData.minDamage = damage * 0.5f;
            tempData.maxDamage = damage;
            tempData.minSpeed = 15f;
            tempData.maxSpeed = 30f;

            return tempData;
        }

        return null;
    }
}

[System.Serializable]
public class ToolStats
{
    public ToolType toolType;
    public int damage;

    [Header("Knockback Settings")]
    public KnockbackSettings knockback = new KnockbackSettings();
}

[System.Serializable]
public class ConsumableStats
{
    [Header("Effects")]
    public int healAmount;
    public int fillAmount;

    [Tooltip("How much hunger to restore")]
    public float hungerRestoreAmount = 20f;

    [Header("Stacking")]
    [Tooltip("Can this consumable stack in inventory?")]
    public bool stackable = true;

    [Tooltip("Maximum stack size (default 99 for consumables)")]
    public int maxStack = 99;
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
    public int verticalOffset = 0;
    public Vector3 placementAnchorOffset;
    public float maxCornerDrop = 0.4f;
    public bool raiseToHighest = true;
    public ScriptableObject placementRole;
}

[CreateAssetMenu(menuName = "Scriptable Objects/Item")]
public class ItemData : ScriptableObject
{
    [Header("Basic Info")]
    public int id;
    public string itemName;
    public Sprite image;
    public ItemType type;

    [Header("Prefabs")]
    public GameObject worldPrefab;
    public GameObject heldPrefab;

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

    
    public KnockbackSettings GetKnockbackSettings()
    {
        if (type == ItemType.Weapon && weapon != null)
            return weapon.knockback;

        if (type == ItemType.Tool && tool != null)
            return tool.knockback;

        return new KnockbackSettings();
    }

    
    public ProjectileData GetProjectileData()
    {
        if (type == ItemType.Weapon && weapon != null && weapon.weaponType == WeaponType.Bow)
        {
            return weapon.GetProjectileData();
        }
        return null;
    }

    
    public bool IsStackable()
    {
        switch (type)
        {
            case ItemType.Consumable:
                return consumable != null && consumable.stackable;

            case ItemType.Resource:
                return resource != null && resource.stackable;

            case ItemType.BuildingPart:
                return building != null && building.stackable;

            default:
                return false;
        }
    }

   
    public int GetMaxStack()
    {
        switch (type)
        {
            case ItemType.Consumable:
                return consumable != null ? consumable.maxStack : 1;

            case ItemType.Resource:
                return resource != null ? resource.maxStack : 1;

            case ItemType.BuildingPart:
                return building != null ? building.maxStack : 1;

            default:
                return 1;
        }
    }
    private void OnValidate()
    {
        if (type == ItemType.Consumable && consumable == null)
        {
            consumable = new ConsumableStats();
        }
    }
#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(ItemData))]
    public class ItemDataEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            UnityEditor.EditorGUILayout.PropertyField(serializedObject.FindProperty("id"));
            UnityEditor.EditorGUILayout.PropertyField(serializedObject.FindProperty("itemName"));
            UnityEditor.EditorGUILayout.PropertyField(serializedObject.FindProperty("image"));
            UnityEditor.EditorGUILayout.PropertyField(serializedObject.FindProperty("type"));

            UnityEditor.EditorGUILayout.Space();
            UnityEditor.EditorGUILayout.PropertyField(serializedObject.FindProperty("worldPrefab"));
            UnityEditor.EditorGUILayout.PropertyField(serializedObject.FindProperty("heldPrefab"));

            UnityEditor.EditorGUILayout.PropertyField(serializedObject.FindProperty("itemPlace"));

            var itemTypeProp = serializedObject.FindProperty("type");
            var selectedType = (ItemType)itemTypeProp.enumValueIndex;

            switch (selectedType)
            {
                case ItemType.Weapon:
                    UnityEditor.EditorGUILayout.PropertyField(serializedObject.FindProperty("weapon"), true);

                    var weaponProp = serializedObject.FindProperty("weapon");
                    var weaponTypeProp = weaponProp.FindPropertyRelative("weaponType");
                    if (weaponTypeProp.enumValueIndex == (int)WeaponType.Bow)
                    {
                        var projectileDataProp = weaponProp.FindPropertyRelative("projectileData");
                        if (projectileDataProp.objectReferenceValue == null)
                        {
                            UnityEditor.EditorGUILayout.HelpBox(
                                "⚠️ No ProjectileData assigned! Bow weapons need ProjectileData to define damage ranges and projectile behavior.\n\n" +
                                "Create one: Right-click → Create → Combat → Projectile Data",
                                UnityEditor.MessageType.Warning
                            );
                        }
                        else
                        {
                            ProjectileData pData = projectileDataProp.objectReferenceValue as ProjectileData;
                            if (pData != null)
                            {
                                UnityEditor.EditorGUILayout.HelpBox(
                                    $"✅ Projectile: {pData.projectileName}\n" +
                                    $"Damage Range: {pData.minDamage} - {pData.maxDamage}\n" +
                                    $"Speed Range: {pData.minSpeed} - {pData.maxSpeed}",
                                    UnityEditor.MessageType.Info
                                );
                            }
                        }
                    }
                    break;

                case ItemType.Tool:
                    UnityEditor.EditorGUILayout.PropertyField(serializedObject.FindProperty("tool"), true);
                    break;

                case ItemType.Consumable:
                    UnityEditor.EditorGUILayout.PropertyField(serializedObject.FindProperty("consumable"), true);

                    // Show helpful info for consumables
                    var consumableProp = serializedObject.FindProperty("consumable");
                    var stackableProp = consumableProp.FindPropertyRelative("stackable");
                    var maxStackProp = consumableProp.FindPropertyRelative("maxStack");

                    if (stackableProp.boolValue)
                    {
                        UnityEditor.EditorGUILayout.HelpBox(
                            $"✅ Stackable: Yes (Max: {maxStackProp.intValue})\n" +
                            "Consumables like food and potions will stack in inventory.",
                            UnityEditor.MessageType.Info
                        );
                    }
                    else
                    {
                        UnityEditor.EditorGUILayout.HelpBox(
                            "⚠️ Not stackable - Each item will take a separate inventory slot.",
                            UnityEditor.MessageType.Warning
                        );
                    }
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