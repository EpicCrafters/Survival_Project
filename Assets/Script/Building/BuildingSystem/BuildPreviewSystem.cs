using UnityEngine;

public class BuildPreviewSystem : MonoBehaviour
{
    private Transform previewObj;
    private ItemData previewType;
    [SerializeField] private Material transparentMat;
    [SerializeField] private BuildManager buildManager;
    private void Awake()
    {
        if (buildManager == null) buildManager = GetComponent<BuildManager>();
    }
    public void StartPreview(ItemData obj)
    {
        ClearPreview();
        previewType = obj;
        previewObj = Instantiate(obj.worldPrefab).transform;
        float previewScale = 1.009f; // tăng so với kích thước thật
        previewObj.localScale = new Vector3(
            buildManager.cellWidth * previewScale,
            buildManager.cellHeight * previewScale,
            buildManager.cellWidth * previewScale
        );
        var renderer = previewObj.GetComponentInChildren<MeshRenderer>();
        foreach (var mat in renderer.materials)
        {
            mat.color = new Color(0.5f, 0.5f, 0.5f, 0.3f);
        }

        previewObj.gameObject.layer = 2;
    }

    public void UpdatePreview(Vector3 pos, float rotY, bool canPlace)
    {
        if (previewObj == null) return;
        previewObj.position = pos;
        previewObj.rotation = Quaternion.Euler(0, rotY, 0);

        var renderer = previewObj.GetComponentInChildren<MeshRenderer>();
        Color col = canPlace ? new Color(0, 1, 0, 0.5f) : new Color(1, 0, 0, 0.5f);
        foreach (var mat in renderer.materials)
        {
            mat.color = col;
        }
    }

    public void ClearPreview()
    {
        if (previewObj != null) Destroy(previewObj.gameObject);
        previewObj = null;
        previewType = null;
    }
}
