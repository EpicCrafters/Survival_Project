using System;
using UnityEngine;

/// <summary>
/// MyTree - immediate behavior:
///  - On death: persist isChopped=true via ResourceManagerOffline (if available),
///    spawn log/stump/sticks immediately,
///    ensure the stump carries the same uniqueId and *inherits the original's localScale*,
///    then destroy the original.
///
/// Key fixes:
///  - When spawning a persistent replacement (stump), we preserve the original localScale
///    and call ApplyState(treatAsReplacement:true) so persistence logic doesn't hide it.
///  - Exposes GetTreeType() for other systems to detect stump type.
///  - Debug logging toggles for quick diagnosing.
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
    [SerializeField] private Transform treeStumpPrefab; // persistent replacement
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

    // small guard to avoid double-destroy
    private bool isBeingDestroyed = false;

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
        DebugLog($"OnResourceDestroyed called. isBeingDestroyed={isBeingDestroyed}, UniqueId='{UniqueId}', treeType={treeType}, instanceID={GetInstanceID()}");

        if (isBeingDestroyed)
        {
            Debug.LogWarning($"{gameObject.name}: OnResourceDestroyed called multiple times!");
            return;
        }

        isBeingDestroyed = true;
        DebugLog($"{gameObject.name}: Tree destruction starting - Type: {treeType}");

        // Persist the destroyed state in the manager BEFORE removing/spawning replacements.
        // Note: ResourceInstanceOffline.ApplyState will be replacement-aware, so even if we persist
        // before spawning the stump the replacement won't be immediately hidden.
        if (!string.IsNullOrEmpty(UniqueId) && ResourceManagerOffline.Instance != null)
        {
            try
            {
                ResourceManagerOffline.Instance.OnResourceStateChanged(UniqueId, true);
                DebugLog($"{gameObject.name}: Called ResourceManagerOffline.OnResourceStateChanged('{UniqueId}', true)");
            }
            catch (Exception ex)
            {
                Debug.LogError($"{gameObject.name}: Exception while calling ResourceManagerOffline.OnResourceStateChanged: {ex}");
            }
        }
        else
        {
            if (string.IsNullOrEmpty(UniqueId))
                Debug.LogWarning($"{gameObject.name}: UniqueId missing — persistence may not work.");
            if (ResourceManagerOffline.Instance == null)
                Debug.LogWarning($"{gameObject.name}: ResourceManagerOffline.Instance is null — persistence may not work.");
        }

        // Spawn immediate replacements / drops
        SpawnTreeComponentsImmediate();

        // Remove the original object from scene (use base class helper if available)
        DebugLog($"{gameObject.name}: DestroyResource() about to be called.");
        DestroyResource();
    }

    // Immediate spawn logic — spawns log + stump (or halves / sticks)
    private void SpawnTreeComponentsImmediate()
    {
        DebugLog($"{gameObject.name}: SpawnTreeComponentsImmediate start (treeType={treeType})");

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
        }

        // spawn stump (persistent replacement) if applicable
        if (treeType == TreeType.Tree && treeStumpPrefab != null)
        {
            DebugLog($"Attempting to instantiate stump prefab '{treeStumpPrefab.name}' at pos={transform.position}, parent={(transform.parent != null ? transform.parent.name : "<null>")}");
            var stumpTransform = Instantiate(treeStumpPrefab, transform.position, transform.rotation, transform.parent);

            // Preserve localScale of the original tree so stump visually matches.
            try
            {
                stumpTransform.localScale = transform.localScale;
                DebugLog($"Preserved localScale on stump: {stumpTransform.localScale}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"{gameObject.name}: Failed to copy localScale to stump: {ex.Message}");
            }

            var stumpGo = stumpTransform.gameObject;
            DebugLog($"Stump instantiated -> name='{stumpGo.name}' instanceID={stumpGo.GetInstanceID()}");

            // assign uniqueId so loader recognizes this stump as the same record (chopped)
            try
            {
                var inst = stumpGo.GetComponent<ResourceInstanceOffline>() ?? stumpGo.AddComponent<ResourceInstanceOffline>();
                inst.uniqueId = UniqueId;
                inst.isChopped = true;
                // Treat the newly-created stump as a replacement so ApplyState won't hide it.
                inst.ApplyState(treatAsReplacement: true);
                DebugLog($"Assigned ResourceInstanceOffline(uniqueId='{UniqueId}', isChopped=true) and called ApplyState(treatAsReplacement:true)");
            }
            catch (Exception ex)
            {
                Debug.LogError($"{gameObject.name}: Exception while assigning ResourceInstanceOffline on stump: {ex}");
            }

            var br = stumpGo.GetComponent<BaseResource>();
            if (br != null)
            {
                try
                {
                    br.SetUniqueId(UniqueId);
                    DebugLog($"Called SetUniqueId on stump's BaseResource (instanceID={stumpGo.GetInstanceID()})");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"{gameObject.name}: Exception calling SetUniqueId on stump's BaseResource: {ex}");
                }
            }
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
            int count = UnityEngine.Random.Range(minC, maxC + 1);
            DebugLog($"Spawning {count} sticks (min={minC} max={maxC})");
            for (int i = 0; i < count; i++)
            {
                Vector3 offset = new Vector3(UnityEngine.Random.Range(-dropRadius, dropRadius), dropHeight, UnityEngine.Random.Range(-dropRadius, dropRadius));
                var stick = Instantiate(stickPrefab, transform.position + offset, Quaternion.Euler(0, UnityEngine.Random.Range(0f, 360f), 0));
                DebugLog($"Instantiated stick {i} at {transform.position + offset} -> instanceID={stick.GetInstanceID()}");
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

    // Public accessor to help other systems (ResourceInstanceOffline, ResourceManagerOffline) detect stump type
    public TreeType GetTreeType() => treeType;

    // small helper to centralize debug output and avoid spamming Release builds
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