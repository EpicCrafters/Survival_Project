using System.Collections.Generic;
using UnityEngine;

public class ItemDatabase : MonoBehaviour
{
    public static ItemDatabase Instance;

    [SerializeField] private List<ItemData> allItems = new List<ItemData>();
    private Dictionary<int, ItemData> itemDict = new Dictionary<int, ItemData>();

    private void Awake()
    {
        if (Instance == null) Instance = this;

        // Chuyển list thành dictionary để truy cập nhanh bằng id
        foreach (var item in allItems)
        {
            if (!itemDict.ContainsKey(item.id))
            {
                itemDict.Add(item.id, item);
            }
            else
            {
                Debug.LogWarning($"Duplicate ID found: {item.id} in {item.itemName}");
            }
        }
    }

    public ItemData GetItemById(int id)
    {
        if (itemDict.TryGetValue(id, out ItemData item))
        {
            return item;
        }
        Debug.LogWarning($"Item with ID {id} not found!");
        return null;
    }

    public static ItemData Get(int id)
    {
        if (Instance == null)
        {
            Debug.LogError("ItemDatabase chưa được khởi tạo!");
            return null;
        }
        return Instance.GetItemById(id);
    }
    public static ItemData GetById(int id) => Instance.GetItemById(id);
    public static GameObject GetPrefabById(int id) => GetById(id)?.worldPrefab;
}
