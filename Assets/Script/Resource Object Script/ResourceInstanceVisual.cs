// ResourceInstanceVisual.cs
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class ResourceInstanceVisual : MonoBehaviour
{
    // ----------------- Config / authoring -----------------
    [Header("Identity / state (set by ResourceManager)")]
    [Tooltip("Unique id assigned by ResourceManager")]
    public string uniqueId;

    [Tooltip("Authoritative state (set by ResourceManager).")]
    public bool isChopped = false;

    [Header("Destroyed replacement")]
    [Tooltip("Optional *instanced* prefab used as the destroyed replacement (stump). Instantiated at runtime under the root.")]
    public GameObject destroyedReplacementPrefab;

    [Tooltip("Optional child in this prefab that acts as the destroyed visual (stump). If provided, this child will be activated/deactivated instead of instantiating a prefab.")]
    public GameObject destroyedChild;

    [Tooltip("If true, colliders on the intact model are disabled when chopped.")]
    public bool disableIntactCollidersWhenChopped = true;

    [Tooltip("If true, colliders on the destroyed replacement (child or prefab) will be enabled when chopped.")]
    public bool enableDestroyedCollidersWhenChopped = false;

    [Tooltip("If true, preserve the replacement prefab's local transform when instantiating. Otherwise we zero local transform so it lines up with root.")]
    public bool preserveReplacementLocalTransform = false;

    // ----------------- Internal state -----------------
    private const string kReplacementChildName = "__destroyed_replacement";

    // cached intact visuals (excludes the replacement child)
    private List<Renderer> _intactRenderers = new List<Renderer>();
    private bool[] _intactRendererInitialStates;
    private List<Collider> _intactColliders = new List<Collider>();
    private bool[] _intactColliderInitialStates;
    private bool _cached = false;

    // instantiated replacement (if any)
    private GameObject _instantiatedReplacement;

    // cached manager reference (set by RegisterWithManagerImmediate)
    private ResourceManager _cachedManager;

    // small flag to avoid repeated Work
    private bool _stateApplied = false;

    // ----------------- Public API (used by ResourceManager / gameplay) -----------------

    /// <summary>Set unique id (ResourceManager uses this).</summary>
    public void SetUniqueId(string id) => uniqueId = id;

    /// <summary>
    /// Called by ResourceManager right after instantiation to register this visual.
    /// Keeps registration explicit and avoids per-instance Find or background coroutines.
    /// </summary>
    public void RegisterWithManagerImmediate(ResourceManager rm)
    {
        if (rm == null) return;
        _cachedManager = rm;
        try { rm.RegisterInstance(this); }
        catch (Exception ex) { Debug.LogWarning($"[ResourceInstanceVisual] RegisterWithManagerImmediate failed: {ex}"); }
    }

    /// <summary>
    /// Called by ResourceManager (or external code) to override the destroyed replacement prefab for this instance at runtime.
    /// Passing null clears any runtime override (existing instantiated replacement will be destroyed).
    /// </summary>
    public void SetDestroyedReplacementPrefab(GameObject prefab)
    {
        destroyedReplacementPrefab = prefab;

        // If currently chopped, apply immediately to reflect change
        if (isChopped)
        {
            // destroy any previous instantiated replacement so new one is created on ApplyState
            if (_instantiatedReplacement != null)
            {
                if (Application.isPlaying) Destroy(_instantiatedReplacement);
                else DestroyImmediate(_instantiatedReplacement);
                _instantiatedReplacement = null;
            }
            ApplyState();
        }
    }

    /// <summary>
    /// Request to chop this instance (gameplay entrypoint). Prefer ResourceManager flow for authority.
    /// </summary>
    public void RequestChop(bool optimistic = false)
    {
        // Try to ask ResourceManager to change authoritative state
        ResourceManager rm = _cachedManager != null ? _cachedManager : UnityEngine.Object.FindObjectOfType<ResourceManager>();
        if (rm != null)
        {
            rm.RequestResourceStateChange(uniqueId, true);
            // optimistic local visual update is intentionally *not* performed here by default.
            // If you want optimistic local feedback, set isChopped = true and ApplyState() here.
            if (optimistic)
            {
                isChopped = true;
                ApplyState();
            }
            return;
        }

        // Fallback: no manager found (singleplayer or early startup) - apply locally.
        isChopped = true;
        ApplyState();
    }

    /// <summary>
    /// Force-set the chopped state locally and apply visuals. Use only when authoritative change already applied elsewhere.
    /// </summary>
    public void SetChoppedLocal(bool chopped)
    {
        if (isChopped == chopped) return;
        isChopped = chopped;
        ApplyState();
    }

    /// <summary>
    /// Apply visuals for current isChopped state. Safe to call multiple times.
    /// </summary>
    public void ApplyState()
    {
        ApplyState(false);
    }

    /// <summary>
    /// Apply visuals for current isChopped state. If 'forceTreatAsReplacement' is true, a destroyed visual will be used even
    /// if no replacement prefab/child exist (no-op if none).
    /// </summary>
    public void ApplyState(bool forceTreatAsReplacement)
    {
        try
        {
            CacheIntactVisualsIfNeeded();

            bool useReplacement = forceTreatAsReplacement || destroyedChild != null || destroyedReplacementPrefab != null || _instantiatedReplacement != null;

            if (isChopped)
            {
                // hide intact visuals
                for (int i = 0; i < _intactRenderers.Count; i++)
                {
                    var r = _intactRenderers[i];
                    if (r != null) r.enabled = false;
                }

                if (disableIntactCollidersWhenChopped)
                {
                    for (int i = 0; i < _intactColliders.Count; i++)
                    {
                        var c = _intactColliders[i];
                        if (c != null) c.enabled = false;
                    }
                }

                // show destroyed replacement (prefer destroyedChild, then replacement prefab)
                if (destroyedChild != null)
                {
                    // if destroyedChild is part of prefab, ensure it's parented under this root (common authoring)
                    if (destroyedChild.transform.root == transform.root && destroyedChild.transform.IsChildOf(transform))
                    {
                        destroyedChild.SetActive(true);
                        // enable/disable colliders on destroyedChild per config
                        ToggleCollidersInHierarchy(destroyedChild, enableDestroyedCollidersWhenChopped);
                    }
                    else
                    {
                        Debug.LogWarning($"[ResourceInstanceVisual] destroyedChild assigned but is not child of this instance: {name}");
                    }
                }
                else if (destroyedReplacementPrefab != null)
                {
                    EnsureInstantiatedReplacementExists();
                    if (_instantiatedReplacement != null)
                        ToggleCollidersInHierarchy(_instantiatedReplacement, enableDestroyedCollidersWhenChopped, afterFixedUpdate: true);
                }

                _stateApplied = true;
            }
            else
            {
                // restore intact visuals
                for (int i = 0; i < _intactRenderers.Count; i++)
                {
                    var r = _intactRenderers[i];
                    if (r != null && _intactRendererInitialStates != null && i < _intactRendererInitialStates.Length)
                        r.enabled = _intactRendererInitialStates[i];
                }

                if (_intactColliderInitialStates != null)
                {
                    for (int i = 0; i < _intactColliders.Count; i++)
                    {
                        var c = _intactColliders[i];
                        if (c != null && i < _intactColliderInitialStates.Length)
                            c.enabled = _intactColliderInitialStates[i];
                    }
                }

                // hide/destroy replacement visuals
                if (destroyedChild != null)
                {
                    // keep child in hierarchy but deactivate
                    if (destroyedChild.transform.IsChildOf(transform))
                        destroyedChild.SetActive(false);
                }

                if (_instantiatedReplacement != null)
                {
                    if (Application.isPlaying) Destroy(_instantiatedReplacement);
                    else DestroyImmediate(_instantiatedReplacement);
                    _instantiatedReplacement = null;
                }

                _stateApplied = false;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ResourceInstanceVisual] ApplyState exception for {name}: {ex}");
        }
    }

    // ----------------- Unity lifecycle -----------------

    private void Awake()
    {
        // Cache intact visuals (do not include replacement child if present)
        CacheIntactVisualsIfNeeded();
    }

    private void Start()
    {
        // If instance was created and RM didn't register us, no problem:
        // ResourceManager should call RegisterWithManagerImmediate when spawning.
    }

    private void OnDestroy()
    {
        // unregister if we have manager
        if (_cachedManager != null)
        {
            try { _cachedManager.UnregisterInstance(uniqueId); } catch { /* best-effort */ }
        }

        // cleanup instantiated replacement
        if (_instantiatedReplacement != null)
        {
            if (Application.isPlaying) Destroy(_instantiatedReplacement);
            else DestroyImmediate(_instantiatedReplacement);
            _instantiatedReplacement = null;
        }
    }

    // ----------------- Helpers -----------------

    /// <summary>Cache all renderers/colliders that belong to the "intact" part of this prefab (excludes replacement child / runtime-instantiated replacement).</summary>
    private void CacheIntactVisualsIfNeeded()
    {
        if (_cached) return;

        _intactRenderers.Clear();
        _intactColliders.Clear();

        var allRenderers = GetComponentsInChildren<Renderer>(true);
        foreach (var r in allRenderers)
        {
            if (IsUnderReplacementHierarchy(r.transform)) continue;
            _intactRenderers.Add(r);
        }

        var allColliders = GetComponentsInChildren<Collider>(true);
        foreach (var c in allColliders)
        {
            if (IsUnderReplacementHierarchy(c.transform)) continue;
            _intactColliders.Add(c);
        }

        _intactRendererInitialStates = new bool[_intactRenderers.Count];
        for (int i = 0; i < _intactRenderers.Count; i++)
            _intactRendererInitialStates[i] = _intactRenderers[i] != null ? _intactRenderers[i].enabled : false;

        _intactColliderInitialStates = new bool[_intactColliders.Count];
        for (int i = 0; i < _intactColliders.Count; i++)
            _intactColliderInitialStates[i] = _intactColliders[i] != null ? _intactColliders[i].enabled : false;

        // If there's an editor-authored destroyedChild, ensure it's deactivated at start unless the authoritative state says chopped.
        if (destroyedChild != null && destroyedChild.transform.IsChildOf(transform))
        {
            if (!isChopped && destroyedChild.activeSelf)
                destroyedChild.SetActive(false);
        }

        // If a replacement child exists from a previous edit-time (named kReplacementChildName), mark it as replacement and exclude it.
        var existing = transform.Find(kReplacementChildName);
        if (existing != null)
        {
            // If destroyedChild wasn't assigned explicitly, treat existing slot as the runtime replacement container.
            if (destroyedChild == null)
            {
                destroyedChild = existing.gameObject;
            }
        }

        _cached = true;
    }

    /// <summary>
    /// Returns true if the transform (or its parent chain) is inside a replacement subtree:
    /// - either the explicit destroyedChild, or
    /// - a runtime-instantiated replacement child named kReplacementChildName.
    /// </summary>
    private bool IsUnderReplacementHierarchy(Transform t)
    {
        if (t == null) return false;

        // if destroyedChild explicitly assigned and the transform is a child of it, exclude
        if (destroyedChild != null && destroyedChild.transform != null && t.IsChildOf(destroyedChild.transform))
            return true;

        // check for runtime-instantiated replacement (by name)
        var cur = t;
        while (cur != null && cur != transform)
        {
            if (string.Equals(cur.name, kReplacementChildName, StringComparison.Ordinal))
                return true;
            cur = cur.parent;
        }
        return false;
    }

    private void EnsureInstantiatedReplacementExists()
    {
        if (_instantiatedReplacement != null) return;
        if (destroyedReplacementPrefab == null) return;

        // Instantiate under this transform
        _instantiatedReplacement = Instantiate(destroyedReplacementPrefab, transform);
        _instantiatedReplacement.name = kReplacementChildName;

        if (!preserveReplacementLocalTransform)
        {
            _instantiatedReplacement.transform.localPosition = Vector3.zero;
            _instantiatedReplacement.transform.localRotation = Quaternion.identity;
            _instantiatedReplacement.transform.localScale = Vector3.one;
        }

        // Disable replacement colliders initially — we'll enable them later if configured
        ToggleCollidersInHierarchy(_instantiatedReplacement, enable: false);

#if UNITY_EDITOR
        // Sanity: warn if replacement contains NetworkIdentity-like components which could be problematic as a child visual
        var allComp = _instantiatedReplacement.GetComponentsInChildren<Component>(true);
        foreach (var c in allComp)
        {
            if (c == null) continue;
            var tname = c.GetType().Name;
            if (tname == "NetworkIdentity" || tname == "MirrorNetworkIdentity")
            {
                Debug.LogWarning($"[ResourceInstanceVisual] Instantiated destroyed replacement contains '{tname}'. Prefer visual-only stumps (no network identities) for child replacements.");
                break;
            }
        }
#endif
    }

    /// <summary>
    /// Enable/disable all colliders in a subtree. If afterFixedUpdate=true, runs a single-frame coroutine to SyncTransforms before enabling colliders
    /// (helps avoid physics glitches when enabling colliders immediately after instantiate).
    /// </summary>
    private void ToggleCollidersInHierarchy(GameObject root, bool enable, bool afterFixedUpdate = false)
    {
        if (root == null) return;
        if (!afterFixedUpdate || !Application.isPlaying)
        {
            foreach (var c in root.GetComponentsInChildren<Collider>(true))
                if (c != null) c.enabled = enable;
            return;
        }

        // run a one-shot coroutine to enable colliders after next FixedUpdate
        StartCoroutine(EnableCollidersAfterFixedUpdate(root, enable));
    }

    private IEnumerator EnableCollidersAfterFixedUpdate(GameObject root, bool enable)
    {
        yield return new WaitForFixedUpdate();
        if (root == null) yield break;

        Physics.SyncTransforms();

        foreach (var c in root.GetComponentsInChildren<Collider>(true))
            if (c != null) c.enabled = enable;
    }
}
