// BuildDestroySystem.cs (Mirror-aware)
using System.Collections.Generic;
using UnityEngine;

public class BuildDestroySystem : MonoBehaviour
{
    private GameObject highlighted;
    private Dictionary<Renderer, Material[]> originalMats = new Dictionary<Renderer, Material[]>();
    private List<Material> createdMats = new List<Material>();

    [Header("Highlight settings")]
    [SerializeField] private Material transparentMat; // gán trong inspector (phải là material dùng shader hỗ trợ màu/alpha)
    [SerializeField] private LayerMask buildLayer = ~0; // mặc định all, gán layer build nếu muốn

    private BuildManager buildManager;

    public void Initialize(BuildManager manager) { buildManager = manager; }

    public void UpdateDestroy(ItemData tool)
    {
        if (Camera.main == null) return;

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out var hit, 30f, buildLayer))
        {
            var obj = hit.collider.GetComponentInParent<BuildtObject>();
            if (obj != null)
            {
                Highlight(obj.gameObject);

                if (Input.GetMouseButtonDown(0))
                {
                    if (buildManager != null)
                    {
                        buildManager.RequestDestroy(obj);
                    }
                    else
                    {
                        // fallback local destroy (if running single player or BuildManager on server)
                        BuildingSaveManager.Instance?.RemoveRecord(obj.guid);
                        Destroy(obj.gameObject);
                    }

                    ClearHighlight();
                }
            }
            else
            {
                ClearHighlight();
            }
        }
        else
        {
            ClearHighlight();
        }
    }

    public void Highlight(GameObject target)
    {
        if (target == null) return;
        if (highlighted == target) return;

        ClearHighlight();

        var renderers = target.GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
        {
            if (r == null) continue;
            originalMats[r] = r.sharedMaterials;

            var newMats = new Material[originalMats[r].Length];
            for (int i = 0; i < newMats.Length; i++)
            {
                Material m = new Material(transparentMat);
                createdMats.Add(m);
                newMats[i] = m;
            }
            r.materials = newMats;
        }

        highlighted = target;
    }

    public void ClearHighlight()
    {
        if (highlighted == null && originalMats.Count == 0 && createdMats.Count == 0) return;

        foreach (var kvp in originalMats)
        {
            var r = kvp.Key;
            var mats = kvp.Value;
            if (r != null)
            {
                r.materials = mats;
            }
        }
        originalMats.Clear();

        foreach (var m in createdMats)
        {
            if (m != null) Destroy(m);
        }
        createdMats.Clear();

        highlighted = null;
    }

    private void OnDisable()
    {
        ClearHighlight();
    }

    private void OnDestroy()
    {
        ClearHighlight();
    }
}
