using UnityEngine;

public class GhostValidator : MonoBehaviour
{
    private int overlapCount = 0;
    private Renderer[] renderers;

    public bool IsValid => overlapCount == 0;

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsIgnored(other))
        {
            overlapCount++;
            UpdateColor();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsIgnored(other))
        {
            overlapCount--;
            UpdateColor();
        }
    }

    private bool IsIgnored(Collider other)
    {
        // Bỏ qua trigger hoặc chính bản thân ghost
        return other.isTrigger || other.transform.IsChildOf(transform)||other.gameObject.layer == LayerMask.NameToLayer("Ground"); ;
    }

    private void UpdateColor()
    {
        Color color = overlapCount == 0 ? Color.white : Color.red;

        foreach (var rend in renderers)
        {
            foreach (var mat in rend.materials)
            {
                mat.color = color;
            }
        }
    }
}
