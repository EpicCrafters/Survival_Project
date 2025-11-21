using System.Collections.Generic;
using UnityEngine;

public static class ItemUseStrategyFactory
{
    // Cache lưu trữ sẵn Strategy đã tạo
    // Key là string (ItemType + WeaponType) để mỗi loại item có 1 strategy riêng
    private static Dictionary<string, IItemUseStrategy> strategyCache = new Dictionary<string, IItemUseStrategy>();

    public static IItemUseStrategy GetStrategy(ItemData itemData)
    {
        if (itemData == null)
            return null;

        // ✅ Tạo key duy nhất cho item (dựa vào loại item + weaponType nếu là vũ khí)
        string key = GetCacheKey(itemData);

        // Nếu Strategy đã tồn tại thì dùng lại (không tạo mới)
        if (strategyCache.TryGetValue(key, out var cachedStrategy))
            return cachedStrategy;

        // Nếu chưa có thì tạo Strategy mới
        IItemUseStrategy strategy = CreateStrategy(itemData);

        // Lưu vào cache để sử dụng lần sau
        if (strategy != null)
            strategyCache[key] = strategy;

        return strategy;
    }

    // ===========================================================
    // Hàm tạo key duy nhất cho từng loại item
    private static string GetCacheKey(ItemData itemData)
    {
        // Nếu là Weapon thì ghép ItemType + WeaponType
        if (itemData.type == ItemType.Weapon && itemData.weapon != null)
            return $"{itemData.type}_{itemData.weapon.weaponType}";

        // Trường hợp khác chỉ cần ItemType
        return itemData.type.ToString();
    }

    // ===========================================================
    // Hàm tạo Strategy mới khi chưa có trong cache
    private static IItemUseStrategy CreateStrategy(ItemData itemData)
    {
        switch (itemData.type)
        {
            case ItemType.Weapon:
                if (itemData.weapon != null)
                {
                    if (itemData.weapon.weaponType == WeaponType.Bow)
                    {
                        Debug.Log("[Factory] Bow Strategy created");
                        return new WeaponBowStrategy();   // Strategy dùng cho cung
                    }
                    else
                    {
                        Debug.Log("[Factory] Melee Strategy created");
                        return new WeaponMeleeStrategy(); // Strategy dùng cho vũ khí cận chiến
                    }
                }
                // Nếu weapon null → fallback melee
                return new WeaponMeleeStrategy();

            case ItemType.Consumable:
                Debug.Log("[Factory] Consumable Strategy created");
                return new ConsumableStrategy();         // Strategy cho đồ dùng Food

            case ItemType.BuildingPart:
                return new BuildingPlacementStrategy();
            // Chưa làm

           // case ItemType.Tool:
               // Debug.Log("[Factory] Tool Strategy created (melee fallback)");
               //return new WeaponMeleeStrategy();         // Tool tạm dùng chiến lược melee

            default:
                Debug.LogWarning($"[Factory] No strategy defined for item type: {itemData.type}");
                return null;
        }
    }

    // ===========================================================
    // Xóa hết cache (khi đổi scene, đổi item database, reload game)
    public static void ClearCache()
    {
        strategyCache.Clear();
        Debug.Log("[Factory] Strategy cache cleared");
    }
}
