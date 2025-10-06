using UnityEngine;

public class BuildPreviewSystem : MonoBehaviour
{
    private Transform previewObj;
    private ItemData previewType;
    [SerializeField] private Material transparentMat;
    private BuildManager buildManager;

    public void Initialize(BuildManager manager)
    {
        buildManager = manager;
    }

    public void StartPreview(ItemData obj)
    {
        ClearPreview();
        previewType = obj;
        previewObj = Instantiate(obj.worldPrefab).transform;
        previewObj.gameObject.hideFlags = HideFlags.HideInHierarchy; // Ẩn khỏi Hierarchy

        // Disable colliders
        foreach (var col in previewObj.GetComponentsInChildren<Collider>())
            col.enabled = false;

        // Set layer trước
        SetLayerRecursively(previewObj.gameObject, 2);

        // Scale preview
        float previewScale = 1.009f;
        previewObj.localScale = new Vector3(
            buildManager.cellWidth * previewScale,
            buildManager.cellHeight * previewScale,
            buildManager.cellWidth * previewScale
        );

        // Gán transparent material
        foreach (var renderer in previewObj.GetComponentsInChildren<MeshRenderer>())
        {
            Material[] newMats = new Material[renderer.sharedMaterials.Length];
            for (int i = 0; i < newMats.Length; i++)
                newMats[i] = new Material(transparentMat);
            renderer.materials = newMats;
        }
    }

    public void UpdatePreview(Vector3 pos, float rotY, bool canPlace)
    {
        if (previewObj == null) return;
        previewObj.position = pos;
        previewObj.rotation = Quaternion.Euler(0, rotY, 0);

        Color col = canPlace ? new Color(0, 1, 0, 0.5f) : new Color(1, 0, 0, 0.5f);
        foreach (var renderer in previewObj.GetComponentsInChildren<MeshRenderer>())
        {
            foreach (var mat in renderer.materials)
                mat.color = col;
        }
    }

    public void ClearPreview()
    {
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
        go.layer = layer;
        foreach (Transform t in go.transform)
            SetLayerRecursively(t.gameObject, layer);
    }
}
