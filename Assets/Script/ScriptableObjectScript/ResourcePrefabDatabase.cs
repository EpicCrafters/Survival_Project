using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class PrefabEntry
{
    public string path;      // your key (e.g. "Assets/Prefab/.../Dau_Short_2.prefab" or "Prefabs/Dau_Short_2")
    public GameObject prefab;
}

[CreateAssetMenu(fileName = "ResourcePrefabDatabase", menuName = "Scriptable Objects/ResourcePrefabDatabase")]
public class ResourcePrefabDatabase : ScriptableObject
{
    public List<PrefabEntry> entries = new List<PrefabEntry>();

    // runtime cache
    private Dictionary<string, GameObject> cache;

    private void OnEnable()
    {
        BuildCache();
    }

    private void BuildCache()
    {
        cache = new Dictionary<string, GameObject>(entries.Count);
        foreach (var e in entries)
        {
            if (e == null || e.prefab == null || string.IsNullOrEmpty(e.path)) continue;
            var key = NormalizeKey(e.path);
            if (!cache.ContainsKey(key)) cache[key] = e.prefab;
        }
    }

    private string NormalizeKey(string p)
    {
        if (string.IsNullOrEmpty(p)) return p;
        p = p.Replace('\\', '/').Trim();
        // optional: remove extension so keys can be either with/without ".prefab"
        // p = System.IO.Path.ChangeExtension(p, null);
        return p;
    }

    public GameObject GetPrefab(string path)
    {
        if (cache == null) BuildCache();
        if (string.IsNullOrEmpty(path)) return null;
        cache.TryGetValue(NormalizeKey(path), out var prefab);
        return prefab;
    }
    // NEW: Get path for a prefab
    public string GetPathForPrefab(GameObject prefab)
    {
        if (prefab == null) return null;

        foreach (var entry in entries)
        {
            if (entry.prefab == prefab)
                return entry.path;
        }

        return null;
    }

    // NEW: Get entry by prefab
    public PrefabEntry GetEntryByPrefab(GameObject prefab)
    {
        if (prefab == null) return null;

        foreach (var entry in entries)
        {
            if (entry.prefab == prefab)
                return entry;
        }

        return null;
    }

    // NEW: Get all paths (for debugging)
    public List<string> GetAllPaths()
    {
        var paths = new List<string>();
        foreach (var entry in entries)
        {
            if (entry != null && !string.IsNullOrEmpty(entry.path))
                paths.Add(entry.path);
        }
        return paths;
    }
}
