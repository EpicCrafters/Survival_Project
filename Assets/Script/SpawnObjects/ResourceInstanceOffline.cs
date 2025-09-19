using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Replacement-aware ResourceInstanceOffline (single-file, coroutine-based safe collider timing)
/// - Caches original child renderers/colliders and their enabled states (first use)
/// - ApplyState(treatAsReplacement=false) will:
///     * if isChopped == true:
///         - if treatAsReplacement OR treatAsReplacementPrefab OR auto-detect Stump via MyTree -> ensure renderers enabled,
///           but keep this instance's colliders disabled until next FixedUpdate (prevents overlap impulse)
///         - else -> disable the original renderers immediately, schedule disabling original colliders next FixedUpdate,
///                 instantiate destroyedReplacement child with colliders disabled and enable that child's colliders next FixedUpdate.
///     * if isChopped == false -> restore cached states and cancel any scheduled toggles
/// - Uses coroutines (no extra helper classes)
/// </summary>
[DisallowMultipleComponent]
public class ResourceInstanceOffline : MonoBehaviour
{
    [Header("Runtime state (offline)")]
    public string uniqueId;
    public bool isChopped = false;

    [Tooltip("Optional replacement prefab to show when chopped (e.g. stump)")]
    public GameObject destroyedReplacement;

    [Tooltip("If true, treat this prefab as a replacement (stump) by default). Useful to mark stump prefabs in the editor.")]
    public bool treatAsReplacementPrefab = false;

    private const string kReplacementChildName = "__destroyed_replacement";

    // cached original components (exclude replacement child)
    private List<Renderer> _cachedRenderers = new List<Renderer>();
    private bool[] _rendererInitialStates;
    private List<Collider> _cachedColliders = new List<Collider>();
    private bool[] _colliderInitialStates;
    private bool _cached = false;

    // scheduled coroutine handles
    private Coroutine _disableOriginalCollidersCoroutine;
    // (we don't track each replacement enable coroutine here because they are started on the replacement GameObject or via this instance)

    #region Public API

    // Backwards-compatible ApplyState
    public void ApplyState()
    {
        ApplyState(treatAsReplacement: false);
    }

    /// <summary>
    /// Apply current state. treatAsReplacement true means "this instance is a replacement (stump) and should stay visible".
    /// For replacements we still delay enabling colliders for one FixedUpdate to avoid overlap impulse when spawned while original still exists.
    /// </summary>
    public void ApplyState(bool treatAsReplacement)
    {
        try
        {
            CacheOriginalChildrenIfNeeded();

            // honor serialized flag
            if (!treatAsReplacement && treatAsReplacementPrefab)
                treatAsReplacement = true;

            // auto-detect stump via MyTree if present
            if (!treatAsReplacement)
            {
                var mt = GetComponent<MyTree>();
                if (mt != null)
                {
                    try
                    {
                        if (mt.GetTreeType() == MyTree.TreeType.Stump)
                            treatAsReplacement = true;
                    }
                    catch { /* ignore cross-assembly signature issues */ }
                }
            }

            if (isChopped)
            {
                if (treatAsReplacement)
                {
                    // Replacement (stump) or object flagged as replacement:
                    // ensure visuals are enabled immediately, but delay colliders enabling one physics step
                    CancelScheduledColliderDisableIfAny();
                    EnsureOriginalRenderersEnabled(); // keep visuals visible
                    // disable colliders now (so they won't overlap with original), then enable after FixedUpdate
                    DisableAllCachedCollidersImmediate();
                    // enable colliders on this object after next FixedUpdate (safe even when loaded; small delay harmless)
                    StartCoroutine(EnableCollidersAfterFixedUpdate(gameObject));
                    // remove any nested replacement child (avoid double replacement)
                    RemoveReplacementChildIfExists();
                    Debug.Log($"[ResourceInstanceOffline] ApplyState: {name} treated as REPLACEMENT -> visuals on, colliders will enable next FixedUpdate.");
                }
                else
                {
                    // Normal chopped: hide visuals immediately, schedule disable of original colliders, spawn child replacement (if any)
                    DisableOriginalRenderersImmediate();
                    ScheduleDisableOriginalCollidersNextFixedUpdate();
                    EnsureReplacementChildExists(); // creates child with disabled colliders and schedules those to enable next FixedUpdate
                    Debug.Log($"[ResourceInstanceOffline] ApplyState: {name} treated as CHOPPED -> visuals off, replacement (if any) spawned.");
                }
            }
            else
            {
                // Unchopped -> restore original cached states, cancel pending coroutine(s), remove any replacement child
                CancelScheduledColliderDisableIfAny();
                RestoreOriginalVisuals();
                RemoveReplacementChildIfExists();
                Debug.Log($"[ResourceInstanceOffline] ApplyState: {name} restored to UNCHOPPED (original visuals/colliders).");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ResourceInstanceOffline] ApplyState exception on {name}: {ex}");
        }
    }

    /// <summary>
    /// Mark chopped and notify manager (keeps old MarkChopped behavior).
    /// </summary>
    public void MarkChopped()
    {
        if (isChopped) return;
        isChopped = true;
        ApplyState();

        if (ResourceManagerOffline.Instance != null)
        {
            try
            {
                ResourceManagerOffline.Instance.OnResourceStateChanged(uniqueId, isChopped);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ResourceInstanceOffline] Error notifying ResourceManagerOffline: {ex}");
            }
        }
    }

    #endregion

    #region caching + renderer/collider helpers

    private void CacheOriginalChildrenIfNeeded()
    {
        if (_cached) return;

        _cachedRenderers.Clear();
        _cachedColliders.Clear();

        var allRenderers = GetComponentsInChildren<Renderer>(true);
        foreach (var r in allRenderers)
        {
            if (IsUnderReplacementChild(r.transform)) continue;
            _cachedRenderers.Add(r);
        }

        var allColliders = GetComponentsInChildren<Collider>(true);
        foreach (var c in allColliders)
        {
            if (IsUnderReplacementChild(c.transform)) continue;
            _cachedColliders.Add(c);
        }

        _rendererInitialStates = new bool[_cachedRenderers.Count];
        for (int i = 0; i < _cachedRenderers.Count; i++)
            _rendererInitialStates[i] = _cachedRenderers[i] != null ? _cachedRenderers[i].enabled : false;

        _colliderInitialStates = new bool[_cachedColliders.Count];
        for (int i = 0; i < _cachedColliders.Count; i++)
            _colliderInitialStates[i] = _cachedColliders[i] != null ? _cachedColliders[i].enabled : false;

        _cached = true;
    }

    private bool IsUnderReplacementChild(Transform t)
    {
        if (t == null) return false;
        var cur = t;
        while (cur != null && cur != transform)
        {
            if (string.Equals(cur.name, kReplacementChildName, StringComparison.Ordinal))
                return true;
            cur = cur.parent;
        }
        return false;
    }

    private void DisableOriginalRenderersImmediate()
    {
        for (int i = 0; i < _cachedRenderers.Count; i++)
        {
            var r = _cachedRenderers[i];
            if (r != null) r.enabled = false;
        }
    }

    private void EnsureOriginalRenderersEnabled()
    {
        for (int i = 0; i < _cachedRenderers.Count; i++)
        {
            var r = _cachedRenderers[i];
            if (r != null) r.enabled = true;
        }
    }

    private void RestoreOriginalVisuals()
    {
        for (int i = 0; i < _cachedRenderers.Count; i++)
        {
            var r = _cachedRenderers[i];
            if (r != null) r.enabled = _rendererInitialStates[i];
        }
        for (int i = 0; i < _cachedColliders.Count; i++)
        {
            var c = _cachedColliders[i];
            if (c != null) c.enabled = _colliderInitialStates[i];
        }
    }

    private void DisableAllCachedCollidersImmediate()
    {
        for (int i = 0; i < _cachedColliders.Count; i++)
        {
            var c = _cachedColliders[i];
            if (c != null) c.enabled = false;
        }
    }

    #endregion

    #region scheduled collider toggles (coroutines)

    // schedule disabling original colliders next FixedUpdate (used when original becomes chopped)
    private void ScheduleDisableOriginalCollidersNextFixedUpdate()
    {
        if (!Application.isPlaying)
        {
            // immediate in editor for preview
            for (int i = 0; i < _cachedColliders.Count; i++)
            {
                var c = _cachedColliders[i];
                if (c != null) c.enabled = false;
            }
            return;
        }

        if (_disableOriginalCollidersCoroutine != null) return; // already scheduled
        _disableOriginalCollidersCoroutine = StartCoroutine(DisableOriginalCollidersAfterFixedUpdate());
    }

    private IEnumerator DisableOriginalCollidersAfterFixedUpdate()
    {
        yield return new WaitForFixedUpdate();

        for (int i = 0; i < _cachedColliders.Count; i++)
        {
            var c = _cachedColliders[i];
            if (c != null) c.enabled = false;
        }

        _disableOriginalCollidersCoroutine = null;
    }

    private void CancelScheduledColliderDisableIfAny()
    {
        if (_disableOriginalCollidersCoroutine != null)
        {
            StopCoroutine(_disableOriginalCollidersCoroutine);
            _disableOriginalCollidersCoroutine = null;
        }
    }

    /// <summary>
    /// Enable colliders on `root` after the next FixedUpdate (safe enabling to avoid overlap impulse).
    /// Public so replacement child instances (their ResourceInstanceOffline) can call it on themselves.
    /// </summary>
    public IEnumerator EnableCollidersAfterFixedUpdate(GameObject root)
    {
        // immediate in editor
        if (!Application.isPlaying)
        {
            if (root != null)
            {
                foreach (var c in root.GetComponentsInChildren<Collider>(true))
                    if (c != null) c.enabled = true;
            }
            yield break;
        }

        yield return new WaitForFixedUpdate();

        if (root == null) yield break; // destroyed in meantime

        // Ensure physics sees up-to-date transforms
        Physics.SyncTransforms();

        var cols = root.GetComponentsInChildren<Collider>(true);
        foreach (var c in cols)
        {
            if (c != null) c.enabled = true;
        }
    }

    #endregion

    #region Replacement child management

    private void EnsureReplacementChildExists()
    {
        if (destroyedReplacement == null) return;
        var existing = transform.Find(kReplacementChildName);
        if (existing != null) return;

        // instantiate replacement as child
        var go = Instantiate(destroyedReplacement, transform);
        go.name = kReplacementChildName;

        // adopt parent's world scale by using localScale = Vector3.one
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        // disable replacement colliders initially
        foreach (var c in go.GetComponentsInChildren<Collider>(true)) c.enabled = false;

        // If the replacement prefab contains ResourceInstanceOffline, configure it and schedule its colliders
        var childInst = go.GetComponent<ResourceInstanceOffline>() ?? go.GetComponentInChildren<ResourceInstanceOffline>();
        if (childInst != null)
        {
            childInst.uniqueId = uniqueId;
            childInst.isChopped = true;
            childInst.treatAsReplacementPrefab = true;
            // ensure visuals visible on replacement
            childInst.CacheOriginalChildrenIfNeeded();
            childInst.EnsureOriginalRenderersEnabled();

            // schedule enabling colliders on the replacement instance after FixedUpdate using its coroutine
            childInst.StartCoroutine(childInst.EnableCollidersAfterFixedUpdate(childInst.gameObject));
        }
        else
        {
            // no ResourceInstanceOffline in replacement: schedule enabling colliders via this instance
            StartCoroutine(EnableCollidersAfterFixedUpdate(go));
        }
    }

    private void RemoveReplacementChildIfExists()
    {
        var existing = transform.Find(kReplacementChildName);
        if (existing == null) return;

        if (Application.isPlaying)
            Destroy(existing.gameObject);
        else
            DestroyImmediate(existing.gameObject);
    }

    #endregion

    #region Editor/debug helpers

    [ContextMenu("Debug: Toggle Chopped")]
    private void Debug_ToggleChopped()
    {
        isChopped = !isChopped;
        ApplyState();
    }

    [ContextMenu("Debug: ApplyState (treatAsReplacement=true)")]
    private void Debug_ApplyReplacement()
    {
        ApplyState(true);
    }

    [ContextMenu("Debug: Print ResourceInstanceOffline Info")]
    private void Debug_PrintInfo()
    {
        CacheOriginalChildrenIfNeeded();
        Debug.Log($"[ResourceInstanceOffline] name='{name}' uniqueId='{uniqueId}' isChopped={isChopped} destroyedReplacement='{(destroyedReplacement ? destroyedReplacement.name : "<null>")}' cachedRenderers={_cachedRenderers.Count} cachedColliders={_cachedColliders.Count}");
    }

    #endregion
}