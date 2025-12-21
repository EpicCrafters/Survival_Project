using UnityEngine;

public class GrassTester : MonoBehaviour
{
    public GrassGridManager gridManager;
    public float testRadius = 5f;

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit))
            {
                gridManager.ClearArea(hit.point, testRadius);
                Debug.Log($"Cleared grass at {hit.point}");
            }
        }
    }

    void OnDrawGizmos()
    {
        if (gridManager != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, testRadius);
        }
    }
}