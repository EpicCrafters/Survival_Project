// BuildPreviewSystem.cs (updated: frees created materials & uses Renderer)
using System.Collections.Generic;
using UnityEngine;

public class BuildPreviewSystem : MonoBehaviour
{
    private Transform previewObj;
    private ItemData previewType;
    [SerializeField] private Material transparentMat;
    [SerializeField] private int previewLayer = 2; // configurable
    private BuildManager buildManager;

    // track mats we create so we can Destroy them
    private List<Material> createdPreviewMats = new List<Material>();

    public void Initialize(BuildManager manager)
    {
        buildManager = manager;
    }

    public void StartPreview(ItemData obj)
    {
        ClearPreview();
        if (obj == null || obj.worldPrefab == null) return;

        previewType = obj;
        previewObj = Instantiate(obj.worldPrefab).transform;
        // after previewObj = Instantiate(obj.worldPrefab).transform;
        foreach (var af in previewObj.GetComponentsInChildren<AutoFoundation>(true))
        {
            // disable so it won't spawn real pillars on the preview clone
            af.enabled = false;
            // optionally destroy any preexisting pillars under prefab (if prefab already baked with pillars)
            for (int i = af.transform.childCount - 1; i >= 0; i--)
            {
                var child = af.transform.GetChild(i);
                // if child is a pillar (name or tag), destroy on preview
                if (child.name.ToLower().Contains("target") || child.CompareTag("Target"))
                    DestroyImmediate(child.gameObject);
            }
        }
        previewObj.gameObject.hideFlags = HideFlags.HideInHierarchy; // Ẩn khỏi Hierarchy

        // Disable colliders
        foreach (var col in previewObj.GetComponentsInChildren<Collider>())
            col.enabled = false;

        // Set layer (configurable)
        SetLayerRecursively(previewObj.gameObject, previewLayer);

        // Scale preview
        float previewScale = 1.009f;
        previewObj.localScale = new Vector3(
            buildManager.cellWidth * previewScale,
            buildManager.cellHeight * previewScale,
            buildManager.cellWidth * previewScale
        );

        // Gán transparent material (per-renderer instances)
        createdPreviewMats.Clear();
        foreach (var renderer in previewObj.GetComponentsInChildren<Renderer>())
        {
            Material[] newMats = new Material[renderer.sharedMaterials.Length];
            for (int i = 0; i < newMats.Length; i++)
            {
                Material m = new Material(transparentMat);
                createdPreviewMats.Add(m);
                newMats[i] = m;
            }
            renderer.materials = newMats;
        }
    }

    public void UpdatePreview(Vector3 pos, float rotY, bool canPlace)
    {
        if (previewObj == null) return;
        previewObj.position = pos;
        previewObj.rotation = Quaternion.Euler(0, rotY, 0);

        Color col = canPlace ? new Color(0, 1, 0, 0.5f) : new Color(1, 0, 0, 0.5f);
        foreach (var renderer in previewObj.GetComponentsInChildren<Renderer>())
        {
            foreach (var mat in renderer.materials)
            {
                if (mat != null) mat.color = col;
            }
        }
    }

    public void ClearPreview()
    {
        // Destroy created materials first
        foreach (var m in createdPreviewMats)
        {
            if (m != null) Destroy(m);
        }
        createdPreviewMats.Clear();

        if (previewObj != null)
        {
            if (Application.isPlaying)
                Destroy(previewObj.gameObject);
            else
                DestroyImmediate(previewObj.gameObject);
        }
        previewObj = null;
        previewType = null;
    }

    private void SetLayerRecursively(GameObject go, int layer)
    {
        if (go == null) return;
        go.layer = layer;
        foreach (Transform t in go.transform)
            SetLayerRecursively(t.gameObject, layer);
    }
}
