using UnityEngine;

/// <summary>
/// Phát hiện surface từ SurfaceType component
/// </summary>
public static class SurfaceDetector
{
    /// <summary>
    /// Phát hiện MaterialType từ Collider
    /// </summary>
    public static MaterialType DetectSurface(Collider collider)
    {
        if (collider == null)
        {
            Debug.LogWarning("[SurfaceDetector] Collider is null, using default Stone");
            return MaterialType.Stone;
        }

        // Tìm SurfaceType component trên chính object
        SurfaceType surfaceType = collider.GetComponent<SurfaceType>();

        // Nếu không có, tìm trên parent
        if (surfaceType == null)
        {
            surfaceType = collider.GetComponentInParent<SurfaceType>();
        }

        if (surfaceType != null)
        {
            Debug.Log($"[SurfaceDetector] Found SurfaceType on {collider.name}: {surfaceType.GetMaterialType()}");
            return surfaceType.GetMaterialType();
        }

        // Fallback: Kiểm tra IMinenable (cho resource system của bạn)
        IMinenable minable = collider.GetComponent<IMinenable>();
        if (minable == null)
        {
            minable = collider.GetComponentInParent<IMinenable>();
        }

        if (minable != null)
        {
            MaterialType matType = ResourceToMaterialType(minable.GetResourceType());
            Debug.Log($"[SurfaceDetector] Found IMinenable: {matType}");
            return matType;
        }

        // Default fallback
        Debug.LogWarning($"[SurfaceDetector] No SurfaceType found on {collider.name}, using default Stone");
        return MaterialType.Stone;
    }

    /// <summary>
    /// Phát hiện custom effect nếu có
    /// </summary>
    public static HitEffectData DetectCustomEffect(Collider collider)
    {
        if (collider == null) return null;

        SurfaceType surfaceType = collider.GetComponent<SurfaceType>();
        if (surfaceType == null)
        {
            surfaceType = collider.GetComponentInParent<SurfaceType>();
        }

      

        return null;
    }

    // Helper: Convert ResourceType sang MaterialType
    private static MaterialType ResourceToMaterialType(ResourceType resourceType)
    {
        switch (resourceType)
        {
            case ResourceType.Tree:
                return MaterialType.Wood;
            case ResourceType.Rock:
                return MaterialType.Stone;
            default:
                return MaterialType.Stone;
        }
    }
}