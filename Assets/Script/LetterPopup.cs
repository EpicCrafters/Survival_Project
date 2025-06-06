using TMPro;
using UnityEngine;

public class LetterPopup : MonoBehaviour
{
    private TextMeshPro textMesh;
    private Transform targetTransform;
    private Vector3 offset;

    public void Setup(Transform target)
    {
        targetTransform = target;
        textMesh = GetComponent<TextMeshPro>();
        
        textMesh.fontSize = 10;
        textMesh.color = Color.white;
        offset = new Vector3(0, 2f, 0); // Offset above object
    }

    void Update()
    {
        if (targetTransform == null)
        {
            Destroy(gameObject);
            return;
        }

        // Keep the letter above the target object and facing the camera
        transform.position = targetTransform.position + offset;
        transform.rotation = Quaternion.LookRotation(Camera.main.transform.forward);
    }
}
