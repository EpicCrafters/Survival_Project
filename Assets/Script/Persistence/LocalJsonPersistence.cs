// LocalJsonPersistence.cs
using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Local JSON persistence (atomic write). Works in Editor and in builds.
/// Construct with optional spawnJsonAsset (TextAsset) and spawnJsonPath (string).
/// If spawnJsonAsset is provided in Editor, the asset path will be resolved to Application.dataPath
/// so the file written mirrors the project asset file. Otherwise the provided spawnJsonPath or
/// Application.persistentDataPath is used.
/// </summary>
public class LocalJsonPersistence : IResourcePersistence
{
    readonly TextAsset spawnJsonAsset;
    readonly string spawnJsonPath;
    readonly bool verbose;

    public LocalJsonPersistence(TextAsset spawnJsonAsset = null, string spawnJsonPath = null, bool verbose = true)
    {
        this.spawnJsonAsset = spawnJsonAsset;
        this.spawnJsonPath = spawnJsonPath;
        this.verbose = verbose;
    }

    public SpawnRecordCollection Load()
    {
        string resolvedPath = ResolveFullPath();
        if (verbose) Debug.Log($"[LocalJsonPersistence] Load -> {resolvedPath}");

        string json = null;
#if UNITY_EDITOR
        if (spawnJsonAsset != null)
        {
            json = spawnJsonAsset.text;
            if (verbose) Debug.Log("[LocalJsonPersistence] Using spawnJsonAsset.text for load.");
        }
#endif
        if (string.IsNullOrEmpty(json))
        {
            if (!File.Exists(resolvedPath))
            {
                if (verbose) Debug.LogWarning($"[LocalJsonPersistence] file not found: {resolvedPath} (returning empty container)");
                return new SpawnRecordCollection();
            }
            try
            {
                json = File.ReadAllText(resolvedPath);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LocalJsonPersistence] Failed to read file: {ex.Message}");
                return new SpawnRecordCollection();
            }
        }

        try
        {
            var container = JsonUtility.FromJson<SpawnRecordCollection>(json);
            return container ?? new SpawnRecordCollection();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[LocalJsonPersistence] JSON parse failed: {ex.Message}");
            return new SpawnRecordCollection();
        }
    }

    public void Save(SpawnRecordCollection container)
    {
        string resolvedPath = ResolveFullPath();
        if (verbose) Debug.Log($"[LocalJsonPersistence] Save -> {resolvedPath}");

        string outJson = JsonUtility.ToJson(container, true);

        try
        {
            var dir = Path.GetDirectoryName(resolvedPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            string tmp = resolvedPath + ".tmp";
            File.WriteAllText(tmp, outJson);

            // Replace original (best-effort)
            if (File.Exists(resolvedPath)) File.Delete(resolvedPath);
            File.Move(tmp, resolvedPath);

#if UNITY_EDITOR
            // If we saved into Assets/..., refresh so the editor shows the file
            if (resolvedPath.StartsWith(Application.dataPath))
                AssetDatabase.Refresh();
#endif
            if (verbose) Debug.Log("[LocalJsonPersistence] Save complete.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[LocalJsonPersistence] Save failed: {ex.Message}");
            // fallback (non-atomic)
            try { File.WriteAllText(resolvedPath, outJson); }
            catch (Exception ex2) { Debug.LogError($"[LocalJsonPersistence] Fallback save failed: {ex2.Message}"); }
        }
    }

    string ResolveFullPath()
    {
#if UNITY_EDITOR
        if (spawnJsonAsset != null)
        {
            string assetPath = AssetDatabase.GetAssetPath(spawnJsonAsset); // e.g. "Assets/SpawnData/spawn_expanded.json"
            if (!string.IsNullOrEmpty(assetPath))
            {
                string relative = assetPath.StartsWith("Assets/") ? assetPath.Substring("Assets/".Length) : assetPath;
                return Path.Combine(Application.dataPath, relative);
            }
        }
#endif
        if (!string.IsNullOrEmpty(spawnJsonPath))
        {
            if (spawnJsonPath.StartsWith("Assets/"))
            {
                string relative = spawnJsonPath.Substring("Assets/".Length);
                return Path.Combine(Application.dataPath, relative);
            }
            // If absolute path, return as-is
            return spawnJsonPath;
        }

        // default to persistent data path so host saves outside project assets in builds
        return Path.Combine(Application.persistentDataPath, "spawn_expanded.json");
    }
}
