using UnityEngine;

public class GrassShaderDebugger : MonoBehaviour
{
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.D))
        {
            DebugShaderParameters();
        }
    }

    void DebugShaderParameters()
    {
        Debug.Log("=== Shader Parameters ===");

        // Check if global properties are set
        Texture gridTex = Shader.GetGlobalTexture("_GrassGrid");
        Debug.Log($"Global _GrassGrid texture: {(gridTex != null ? "SET" : "NULL")}");

        Vector4 gridOrigin = Shader.GetGlobalVector("_GridOrigin");
        Debug.Log($"Global _GridOrigin: {gridOrigin}");

        float cellSize = Shader.GetGlobalFloat("_CellSize");
        Debug.Log($"Global _CellSize: {cellSize}");

        float gridSize = Shader.GetGlobalFloat("_GridSize");
        Debug.Log($"Global _GridSize: {gridSize}");

        // Check material properties
        Renderer[] grassRenderers = FindObjectsOfType<Renderer>();
        foreach (Renderer r in grassRenderers)
        {
            if (r.sharedMaterial != null && r.sharedMaterial.shader.name.Contains("Grass"))
            {
                Debug.Log($"Material: {r.sharedMaterial.name}");
                Debug.Log($"Has _GridOrigin: {r.sharedMaterial.HasVector("_GridOrigin")}");
                Debug.Log($"Has _GrassGrid: {r.sharedMaterial.HasTexture("_GrassGrid")}");
            }
        }
    }
}