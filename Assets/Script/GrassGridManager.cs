// Drag this onto an empty GameObject in your scene
using UnityEditor;
using UnityEngine;

public class GrassGridManager : MonoBehaviour
{
    [Header("Grid Settings")]
    public float cellSize = 1f;          // 1-meter squares
    public int gridSize = 512;           // 512x512 grid
    public Vector2 gridCenter = Vector2.zero; // Center of your island

    [Header("Gizmo Settings")]
    [Tooltip("Enable/disable gizmo drawing")]
    public bool drawGizmos = true;
    [Tooltip("Color of the grid lines")]
    public Color gridColor = new Color(0.5f, 1f, 0.5f, 0.3f);
    [Tooltip("Color for cleared areas")]
    public Color clearedColor = new Color(1f, 0.3f, 0.3f, 0.5f);
    [Tooltip("Draw grid lines every N cells")]
    public int gridLineFrequency = 10;
    [Tooltip("Draw text labels for coordinates")]
    public bool drawCoordinates = false;
    [Tooltip("Preview cleared area at mouse position")]
    public bool previewClearArea = false;
    [Range(1f, 20f)]
    public float previewRadius = 5f;
    [Tooltip("Draw the actual grid texture as a plane")]
    public bool drawTexturePreview = false;

    private Texture2D gridTexture;
    private Vector3 gridWorldOrigin;
    private float gridWorldSize;
    private Vector3 lastClearCenter;
    private float lastClearRadius;

    void Start()
    {
        CreateGrid();
        SetupShader();
        Debug.Log($"Grid texture created: {gridTexture != null}");
        Debug.Log($"Texture size: {gridTexture?.width}x{gridTexture?.height}");
    }

    void CreateGrid()
    {
        // Create black-and-white texture (white = grass allowed)
        gridTexture = new Texture2D(gridSize, gridSize,
            UnityEngine.Experimental.Rendering.GraphicsFormat.R8_UNorm,
            UnityEngine.Experimental.Rendering.TextureCreationFlags.None);

        // Fill with white (grass everywhere)
        Color[] colors = new Color[gridSize * gridSize];
        for (int i = 0; i < colors.Length; i++)
            colors[i] = Color.white;

        gridTexture.SetPixels(colors);
        gridTexture.Apply();
        gridTexture.filterMode = FilterMode.Bilinear;
        gridTexture.wrapMode = TextureWrapMode.Clamp;

        // Calculate grid bounds
        gridWorldSize = gridSize * cellSize;
        gridWorldOrigin = new Vector3(
            gridCenter.x - gridWorldSize * 0.5f,
            0,
            gridCenter.y - gridWorldSize * 0.5f
        );
    }

    void SetupShader()
    {
        // Send to shader globally
        Shader.SetGlobalTexture("_GrassGrid", gridTexture);
        Shader.SetGlobalVector("_GridOrigin",
            new Vector4(gridWorldOrigin.x,
                       0,
                       gridWorldOrigin.z,
                       0));
        // ADD THIS LINE:
        Shader.SetGlobalVector("_GridCenter",
            new Vector4(gridCenter.x,
                       0,
                       gridCenter.y,
                       0));
        Shader.SetGlobalFloat("_CellSize", cellSize);
        Shader.SetGlobalFloat("_GridSize", gridSize);

        // Also update all materials in the scene that use this shader
        var renderers = FindObjectsOfType<Renderer>();
        foreach (var renderer in renderers)
        {
            if (renderer.sharedMaterial != null &&
                renderer.sharedMaterial.shader.name == "Custom/URP_GrassCulling")
            {
                renderer.sharedMaterial.SetTexture("_GrassGrid", gridTexture);
                // ADD THIS LINE:
                renderer.sharedMaterial.SetVector("_GridCenter", new Vector4(gridCenter.x, 0, gridCenter.y, 0));
            }
        }
    }

    // Call this when placing a building
    public void ClearArea(Vector3 worldPos, float radius)
    {
        // Convert world position to texture coordinates
        int centerX = Mathf.FloorToInt((worldPos.x - gridCenter.x + gridWorldSize * 0.5f) / cellSize);
        int centerY = Mathf.FloorToInt((worldPos.z - gridCenter.y + gridWorldSize * 0.5f) / cellSize);
        int pixelRadius = Mathf.CeilToInt(radius / cellSize);

        // Mark area as black (no grass)
        for (int y = -pixelRadius; y <= pixelRadius; y++)
        {
            for (int x = -pixelRadius; x <= pixelRadius; x++)
            {
                if (x * x + y * y <= pixelRadius * pixelRadius)
                {
                    int px = centerX + x;
                    int py = centerY + y;

                    if (px >= 0 && px < gridSize && py >= 0 && py < gridSize)
                    {
                        gridTexture.SetPixel(px, py, Color.black);
                    }
                }
            }
        }

        gridTexture.Apply();

        // Store for gizmo drawing
        lastClearCenter = worldPos;
        lastClearRadius = radius;
    }

    void OnDrawGizmos()
    {
        if (!drawGizmos) return;

        // Cache values for editor-time calculations
        float gridSizeValue = Application.isPlaying ? gridWorldSize : gridSize * cellSize;
        Vector3 gridOrigin = Application.isPlaying ? gridWorldOrigin :
            new Vector3(gridCenter.x - gridSizeValue * 0.5f, 0, gridCenter.y - gridSizeValue * 0.5f);

        // Draw grid boundary
        Gizmos.color = new Color(gridColor.r, gridColor.g, gridColor.b, 0.7f);
        Vector3[] corners = new Vector3[4]
        {
            gridOrigin,
            gridOrigin + new Vector3(gridSizeValue, 0, 0),
            gridOrigin + new Vector3(gridSizeValue, 0, gridSizeValue),
            gridOrigin + new Vector3(0, 0, gridSizeValue)
        };

        for (int i = 0; i < 4; i++)
        {
            Gizmos.DrawLine(corners[i], corners[(i + 1) % 4]);
        }

        // Draw grid lines
        Gizmos.color = gridColor;
        int lineStep = Mathf.Max(1, gridLineFrequency);

        for (int x = 0; x <= gridSize; x += lineStep)
        {
            float xPos = gridOrigin.x + x * cellSize;
            Gizmos.DrawLine(
                new Vector3(xPos, 0, gridOrigin.z),
                new Vector3(xPos, 0, gridOrigin.z + gridSizeValue)
            );
        }

        for (int z = 0; z <= gridSize; z += lineStep)
        {
            float zPos = gridOrigin.z + z * cellSize;
            Gizmos.DrawLine(
                new Vector3(gridOrigin.x, 0, zPos),
                new Vector3(gridOrigin.x + gridSizeValue, 0, zPos)
            );
        }

        /*// Draw preview clear area
        if (previewClearArea)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit))
            {
                Vector3 previewPos = hit.point;
                previewPos.y = 0;

                Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
                Gizmos.DrawSphere(previewPos, previewRadius);
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(previewPos, previewRadius);
            }
        }*/

        // Draw last cleared area
        if (lastClearRadius > 0)
        {
            Gizmos.color = clearedColor;
            Gizmos.DrawSphere(lastClearCenter, lastClearRadius * 0.8f);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(lastClearCenter, lastClearRadius);

            // Draw grid cell overlay
            Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
            float halfCell = cellSize * 0.5f;
            for (float x = lastClearCenter.x - lastClearRadius; x <= lastClearCenter.x + lastClearRadius; x += cellSize)
            {
                for (float z = lastClearCenter.z - lastClearRadius; z <= lastClearCenter.z + lastClearRadius; z += cellSize)
                {
                    Vector3 cellPos = new Vector3(
                        Mathf.Floor(x / cellSize) * cellSize + halfCell,
                        0.1f,
                        Mathf.Floor(z / cellSize) * cellSize + halfCell
                    );
                    if (Vector3.Distance(cellPos, lastClearCenter) <= lastClearRadius)
                    {
                        Gizmos.DrawCube(cellPos, new Vector3(cellSize, 0.1f, cellSize));
                    }
                }
            }
        }

        // Draw texture preview plane
        if (drawTexturePreview && gridTexture != null)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawWireCube(
                new Vector3(gridCenter.x, 0, gridCenter.y),
                new Vector3(gridSizeValue, 0.1f, gridSizeValue)
            );
        }
    }

    void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;

        // Highlight selected grid with brighter colors
        Gizmos.color = new Color(0f, 1f, 0f, 0.5f);
        Vector3 gridOrigin = Application.isPlaying ? gridWorldOrigin :
            new Vector3(gridCenter.x - gridSize * cellSize * 0.5f, 0, gridCenter.y - gridSize * cellSize * 0.5f);
        float gridSizeValue = Application.isPlaying ? gridWorldSize : gridSize * cellSize;

        // Draw thicker boundary when selected
        Vector3[] corners = new Vector3[4]
        {
            gridOrigin,
            gridOrigin + new Vector3(gridSizeValue, 0, 0),
            gridOrigin + new Vector3(gridSizeValue, 0, gridSizeValue),
            gridOrigin + new Vector3(0, 0, gridSizeValue)
        };

        for (int i = 0; i < 4; i++)
        {
            Gizmos.DrawLine(corners[i], corners[(i + 1) % 4]);
            // Draw small spheres at corners
            Gizmos.DrawSphere(corners[i], 0.5f);
        }

        // Draw center marker
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(new Vector3(gridCenter.x, 0, gridCenter.y), 1f);
        Gizmos.DrawWireSphere(new Vector3(gridCenter.x, 0, gridCenter.y), 2f);
    }

    // Helper method to get grid cell position from world position
    public Vector2Int WorldToGridPosition(Vector3 worldPos)
    {
        int gridX = Mathf.FloorToInt((worldPos.x - gridWorldOrigin.x) / cellSize);
        int gridY = Mathf.FloorToInt((worldPos.z - gridWorldOrigin.z) / cellSize);
        return new Vector2Int(gridX, gridY);
    }

    // Helper method to get world position from grid cell
    public Vector3 GridToWorldPosition(Vector2Int gridPos)
    {
        float worldX = gridWorldOrigin.x + gridPos.x * cellSize + cellSize * 0.5f;
        float worldZ = gridWorldOrigin.z + gridPos.y * cellSize + cellSize * 0.5f;
        return new Vector3(worldX, 0, worldZ);
    }

    // Debug method to test grid coordinates
    public void DebugGridPosition(Vector3 worldPos)
    {
        Vector2Int gridPos = WorldToGridPosition(worldPos);
        Vector3 worldPosFromGrid = GridToWorldPosition(gridPos);

        Debug.Log($"World: {worldPos} -> Grid: {gridPos} -> World: {worldPosFromGrid}");
        Debug.Log($"Texture pixel at ({gridPos.x}, {gridPos.y}): " +
                 (gridTexture.GetPixel(gridPos.x, gridPos.y) == Color.white ? "Grass Allowed" : "No Grass"));
    }
}
