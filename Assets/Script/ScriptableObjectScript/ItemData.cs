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
    public GameObject arrowProjectilePrefab; // Server-spawned projectile

    [Tooltip("⚠️ DEPRECATED: Use projectileData.visualPrefab instead")]
    public GameObject arrowVisualPrefab;     // Visual on string

    [Header("Knockback Settings")]
    public KnockbackSettings knockback = new KnockbackSettings();

    /// <summary>
    /// Get the projectile data with fallback to legacy prefabs
    /// </summary>
    public ProjectileData GetProjectileData()
    {
        // If we have projectile data, use it
        if (projectileData != null)
            return projectileData;

        // Fallback: Create temporary projectile data from legacy fields
        if (arrowProjectilePrefab != null)
        {
            Debug.LogWarning("[WeaponStats] Using legacy arrow prefabs. Please migrate to ProjectileData!");

            // Create a runtime-only ProjectileData (not saved to disk)
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
    public int healAmount;
    public int fillAmount;
    [Tooltip("How much hunger to restore")]
    public float hungerRestoreAmount = 20f;
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
    public Vector3 placementAnchorOffset;     // local offset (child pivot) để align prefab với slot
    public float maxCornerDrop = 0.4f;    // độ chênh tối đa giữa các góc cho phép
    public bool raiseToHighest = true;    // có nâng object lên góc cao nhất không
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
    public GameObject worldPrefab; // ✅ For world pickups
    public GameObject heldPrefab;  // ✅ For held visuals (in player hand)

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

    /// <summary>
    /// Helper method to get knockback settings
    /// </summary>
    public KnockbackSettings GetKnockbackSettings()
    {
        if (type == ItemType.Weapon && weapon != null)
            return weapon.knockback;

        if (type == ItemType.Tool && tool != null)
            return tool.knockback;

        // Default knockback if no settings
        return new KnockbackSettings();
    }

    /// <summary>
    /// Helper method to get projectile data for ranged weapons
    /// </summary>
    public ProjectileData GetProjectileData()
    {
        if (type == ItemType.Weapon && weapon != null && weapon.weaponType == WeaponType.Bow)
        {
            return weapon.GetProjectileData();
        }
        return null;
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

                    // Show helpful info for bow weapons
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