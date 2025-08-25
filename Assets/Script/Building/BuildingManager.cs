using System;
using System.Collections.Generic;
using UnityEngine;
using static UnityEditor.PlayerSettings;

public class BuildManager : MonoBehaviour
{
    [SerializeField] private float cellHeight = 5.0f;
    [SerializeField] private float cellWidth = 5.0f;
    [SerializeField] private float cellHeightBounus = 5.0f;
    [SerializeField] private bool visualizeGrid = true;
    [SerializeField] private int gridRadiusInCells = 5;
    [SerializeField] private float gizmoSize = 0.2f;
    //[SerializeField] private LayerMask groundLayer; // Layer cho Ground
    [SerializeField] private Material transparentMat;
    //public List<BuiltObjectData> builtObjects = new List<BuiltObjectData>();

    public static BuildManager Instance { get; private set; }
    private float rotY;
    private ItemData currentItem;

    ItemData visualObjectType;
    Transform visualiseObject;
    public PlayerHoldingItem playerHoling;

    [SerializeField] LayerMask buildLayer;
    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Update()
    {

        if (currentItem == null) return;
        Transform cam = Camera.main.transform;
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        // Nếu là tool phá building
        if (currentItem.type == ItemType.Tool)
        {
            if (Physics.Raycast(ray, out RaycastHit hitInfo, 30, buildLayer))
            {
                VisualiseDestroyObject(hitInfo.collider.gameObject);
            }
            else
            {
                EndVisualiseDestroyObject(); // bỏ highlight khi không trỏ vào gì
            }
        }
        else // Nếu là item xây dựng
        {

            if (Physics.Raycast(ray, out RaycastHit hitInfo, 30, currentItem.building.groundMask))
            {
                PlaceObject(hitInfo.point, currentItem);
            }
        }
    }

    public void SetCurrentItem(ItemData item, PlayerHoldingItem playerH)
    {
        currentItem = item;
        playerHoling = playerH;
    }


    private void OnDrawGizmos()
    {
        if (!visualizeGrid)
            return;

        Gizmos.color = Color.white;
        Vector3 center = GetNearestGridPosition(transform.position);

        for (int x = -gridRadiusInCells; x <= gridRadiusInCells; x++)
        {
            for (int y = -gridRadiusInCells; y <= gridRadiusInCells; y++)
            {
                for (int z = -gridRadiusInCells; z <= gridRadiusInCells; z++)
                {
                    Vector3 position = center + new Vector3(x * cellWidth, y * cellHeight, z * cellWidth);
                    Gizmos.DrawCube(position, Vector3.one * gizmoSize);
                }
            }
        }
    }

    private Vector3 GetNearestGridPosition(Vector3 position)
    {
        float x = Mathf.Round(position.x / cellWidth) * cellWidth;
        float y = Mathf.Round(position.y / cellHeight) * cellHeight;
        float z = Mathf.Round(position.z / cellWidth) * cellWidth;

        y += cellHeightBounus; // nâng lên nửa chiều cao ô
        return new Vector3(x, y, z);
    }

    public void PlaceObject(Vector3 position, ItemData obj)
    {
        Vector3 basePos = position;
        if (Input.GetKey(KeyCode.Q))
            rotY -= 90;
        else if (Input.GetKey(KeyCode.E))
            rotY += 90;
        position = GetNearestGridPosition(position);

        if (obj.building.snapToGridEdge)
        {
            Vector2 direction = new Vector2(basePos.x - position.x, basePos.z - position.z);
            float x = direction.x < 0 ? -1 : 1;
            float z = direction.y < 0 ? -1 : 1;

            if (Mathf.Abs(direction.x) < Mathf.Abs(direction.y))
            {
                rotY = -90;
                position += new Vector3(0, 0, cellWidth * z / 2);
            }
            else
            {
                rotY = 0;
                position += new Vector3(cellWidth * x / 2, 0, 0);
            }
        }
        if (visualiseObject == null || visualObjectType != obj)
        {
            StartVisualisingObject(obj);
        }
        VisualIsGameObject(position, rotY, obj);
    }
    public void VisualIsGameObject(Vector3 pos, float rotY, ItemData obj)
    {
        bool isOccupied = false;

        Vector3 direction = (pos - transform.position).normalized;

        visualiseObject.position = pos + direction * -0.01f;
        visualiseObject.rotation = Quaternion.Euler(0, rotY, 0);

        //set colider
        Collider[] colliders = visualiseObject.GetComponentsInChildren<Collider>();
        foreach (Collider collider in colliders)
        {
            bool BreakBothLoops = false;
            collider.isTrigger = true;
            RaycastHit[] hits = Physics.BoxCastAll(collider.bounds.center, new Vector3(collider.bounds.size.x * 0.4f, collider.bounds.size.y * 0.4f,
                collider.bounds.size.z * 0.4f), Vector3.up, Quaternion.identity, 1, buildLayer);
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider != null && hit.collider.GetComponentInParent<BuildtObject>() != null &&
                    !obj.building.ignorObject.Contains(hit.collider.GetComponentInParent<BuildtObject>().objectType))
                {
                    isOccupied = true;
                    BreakBothLoops = true;
                    break;
                }
            }
            if (BreakBothLoops)
            {
                break;
            }
        }

        Color color = isOccupied ? new Color(0.7f, 0.3f, 0.3f, 0.5f) : new Color(0.3f, 0.7f, 0.3f, 0.5f);
        transparentMat.color = color;

        if (Input.GetMouseButtonDown(0) && !isOccupied)
        {
            //Debug.Log("Dat vat the");
            BuildingObject(pos, rotY, obj);
        }
    }
    private void StartVisualisingObject(ItemData obj)
    {
        visualObjectType = obj;
        if (visualiseObject != null)
        {
            Destroy(visualiseObject.gameObject);
        }
        visualiseObject = Instantiate(visualObjectType.worldPrefab).transform;
        visualiseObject.localScale = new Vector3(cellWidth, cellHeight, cellWidth);

        MeshRenderer renderer = visualiseObject.GetComponentInChildren<MeshRenderer>();
        Material[] materials = new Material[renderer.materials.Length];
        for (int i = 0; i < materials.Length; i++)
        {
            materials[i] = transparentMat;
        }
        renderer.materials = materials;

        visualiseObject.gameObject.layer = 2;
        foreach (Transform child in visualiseObject)
        {
            child.gameObject.layer = 2;
        }
    }

    public void EndVisualisingObject()
    {
        if (visualiseObject != null)
        {
            Destroy(visualiseObject.gameObject);
            visualiseObject = null;
            visualObjectType = null;
        }
    }
    GameObject lastVisualiseDestroyObject;
    Material[] lastDestoryObjectMaterials;

    public void VisualiseDestroyObject(GameObject raycatedOject)
    {
        // Kiểm tra xem người chơi có cầm tool hợp lệ không
        if (currentItem == null || currentItem.type != ItemType.Tool)
        {
            EndVisualiseDestroyObject(); // nếu không phải tool thì bỏ highlight
            return;
        }
        BuildtObject buildingObject = raycatedOject.GetComponentInParent<BuildtObject>();
        if (buildingObject == null)
        {
            return;
        }
        if (lastVisualiseDestroyObject != raycatedOject)
        {
            EndVisualiseDestroyObject();
            StartVisualiseDestroyObject(raycatedOject);
        }

        if (Input.GetMouseButtonDown(0))
        {
            DestroyObject(raycatedOject);
        }
    }
    public void EndVisualiseDestroyObject()
    {
        if (lastVisualiseDestroyObject == null)
        {
            return;
        }

        MeshRenderer renderer = lastVisualiseDestroyObject.GetComponentInChildren<MeshRenderer>();

        renderer.materials = lastDestoryObjectMaterials;

        lastVisualiseDestroyObject = null;

    }
    public void StartVisualiseDestroyObject(GameObject raycastedObject)
    {
        lastVisualiseDestroyObject = raycastedObject;
        MeshRenderer renderer = raycastedObject.GetComponentInChildren<MeshRenderer>();

        lastDestoryObjectMaterials = renderer.materials;
        Material[] materials = new Material[renderer.materials.Length];

        for (int i = 0; i < materials.Length; i++)
        {
            materials[i] = transparentMat;
        }
        renderer.materials = materials;

        transparentMat.color = new Color(0.3f, 0.7f, 0.3f, 0.5f);
    }
    private void DestroyObject(GameObject raycastedObject)
    {
        BuildtObject obj = raycastedObject.GetComponentInParent<BuildtObject>();

        if (obj != null)
        {
            // Nếu muốn sau này có hệ thống lưu thì remove ở đây
            // builtObjects.RemoveAt(...) 

            Destroy(obj.gameObject);
        }
    }

    private void BuildingObject(Vector3 pos, float rotY, ItemData obj)
    {
        GameObject newObj = Instantiate(obj.worldPrefab, pos, Quaternion.Euler(0, rotY, 0));
        newObj.transform.localScale = new Vector3(cellWidth, cellHeight, cellWidth);

        int LayerIndex = Mathf.FloorToInt(Mathf.Log(buildLayer, 2));

        foreach (Transform child in newObj.transform)
        {
            child.gameObject.layer = LayerIndex;
        }
        newObj.layer = LayerIndex;

        BuildtObject buildingObject = newObj.AddComponent<BuildtObject>();

        buildingObject.objectType = obj;

        //playerHoling.OnPlaced();
    }
    //[SerializeField]
    //[System.Serializable]
    //public class BuiltObjectData
    //{
    //    public Vector3 position;
    //    public Vector3 rotation;
    //    public ItemData objectType;

    //    public BuiltObjectData(Vector3 pos, Vector3 rot, ItemData obj)
    //    {
    //        position = pos;
    //        rotation = rot;
    //        objectType = obj;
    //    }
    //}

    //[SerializeField]
    //[System.Serializable]
    //public class BuiltObjectsData
    //{
    //    public List<BuiltObjectData> builtObjectData;
    //}

}
