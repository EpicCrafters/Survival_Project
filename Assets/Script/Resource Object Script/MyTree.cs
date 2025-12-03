using Mirror;
using System;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;

/// <summary>
/// MyTree - immediate behavior:
///  - On death: persist isChopped=true via ResourceManager (if available),
///    spawn log/stump/sticks immediately (visual feedback),
///    ensure the stump carries the same uniqueId and inherits the original's localScale when we instantiate a dedicated stump (fallback),
///    then destroy the original only in the local-only fallback.
/// </summary>
public class MyTree : BaseResource, IMinenable
{
    public enum TreeType { Tree, Log, LogHalf, Stump }
    public enum TreeSize { Small, Medium, Large }

    [Header("Tree Specific")]
    [SerializeField] private TreeType treeType = TreeType.Tree;
    [SerializeField] private TreeSize treeSize = TreeSize.Medium;

    [Header("Prefabs")]
    [SerializeField] private Transform treeLogPrefab;
    [SerializeField] private Transform treeLogHalfPrefab;
    [SerializeField] private Transform treeStumpPrefab; // persistent replacement candidate
    [SerializeField] private Transform stickPrefab;

    [Header("Byproduct Settings")]
    [SerializeField] private float halfLogSpacing = 1.2f;
    [SerializeField] private bool useColliderBoundsForLogLength = true;
    [SerializeField] private float manualLogLength = 5f;

    [Header("Drop Settings (override)")]
    [SerializeField] private int overrideMinDropCount = -1;
    [SerializeField] private int overrideMaxDropCount = -1;

    [Header("Debug")]
    [SerializeField] private bool debugMode = true;
    [SerializeField] private bool verboseDebug = true;

    protected override void InitializeHealth()
    {
        int healthAmount = treeType switch
        {
            TreeType.Tree => 30,
            TreeType.Log => 25,
            TreeType.LogHalf => 15,
            TreeType.Stump => 20,
            _ => 30
        };
        healthSystem = new HealthSystem(healthAmount);
        resourceType = ResourceType.Tree;
    }

    protected override void OnResourceDestroyed()
    {
        ResourceManager rm = ResourceManager.GetManagerForGameObject(this.gameObject);
        // Update health to 0 before doing anything else
        if (!string.IsNullOrEmpty(UniqueId))
        {
            rm.RequestResourceStateChange(UniqueId, true, 0); // isChopped=true, health=0
        }

        // Defensive early return: don't re-run if already destroying or destroyed.
        // IMPORTANT: Do NOT set isBeingDestroyed here unconditionally; let the base method set it when
        // calling RequestDestroyAndReplace (so manager flows happen correctly). Only set it for local-only fallback.
        if (isBeingDestroyed || isDestroyed)
        {
            Debug.LogWarning($"{gameObject.name}: OnResourceDestroyed called multiple times or already destroyed!");
            return;
        }

        //DebugLog($"{gameObject.name}: Tree destruction starting - Type: {treeType}");

        // Ensure we have an id for persistence flows
        if (string.IsNullOrEmpty(UniqueId))
        {
            GenerateUniqueIdIfMissing();
            Debug.LogWarning($"{gameObject.name}: UniqueId missing — generated '{UniqueId}' for persistence.");
        }

        // Spawn drops & non-stump byproducts first so they use the original transform/scale.
        SpawnTreeComponentsImmediate(spawnStump: (rm == null));

        // If ResourceManager present -> request authoritative change and apply immediate visual feedback on this object.
        if (rm != null)
        {
            GameObject replacementPrefab = treeStumpPrefab != null ? treeStumpPrefab.gameObject : null;

            // RequestDestroyAndReplace is implemented on the base; it will set isBeingDestroyed and route to manager.
            try
            {
                RequestDestroyAndReplace(replacementPrefab);
                DebugLog($"{gameObject.name}: Requested destroy/replace via ResourceManager for UniqueId='{UniqueId}' (replacement={(replacementPrefab != null ? replacementPrefab.name : "<null>")})");
            }
            catch (Exception ex)
            {
                Debug.LogError($"{gameObject.name}: RequestDestroyAndReplace threw: {ex}");
            }

            // Immediate visual feedback: set destroyed replacement on the existing visual and apply chopped state.
            try
            {
                var vis = GetComponent<ResourceInstanceVisual>() ?? gameObject.AddComponent<ResourceInstanceVisual>();
                if (replacementPrefab != null)
                {
                    vis.SetDestroyedReplacementPrefab(replacementPrefab);
                    vis.SetChoppedLocal(true);
                    DebugLog($"{gameObject.name}: Applied visual replacement (no object destruction).");
                }
                else
                {
                    vis.SetChoppedLocal(true);
                    DebugLog($"{gameObject.name}: Applied visual chopped state (no replacement prefab available).");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{gameObject.name}: Failed to apply immediate visual via ResourceInstanceVisual: {ex}");
            }

            // Do NOT destroy this GameObject: ResourceManager is authoritative and will handle persistence + any instanced replacement policy.
            return;
        }

        // No ResourceManager present => local-only fallback: instantiate stump (preserve localScale & uniqueId) and destroy original.
        // SpawnTreeComponentsImmediate already created the stump (spawnStump==true). Now mark being destroyed and remove this.
        DebugLog($"{gameObject.name}: No ResourceManager found - performing local-only replacement and destroy.");

        // Mark being destroyed to prevent re-entry (base would have done this if a manager existed).
        isBeingDestroyed = true;

        DestroyResource();
    }

    /// <summary>
    /// Immediate spawn logic — spawns log + stump (only when spawnStump==true) + halves/sticks.
    /// When ResourceManager is present we call this with spawnStump=false (so manager handles persistent visuals).
    /// </summary>
    private void SpawnTreeComponentsImmediate(bool spawnStump)
    {
        DebugLog($"{gameObject.name}: SpawnTreeComponentsImmediate start (treeType={treeType}, spawnStump={spawnStump})");

        // spawn log (non-persistent drop)
        if (treeType == TreeType.Tree && treeLogPrefab != null)
        {
            Vector3 logPos = transform.position + transform.up * 0.2f;
            Quaternion logRot = Quaternion.Euler(
                UnityEngine.Random.Range(-2f, 2f),
                UnityEngine.Random.Range(0f, 360f),
                UnityEngine.Random.Range(-2f, 2f)
            );
            var logObj = Instantiate(treeLogPrefab, logPos, logRot);
            DebugLog($"Instantiated log prefab '{treeLogPrefab.name}' at {logPos} rot={logRot.eulerAngles} -> instanceID={logObj.GetInstanceID()}");
            TryAddPhysicsTo(logObj.gameObject);
        }
        
        // log halves for Log type
        if (treeType == TreeType.Log && treeLogHalfPrefab != null)
        {
            int halfCount = treeSize switch { TreeSize.Small => 2, TreeSize.Medium => 4, TreeSize.Large => 6, _ => 4 };
            float len = GetLogLength();
            Vector3 dir = GetLogLengthDirection().normalized;
            float spacing = Mathf.Max(halfLogSpacing, len * 0.5f);
            DebugLog($"Spawning {halfCount} log halves (length={len} spacing={spacing} dir={dir})");
            for (int i = 0; i < halfCount; i++)
            {
                Vector3 pos = transform.position + dir * spacing * i;
                Quaternion rot = Quaternion.LookRotation(transform.forward, transform.up) * Quaternion.Euler(0, UnityEngine.Random.Range(0f, 360f), 0);
                var half = Instantiate(treeLogHalfPrefab, pos, rot);
                DebugLog($"Instantiated log half {i} at {pos} rot={rot.eulerAngles} -> instanceID={half.GetInstanceID()}");
                AddPhysicsToHalfLog(half.gameObject, dir);

                // preserve localScale of the original log if desired
                try { half.localScale = transform.localScale; } catch { }
            }

        }

        // sticks for LogHalf or Stump types
        if ((treeType == TreeType.LogHalf || treeType == TreeType.Stump) && stickPrefab != null)
        {
            int minC = overrideMinDropCount >= 0 ? overrideMinDropCount : minDropCount;
            int maxC = overrideMaxDropCount >= 0 ? overrideMaxDropCount : maxDropCount;
            if (minC > maxC) maxC = minC; // safety clamp
            int count = UnityEngine.Random.Range(minC, maxC + 1);
            DebugLog($"Spawning {count} sticks (min={minC} max={maxC})");
            for (int i = 0; i < count; i++)
            {
                Vector3 offset = new Vector3(UnityEngine.Random.Range(-dropRadius, dropRadius), dropHeight, UnityEngine.Random.Range(-dropRadius, dropRadius));
                var stick = Instantiate(stickPrefab, transform.position + offset, Quaternion.Euler(0, UnityEngine.Random.Range(0f, 360f), 0));
                DebugLog($"Instantiated stick {i} at {transform.position + offset} -> instanceID={stick.GetInstanceID()}");
                TryAddPhysicsTo(stick.gameObject);

                try
                {
                    NetworkServer.Spawn(stick.gameObject);
                }
                catch (Exception ex) { Debug.LogException(ex); }
            }
        }

        DebugLog($"{gameObject.name}: SpawnTreeComponentsImmediate end.");
    }

    // ---------- Physics & spawn helpers ----------
    private void TryAddPhysicsTo(GameObject go)
    {
        if (go == null) return;
        var rb = go.GetComponent<Rigidbody>() ?? go.AddComponent<Rigidbody>();
        rb.AddForce(Vector3.up * UnityEngine.Random.Range(0.3f, 1f) + UnityEngine.Random.insideUnitSphere * UnityEngine.Random.Range(0.1f, 0.8f), ForceMode.Impulse);
        rb.AddTorque(UnityEngine.Random.insideUnitSphere * 0.5f, ForceMode.Impulse);
    }

    private void AddPhysicsToHalfLog(GameObject halfLog, Vector3 lengthDirection)
    {
        if (halfLog == null) return;
        var rb = halfLog.GetComponent<Rigidbody>() ?? halfLog.AddComponent<Rigidbody>();
        Vector3 tangent = Vector3.Cross(lengthDirection, Vector3.up);
        if (tangent.sqrMagnitude < 0.001f)
            tangent = Vector3.Cross(lengthDirection, Vector3.right);
        Vector3 scatter = tangent.normalized * UnityEngine.Random.Range(-1.2f, 1.2f) + Vector3.up * UnityEngine.Random.Range(0.2f, 1f);
        rb.AddForce(scatter, ForceMode.Impulse);
        rb.AddTorque(UnityEngine.Random.insideUnitSphere * UnityEngine.Random.Range(0.2f, 1f), ForceMode.Impulse);
    }

    private float GetLogLength()
    {
        if (!useColliderBoundsForLogLength) return manualLogLength;
        var c = GetComponent<Collider>();
        if (c == null) return manualLogLength;
        var s = c.bounds.size;
        return Mathf.Max(s.x, Mathf.Max(s.y, s.z));
    }

    private Vector3 GetLogLengthDirection()
    {
        var c = GetComponent<Collider>();
        if (c == null) return Vector3.forward;
        var s = c.bounds.size;
        if (s.x >= s.y && s.x >= s.z) return Vector3.right;
        if (s.y >= s.x && s.y >= s.z) return Vector3.up;
        return Vector3.forward;
    }

    protected override void ValidateComponents()
    {
        if (treeType == TreeType.Tree && treeLogPrefab == null)
            Debug.LogWarning($"{name}: treeLogPrefab not assigned!");
        if (treeType == TreeType.Log && treeLogHalfPrefab == null)
            Debug.LogWarning($"{name}: treeLogHalfPrefab not assigned!");
        if ((treeType == TreeType.LogHalf || treeType == TreeType.Stump) && stickPrefab == null)
            Debug.LogWarning($"{name}: stickPrefab not assigned!");
    }

    public TreeType GetTreeType() => treeType;

    private void DebugLog(string msg)
    {
        if (!debugMode) return;
        if (verboseDebug)
            Debug.Log($"[MyTree][{name}] {msg}");
        else
            Debug.Log($"[MyTree] {msg}");
    }

    public override ResourceType GetResourceType() => ResourceType.Tree;
}
