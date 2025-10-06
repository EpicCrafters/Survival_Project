using UnityEngine;

public class GhostValidator : MonoBehaviour
{
    private int overlapCount = 0;
    private Renderer[] renderers;
    private bool isOnValidSurface = false;

    public bool IsValid => overlapCount == 0 && isOnValidSurface;

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsSurface(other))
        {
            isOnValidSurface = true;
            UpdateColor();
            return;
        }

        if (!IsIgnored(other))
        {
            overlapCount++;
            UpdateColor();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsSurface(other))
        {
            isOnValidSurface = false;
            UpdateColor();
            return;
        }

        if (!IsIgnored(other))
        {
            overlapCount--;
            UpdateColor();
        }
    }

    private bool IsSurface(Collider other)
    {
        int layer = other.gameObject.layer;
        return layer == LayerMask.NameToLayer("Ground") ||
               layer == LayerMask.NameToLayer("buildLayer");
    }

    private bool IsIgnored(Collider other)
    {
        // Bỏ qua trigger và chính ghost
        return other.isTrigger || other.transform.IsChildOf(transform);
    }

    private void UpdateColor()
    {
        Color color = IsValid ? Color.white : Color.red;

        foreach (var rend in renderers)
        {
            foreach (var mat in rend.materials)
            {
                mat.color = color;
            }
        }
    }
}
