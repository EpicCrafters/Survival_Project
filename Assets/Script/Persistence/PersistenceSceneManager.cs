using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Robust PersistenceSceneManager — finds all loaded ResourceManager instances across loaded scenes (including additive),
/// logs exactly what it found, and calls SaveNow() on each one. Attach in your persistence scene and wire OnSaveButtonPressed()
/// to the Save button's OnClick.
/// </summary>
public class PersistenceSceneManager : MonoBehaviour
{
    [Header("Behavior")]
    public bool staggerSaves = true;
    public bool useDelayBetween = false;
    public float delayBetweenSeconds = 0.05f;

    [Header("Logging")]
    public bool verbose = true;

    /// <summary>
    /// Called by the UI Button (OnClick). Kicks off the save process.
    /// </summary>
    public void OnSaveButtonPressed()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[PersistenceSceneManager] OnSaveButtonPressed called outside play mode.");
            return;
        }

        if (verbose) Debug.Log("[PersistenceSceneManager] OnSaveButtonPressed invoked.");

        if (staggerSaves)
            StartCoroutine(SaveAllCoroutine());
        else
            SaveAllImmediate();
    }

    public void SaveAllImmediate()
    {
        var managers = FindLoadedResourceManagers();
        if (managers == null || managers.Count == 0)
        {
            Debug.LogWarning("[PersistenceSceneManager] SaveAllImmediate: no ResourceManager instances found to save.");
            return;
        }

        if (verbose) Debug.Log($"[PersistenceSceneManager] SaveAllImmediate: saving {managers.Count} ResourceManager(s).");
        foreach (var rm in managers)
        {
            TrySaveManager(rm);
        }
        if (verbose) Debug.Log("[PersistenceSceneManager] SaveAllImmediate: completed.");
    }

    public IEnumerator SaveAllCoroutine()
    {
        var managers = FindLoadedResourceManagers();
        if (managers == null || managers.Count == 0)
        {
            Debug.LogWarning("[PersistenceSceneManager] SaveAllCoroutine: no ResourceManager instances found to save.");
            yield break;
        }

        if (verbose) Debug.Log($"[PersistenceSceneManager] SaveAllCoroutine: saving {managers.Count} ResourceManager(s) (staggered).");

        foreach (var rm in managers)
        {
            TrySaveManager(rm);

            if (useDelayBetween)
                yield return new WaitForSecondsRealtime(Mathf.Max(0f, delayBetweenSeconds));
            else
                yield return null;
        }

        if (verbose) Debug.Log("[PersistenceSceneManager] SaveAllCoroutine: completed.");
    }

    // --------- robust finder ----------
    /// <summary>
    /// Reliable search across all currently loaded scenes.
    /// - Walks every loaded scene and looks for active ResourceManager components in root objects & their children.
    /// - Returns unique set (no duplicates).
    /// </summary>
    private List<ResourceManager> FindLoadedResourceManagers()
    {
        var results = new List<ResourceManager>();

        try
        {
            int sceneCount = SceneManager.sceneCount;
            if (verbose) Debug.Log($"[PersistenceSceneManager] Searching {sceneCount} loaded scene(s) for ResourceManager...");

            for (int i = 0; i < sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (!s.isLoaded)
                {
                    if (verbose) Debug.Log($"[PersistenceSceneManager] Scene at index {i} not loaded (skip).");
                    continue;
                }

                var roots = s.GetRootGameObjects();
                if (roots == null || roots.Length == 0)
                {
                    if (verbose) Debug.Log($"[PersistenceSceneManager] Scene '{s.name}' has no root objects.");
                    continue;
                }

                foreach (var root in roots)
                {
                    if (root == null) continue;
                    // find ResourceManager in this root (including inactive children)
                    var rm = root.GetComponentInChildren<ResourceManager>(includeInactive: true);
                    if (rm != null)
                    {
                        if (!results.Contains(rm)) results.Add(rm);
                        if (verbose) Debug.Log($"[PersistenceSceneManager] Found ResourceManager '{rm.name}' in scene '{s.name}'.");
                    }
                }
            }

            // final fallback: if still empty, try FindObjectsOfType (covers unusual cases)
            if (results.Count == 0)
            {
#if UNITY_2020_1_OR_NEWER
                var all = FindObjectsOfType<ResourceManager>(includeInactive: true);
#else
                var all = FindObjectsOfType<ResourceManager>();
#endif
                foreach (var a in all)
                {
                    if (a == null) continue;
                    if (!results.Contains(a)) results.Add(a);
                    if (verbose) Debug.Log($"[PersistenceSceneManager] (Fallback) Found ResourceManager '{a.name}' via FindObjectsOfType.");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[PersistenceSceneManager] Exception while searching for ResourceManagers: {ex}");
        }

        return results;
    }

    // --------- saving helper ----------
    private void TrySaveManager(ResourceManager rm)
    {
        if (rm == null) return;

        try
        {
            if (verbose) Debug.Log($"[PersistenceSceneManager] Saving ResourceManager '{rm.name}' (role={rm.role}) ...");
            rm.SaveNow();

            if (rm.hasUnsavedChanges)
                Debug.LogWarning($"[PersistenceSceneManager] ResourceManager '{rm.name}' still has unsaved changes after SaveNow().");
            else
                if (verbose) Debug.Log($"[PersistenceSceneManager] ResourceManager '{rm.name}' saved successfully.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[PersistenceSceneManager] Exception while saving ResourceManager '{rm.name}': {ex}");
        }
    }

    // ---------- Editor / debug convenience ----------
#if UNITY_EDITOR
    [ContextMenu("Debug: Print Found ResourceManagers")]
    private void Editor_PrintFoundManagers()
    {
        var managers = FindLoadedResourceManagers();
        Debug.Log($"[PersistenceSceneManager] Editor_PrintFoundManagers: found {managers.Count}");
        foreach (var m in managers) Debug.Log($" - {m.name} (role={m.role}) in scene '{m.gameObject.scene.name}'");
    }
#endif
}
