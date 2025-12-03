using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

// Minimal client-side adapter to receive pages and apply them to local visuals.
// Use this unless you have another place to manage local uniqueId -> GameObject mapping.
public class ClientResourceVisuals : MonoBehaviour
{
    public static ClientResourceVisuals Instance { get; private set; }

    // map uniqueId -> ResourceInstanceVisual (populated at local scene load)
    Dictionary<string, ResourceInstanceVisual> visuals = new Dictionary<string, ResourceInstanceVisual>();

    // readiness flags
    bool isLocalMapReady = false;
    bool hasRequestedSnapshot = false;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        // Optionally auto-build mapping now (if objects are already in scene)
        BuildLocalMapFromScene();
        // Start waiting for network + relay + local map
        StartCoroutine(RequestSnapshotWhenReadyCoroutine());
    }

    // Call this if your map is built elsewhere after async operations
    public void NotifyLocalMapReady()
    {
        isLocalMapReady = true;
    }

    // Build mapping by scanning scene for ResourceInstanceVisual components.
    // This is a simple strategy; adapt if you instantiate visuals later.
    public void BuildLocalMapFromScene()
    {
        visuals.Clear();
        var list = FindObjectsOfType<ResourceInstanceVisual>(true);
        foreach (var v in list)
        {
            if (!string.IsNullOrEmpty(v.uniqueId))
            {
                visuals[v.uniqueId] = v;
            }
        }
        // if you scanned and found some, mark ready
        if (list.Length > 0) isLocalMapReady = true;
    }

    // Called by NetworkSnapshotRelay.Target_SendSnapshotPage on client
    public void ApplyResourcePageJson(string pageJson)
    {
        var page = JsonUtility.FromJson<ResourcePage>(pageJson);
        foreach (var rs in page.resources)
        {
            ApplyResourceStateToLocalVisual(rs);
        }
        if (page.isLast) OnInitialSnapshotComplete();
    }

    public void OnInitialSnapshotComplete()
    {
        Debug.Log("[ClientResourceVisuals] Initial snapshot complete for scene.");
        // enable player movement or raise event if you want
    }

    // Apply a single ResourceState to local visual (or create placeholder)
    void ApplyResourceStateToLocalVisual(ResourceState state)
    {
        if (state == null || string.IsNullOrEmpty(state.uniqueId)) return;

        if (visuals.TryGetValue(state.uniqueId, out var vis) && vis != null)
        {
            // Update visual state
            vis.SetChoppedLocal(state.isChopped);
            // Optionally update transform if needed:
            vis.transform.position = state.position;
            vis.transform.rotation = state.rotation;
            vis.transform.localScale = state.scale;

            // NEW: Initialize health for client-side display
            var baseResource = vis.GetComponent<BaseResource>();
            if (baseResource != null && baseResource.GetHealthSystem() != null)
            {
                // Set initial health from snapshot (for health bar display)
                // Note: This doesn't sync real-time, just sets initial state
                baseResource.GetHealthSystem().SetHealth(state.curHealth);
            }
            return;
        }

        // Optionally instantiate a placeholder (you probably don't want this unless deterministic)
        // Debug.LogWarning($"[ClientResourceVisuals] Missing visual for {state.uniqueId}");
    }

    IEnumerator RequestSnapshotWhenReadyCoroutine()
    {
        // Wait for Mirror client to be fully ready
        while (!NetworkClient.active || !NetworkClient.isConnected)
        {
            yield return new WaitForSeconds(0.5f);
        }

        // Additional stability wait
        yield return new WaitForSeconds(1f);

        // Wait for relay to exist with timeout
        NetworkSnapshotRelay relay = null;
        float timeout = 10f;
        float startTime = Time.time;

        while (relay == null && (Time.time - startTime) < timeout)
        {
            relay = FindObjectOfType<NetworkSnapshotRelay>();
            if (relay == null)
            {
                yield return new WaitForSeconds(0.5f);
            }
        }

        if (relay == null)
        {
            Debug.LogError("[ClientResourceVisuals] NetworkSnapshotRelay not found after timeout");
            yield break;
        }

        // Wait until local map ready (with timeout)
        startTime = Time.time;
        while (!isLocalMapReady && (Time.time - startTime) < timeout)
        {
            yield return new WaitForSeconds(0.5f);
        }

        if (!isLocalMapReady)
        {
            Debug.LogWarning("[ClientResourceVisuals] Local map not ready after timeout, proceeding anyway");
        }

        if (hasRequestedSnapshot) yield break;
        hasRequestedSnapshot = true;

        string sceneName = gameObject.scene.name;

        // Verify connection is still alive before sending
        if (NetworkClient.isConnected)
        {
            relay.Cmd_RequestSnapshotFromClient(sceneName);
            Debug.Log($"[ClientResourceVisuals] Requested snapshot for scene '{sceneName}'");
        }
        else
        {
            Debug.LogError("[ClientResourceVisuals] Connection lost before snapshot request");
        }
    }
}
