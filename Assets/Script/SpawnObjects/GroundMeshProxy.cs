// Create this simple component to hold mesh data without rendering
using UnityEngine;

public class GroundMeshProxy : MonoBehaviour
{
    public Mesh mesh;
    public bool createCollider = false;

    void Start()
    {
        MeshFilter mf = gameObject.AddComponent<MeshFilter>();
        mf.mesh = mesh;

        if (createCollider)
        {
            MeshCollider mc = gameObject.AddComponent<MeshCollider>();
            mc.sharedMesh = mesh;
        }

        // Make invisible (no Renderer)
        gameObject.hideFlags = HideFlags.HideInHierarchy;
    }
}