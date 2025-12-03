// PersistenceSceneManager.cs
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PersistenceSceneManager : MonoBehaviour
{
    [Header("Behavior")]
    public bool staggerSaves = true;
    public bool useDelayBetween = false;
    public float delayBetweenSeconds = 0.05f;

    [Header("Logging")]
    public bool verbose = true;

    [Header("Fallback options")]
    [Tooltip("Nếu true sẽ fallback quét scene để tìm ISaveable khi SaveManager không tồn tại hoặc không có bản ghi.")]
    public bool allowFallbackScan = true;

    /// <summary>
    /// Gọi từ UI button. Orchestrator chính.
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
        var saveables = GatherSaveables();
        if (saveables == null || saveables.Count == 0)
        {
            Debug.LogWarning("[PersistenceSceneManager] SaveAllImmediate: no ISaveable instances found.");
            return;
        }

        if (verbose) Debug.Log($"[PersistenceSceneManager] SaveAllImmediate: saving {saveables.Count} ISaveable(s).");
        foreach (var s in saveables)
        {
            TrySave(s);
        }

        if (verbose) Debug.Log("[PersistenceSceneManager] SaveAllImmediate: completed.");
    }

    private IEnumerator SaveAllCoroutine()
    {
        var saveables = GatherSaveables();
        if (saveables == null || saveables.Count == 0)
        {
            Debug.LogWarning("[PersistenceSceneManager] SaveAllCoroutine: no ISaveable instances found.");
            yield break;
        }

        if (verbose) Debug.Log($"[PersistenceSceneManager] SaveAllCoroutine: saving {saveables.Count} ISaveable(s) (staggered).");

        foreach (var s in saveables)
        {
            TrySave(s);

            if (useDelayBetween)
                yield return new WaitForSecondsRealtime(Mathf.Max(0f, delayBetweenSeconds));
            else
                yield return null; // spread across frames
        }

        if (verbose) Debug.Log("[PersistenceSceneManager] SaveAllCoroutine: completed.");
    }

    // ---------- core helpers ----------

    /// <summary>
    /// Gather list of ISaveable to save.
    /// Priority:
    /// 1) SaveManager.Instance.GetSaveables() (if exists and non-empty)
    /// 2) Fallback: scan all MonoBehaviours (include inactive) and pick those implementing ISaveable
    /// Returns a de-duplicated list.
    /// </summary>
    private List<ISaveable> GatherSaveables()
    {
        var results = new List<ISaveable>();

        // 1) Try registry first
        if (SaveManager.Instance != null)
        {
            try
            {
                var reg = SaveManager.Instance.GetSaveables();
                if (reg != null && reg.Count > 0)
                {
                    foreach (var s in reg)
                        if (s != null && !results.Contains(s))
                            results.Add(s);

                    if (verbose) Debug.Log($"[PersistenceSceneManager] GatherSaveables: found {results.Count} via SaveManager registry.");
                    return results;
                }
                else
                {
                    if (verbose) Debug.Log("[PersistenceSceneManager] GatherSaveables: SaveManager found but registry empty.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PersistenceSceneManager] Error reading SaveManager registry: {ex}");
            }
        }
        else
        {
            if (verbose) Debug.Log("[PersistenceSceneManager] GatherSaveables: SaveManager.Instance is null.");
        }

        // 2) Fallback scan (only if allowed)
        if (!allowFallbackScan)
        {
            if (verbose) Debug.Log("[PersistenceSceneManager] Fallback scan disabled.");
            return results;
        }

        try
        {
            var monos = FindObjectsOfType<MonoBehaviour>(true); // include inactive
            foreach (var m in monos)
            {
                if (m == null) continue;
                if (m is ISaveable s && s != null && !results.Contains(s))
                    results.Add(s);
            }

            if (verbose) Debug.Log($"[PersistenceSceneManager] GatherSaveables: fallback scan found {results.Count} ISaveable(s).");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[PersistenceSceneManager] Exception during fallback scan: {ex}");
        }

        return results;
    }

    private void TrySave(ISaveable s)
    {
        if (s == null) return;

        try
        {
            if (verbose) Debug.Log($"[PersistenceSceneManager] Saving '{s.SaveableName}' (scene={s.SceneName}) ...");
            s.SaveNow();

            if (s.HasUnsavedChanges)
                Debug.LogWarning($"[PersistenceSceneManager] '{s.SaveableName}' vẫn còn unsaved changes sau SaveNow().");
            else if (verbose)
                Debug.Log($"[PersistenceSceneManager] '{s.SaveableName}' saved successfully.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[PersistenceSceneManager] Exception while saving '{s.SaveableName}': {ex}");
        }
    }
}
