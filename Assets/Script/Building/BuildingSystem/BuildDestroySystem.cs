using UnityEngine;

public class BuildDestroySystem : MonoBehaviour
{
    private GameObject highlighted;
    private Material[] originalMats;
    private Material transparentMat;

    public void UpdateDestroy(ItemData tool)
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out var hit, 30))
        {
            var obj = hit.collider.GetComponentInParent<BuildtObject>();
            if (obj != null) Highlight(obj.gameObject);

            if (Input.GetMouseButtonDown(0))
                GameObject.Destroy(obj.gameObject);
        }
        else
        {
            ClearHighlight();
        }
    }

    private void Highlight(GameObject target)
    {
        if (highlighted == target) return;
        ClearHighlight();

        var renderer = target.GetComponentInChildren<MeshRenderer>();
        originalMats = renderer.materials;
        Material[] mats = new Material[originalMats.Length];
        for (int i = 0; i < mats.Length; i++) mats[i] = transparentMat;
        renderer.materials = mats;

        highlighted = target;
    }

    private void ClearHighlight()
    {
        if (highlighted == null) return;
        var renderer = highlighted.GetComponentInChildren<MeshRenderer>();
        renderer.materials = originalMats;
        highlighted = null;
    }
}
