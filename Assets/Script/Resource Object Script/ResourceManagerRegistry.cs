using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central registry for ResourceManager instances in the current Unity process.
/// Each ResourceManager should register itself (by sceneName or a custom key) on Awake and unregister on OnDestroy.
/// This avoids fragile FindObjectOfType across scenes and makes lookups deterministic.
/// </summary>
public static class ResourceManagerRegistry
{
    private static readonly Dictionary<string, ResourceManager> managersByScene = new Dictionary<string, ResourceManager>();
    private static readonly object _lock = new object();

    public static void RegisterManager(string sceneName, ResourceManager manager)
    {
        if (string.IsNullOrEmpty(sceneName) || manager == null) return;
        lock (_lock)
        {
            managersByScene[sceneName] = manager;
        }
        Debug.Log($"[ResourceManagerRegistry] Registered manager for scene '{sceneName}'");
    }

    public static void UnregisterManager(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return;
        lock (_lock)
        {
            managersByScene.Remove(sceneName);
        }
        Debug.Log($"[ResourceManagerRegistry] Unregistered manager for scene '{sceneName}'");
    }

    public static ResourceManager GetManagerForScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return null;
        lock (_lock)
        {
            managersByScene.TryGetValue(sceneName, out var rm);
            return rm;
        }
    }

    public static List<ResourceManager> GetAllManagers()
    {
        lock (_lock) return new List<ResourceManager>(managersByScene.Values);
    }

    /// <summary>
    /// Scans all registered managers to find one that contains the given uniqueId in its core snapshot.
    /// This is O(N) across managers but is fine when manager count is small (per streamed region).
    /// </summary>
    public static ResourceManager GetManagerForUniqueId(string uniqueId)
    {
        if (string.IsNullOrEmpty(uniqueId)) return null;
        lock (_lock)
        {
            foreach (var kv in managersByScene)
            {
                try
                {
                    var core = kv.Value.core;
                    if (core != null && core.recordsById != null && core.recordsById.ContainsKey(uniqueId))
                        return kv.Value;
                }
                catch { /* best-effort */ }
            }
        }
        return null;
    }
}
