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

        // --- CRITICAL: Only run visual effects on server ---
        if (!NetworkServer.active)
        {
            DebugLog($"{gameObject.name}: OnResourceDestroyed called on client, skipping visual effects (server will handle)");
            return;
        }

        // Defensive early return
        if (isBeingDestroyed || isDestroyed)
        {
            Debug.LogWarning($"{gameObject.name}: OnResourceDestroyed called multiple times or already destroyed!");
            return;
        }

        DebugLog($"{gameObject.name}: Tree destruction starting - Type: {treeType}");

        // Ensure we have an id for persistence flows
        if (string.IsNullOrEmpty(UniqueId))
        {
            GenerateUniqueIdIfMissing();
            Debug.LogWarning($"{gameObject.name}: UniqueId missing — generated '{UniqueId}' for persistence.");
        }

        // Spawn drops & non-stump byproducts first so they use the original transform/scale.
        SpawnTreeComponentsImmediate(spawnStump: (rm == null));

        // If ResourceManager present -> request authoritative change
        if (rm != null)
        {
            GameObject replacementPrefab = treeStumpPrefab != null ? treeStumpPrefab.gameObject : null;

            try
            {
                RequestDestroyAndReplace(replacementPrefab);
                DebugLog($"{gameObject.name}: Requested destroy/replace via ResourceManager for UniqueId='{UniqueId}'");
            }
            catch (Exception ex)
            {
                Debug.LogError($"{gameObject.name}: RequestDestroyAndReplace threw: {ex}");
            }
        }
        else
        {
            // No ResourceManager present => local-only fallback
            DebugLog($"{gameObject.name}: No ResourceManager found - performing local-only replacement and destroy.");
            isBeingDestroyed = true;
            DestroyResource();
        }
    }

    private void SpawnTreeComponentsImmediate(bool spawnStump)
    {
        DebugLog($"{gameObject.name}: SpawnTreeComponentsImmediate start (treeType={treeType}, spawnStump={spawnStump})");

        // spawn log (non-persistent drop) - SERVER ONLY
        if (treeType == TreeType.Tree && treeLogPrefab != null && NetworkServer.active)
        {
            Vector3 logPos = transform.position + transform.up * 0.2f;
            Quaternion logRot = Quaternion.Euler(
                UnityEngine.Random.Range(-2f, 2f),
                UnityEngine.Random.Range(0f, 360f),
                UnityEngine.Random.Range(-2f, 2f)
            );
            var logObj = Instantiate(treeLogPrefab, logPos, logRot);

            // Network spawn the log
            NetworkServer.Spawn(logObj.gameObject);
        }

        // log halves for Log type - SERVER ONLY
        if (treeType == TreeType.Log && treeLogHalfPrefab != null && NetworkServer.active)
        {
            int halfCount = treeSize switch { TreeSize.Small => 2, TreeSize.Medium => 4, TreeSize.Large => 6, _ => 4 };
            float len = GetLogLength();
            Vector3 dir = GetLogLengthDirection().normalized;
            float spacing = Mathf.Max(halfLogSpacing, len * 0.5f);

            for (int i = 0; i < halfCount; i++)
            {
                Vector3 pos = transform.position + dir * spacing * i;
                Quaternion rot = Quaternion.LookRotation(transform.forward, transform.up) * Quaternion.Euler(0, UnityEngine.Random.Range(0f, 360f), 0);
                var half = Instantiate(treeLogHalfPrefab, pos, rot);

                // Network spawn
                NetworkServer.Spawn(half.gameObject);
            }
        }

        // sticks for LogHalf or Stump types - SERVER ONLY
        if ((treeType == TreeType.LogHalf || treeType == TreeType.Stump) && stickPrefab != null && NetworkServer.active)
        {
            int minC = overrideMinDropCount >= 0 ? overrideMinDropCount : minDropCount;
            int maxC = overrideMaxDropCount >= 0 ? overrideMaxDropCount : maxDropCount;
            if (minC > maxC) maxC = minC;
            int count = UnityEngine.Random.Range(minC, maxC + 1);

            for (int i = 0; i < count; i++)
            {
                Vector3 offset = new Vector3(UnityEngine.Random.Range(-dropRadius, dropRadius), dropHeight, UnityEngine.Random.Range(-dropRadius, dropRadius));
                var stick = Instantiate(stickPrefab, transform.position + offset, Quaternion.Euler(0, UnityEngine.Random.Range(0f, 360f), 0));

                // Network spawn
                NetworkServer.Spawn(stick.gameObject);
            }
        }
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
