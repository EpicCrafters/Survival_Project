using UnityEngine;

/// <summary>
/// Gắn component này vào bất kỳ object nào để xác định loại bề mặt
/// </summary>
public class SurfaceType : MonoBehaviour
{
    [Header("Surface Settings")]
    [Tooltip("Loại bề mặt của object này")]
    public MaterialType surfaceMaterial = MaterialType.Stone;

 

    /// <summary>
    /// Lấy loại material
    /// </summary>
    public MaterialType GetMaterialType()
    {
        return surfaceMaterial;
    }

    /// <summary>
    /// Lấy custom effect nếu có
    /// </summary>
   
}