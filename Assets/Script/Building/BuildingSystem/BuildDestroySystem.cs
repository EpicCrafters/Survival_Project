using System.Collections.Generic;
using UnityEngine;

public class BuildDestroySystem : MonoBehaviour
{
    private GameObject highlighted;
    // Lưu original materials cho từng renderer (để restore)
    private Dictionary<Renderer, Material[]> originalMats = new Dictionary<Renderer, Material[]>();
    // Lưu các material instance mình tạo ra để Destroy() khi clear
    private List<Material> createdMats = new List<Material>();

    [Header("Highlight settings")]
    [SerializeField] private Material transparentMat; // gán trong inspector (phải là material dùng shader hỗ trợ màu/alpha)
    [SerializeField] private LayerMask buildLayer = ~0; // mặc định all, gán layer build nếu muốn

    // Optional init — BuildManager có thể gọi, không bắt buộc
    public void Initialize(BuildManager manager) { }

    public void UpdateDestroy(ItemData tool)
    {
        // Giới hạn raycast vào buildLayer (tránh highlight object khác)
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out var hit, 30f, buildLayer))
        {
            var obj = hit.collider.GetComponentInParent<BuildtObject>();
            if (obj != null)
            {
                Highlight(obj.gameObject);

                if (Input.GetMouseButtonDown(0))
                {
                    if (obj != null)
                    {
                        // Clear highlight then remove record from save
                        ClearHighlight();
                        BuildingSaveManager.Instance?.RemoveRecord(obj.guid);
                        Destroy(obj.gameObject);
                    }
                }
            }
            else
            {
                // Hit nhưng không phải BuildtObject => clear
                ClearHighlight();
            }
        }
        else
        {
            // Không hit => clear
            ClearHighlight();
        }
    }

    // Hiển thị highlight cho toàn bộ renderer trong object
    public void Highlight(GameObject target)
    {
        if (target == null) return;
        if (highlighted == target) return; // đã là chính nó

        ClearHighlight(); // clear highlight cũ trước khi set mới

        var renderers = target.GetComponentsInChildren<MeshRenderer>(true);
        foreach (var r in renderers)
        {
            if (r == null) continue;
            // lưu materials gốc (sharedMaterials để không sinh instance khi đọc)
            originalMats[r] = r.sharedMaterials;

            // chuẩn bị array mới (mỗi slot tạo một material instance dựa trên transparentMat)
            var newMats = new Material[originalMats[r].Length];
            for (int i = 0; i < newMats.Length; i++)
            {
                Material m = new Material(transparentMat);
                createdMats.Add(m);
                newMats[i] = m;
            }

            // gán materials (renderer.materials sẽ dùng instance chúng ta tạo)
            r.materials = newMats;
        }

        highlighted = target;
    }

    // Phục hồi trạng thái và hủy các material instance đã tạo
    public void ClearHighlight()
    {
        if (highlighted == null && originalMats.Count == 0 && createdMats.Count == 0) return;

        // Restore original materials (không dùng renderer.materials nếu renderer null)
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

        // Destroy các material tạm we đã tạo
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
