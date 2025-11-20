using UnityEngine;

public class BowStringController : MonoBehaviour
{
    [Header("String Bones (Assign in Inspector)")]
    [Tooltip("Top attachment point of the string")]
    public Transform stringTopBone;

    [Tooltip("Middle bone - arrow attaches here, RIGHT HAND IK target")]
    public Transform stringMiddleBone;

    [Tooltip("Bottom attachment point of the string")]
    public Transform stringBottomBone;

    [Header("Draw Settings")]
    [Tooltip("How far back the string can be pulled (in local space)")]
    public float maxDrawDistance = 0.3f;

    [Tooltip("Speed of string snap back animation")]
    public float releaseSpeed = 10f;

    [Header("Arrow")]
    [Tooltip("Visual arrow prefab spawned when drawing")]
    public GameObject arrowVisualPrefab;
    public Transform arrowSpawnPoint;
    [Header("Right Hand IK")]
    [Tooltip("Transform for RIGHT hand to follow string (auto-created as child of stringMiddleBone)")]
    public Transform rightHandIKTarget;

    // Private state
    private Vector3 middleBoneRestPosition;
    private Vector3 middleBoneRestLocalPosition;
    private GameObject currentArrowVisual;
    public bool isDrawing = false;
    public bool isReleasing = false;
    private float currentDrawAmount = 0f;

    private void Awake()
    {
        // Store rest positions
        if (stringMiddleBone != null)
        {
            middleBoneRestPosition = stringMiddleBone.position;
            middleBoneRestLocalPosition = stringMiddleBone.localPosition;
        }

        // Auto-create RIGHT hand IK target (follows string)
        //if (rightHandIKTarget == null && stringMiddleBone != null)
        //{
        //    GameObject rightIkTarget = new GameObject("RightHandIKTarget");
        //    rightIkTarget.transform.SetParent(stringMiddleBone);
        //    rightIkTarget.transform.localPosition = Vector3.zero;
        //    rightIkTarget.transform.localRotation = Quaternion.identity;
        //    rightHandIKTarget = rightIkTarget.transform;
        //}
    }

    public void StartDrawing()
    {
        if (isDrawing) return;

        isDrawing = true;
        isReleasing = false;
        currentDrawAmount = 0f;

        // Spawn visual arrow attached to middle bone
        if (arrowVisualPrefab != null && stringMiddleBone != null)
        {
            currentArrowVisual = Instantiate(arrowVisualPrefab, stringMiddleBone);
            currentArrowVisual.transform.localPosition = Vector3.zero;
            currentArrowVisual.transform.localRotation = Quaternion.identity;

            Debug.Log("[BowString] Arrow spawned on middle bone");
        }
    }

    public void UpdateDrawAmount(float drawPercent)
    {
        if (!isDrawing || stringMiddleBone == null) return;

        currentDrawAmount = Mathf.Clamp01(drawPercent);

        // Pull middle bone back along local -y axis (backward from bow)
        Vector3 pullOffset = -Vector3.up * (maxDrawDistance * currentDrawAmount);
        stringMiddleBone.localPosition = middleBoneRestLocalPosition + pullOffset;

        // Right hand IK target automatically follows since it's a child of stringMiddleBone
    }

    public void Release()
    {
        if (!isDrawing) return;

        isDrawing = false;
        isReleasing = true;

        // Remove arrow visual (projectile will be spawned by server)
        if (currentArrowVisual != null)
        {
            Destroy(currentArrowVisual);
            currentArrowVisual = null;
        }

        Debug.Log("[BowString] Released!");
    }

    public void CancelDraw()
    {
        if (!isDrawing && !isReleasing) return;

        isDrawing = false;
        isReleasing = true;

        // Remove arrow visual
        if (currentArrowVisual != null)
        {
            Destroy(currentArrowVisual);
            currentArrowVisual = null;
        }

        Debug.Log("[BowString] Draw cancelled");
    }

    private void Update()
    {
        // This runs BEFORE OnAnimatorIK
        if (isReleasing && stringMiddleBone != null)
        {
            stringMiddleBone.localPosition = Vector3.Lerp(
                stringMiddleBone.localPosition,
                middleBoneRestLocalPosition,
                releaseSpeed * Time.deltaTime
            );

            // Add debug visualization
            if (rightHandIKTarget != null)
            {
                Debug.DrawRay(rightHandIKTarget.position, Vector3.up * 0.1f, Color.cyan);
            }
        }
    }

    private void OnDisable()
    {
        // Clean up if item is unequipped while drawing
        if (isDrawing || isReleasing)
        {
            if (stringMiddleBone != null)
                stringMiddleBone.localPosition = middleBoneRestLocalPosition;

            if (currentArrowVisual != null)
            {
                Destroy(currentArrowVisual);
                currentArrowVisual = null;
            }

            isDrawing = false;
            isReleasing = false;
            currentDrawAmount = 0f;
        }
    }

    // Public getters for debugging
    public bool IsDrawing => isDrawing;
    public float CurrentDrawAmount => currentDrawAmount;

    // Optional: Visualize draw distance in editor
    private void OnDrawGizmosSelected()
    {
        if (stringMiddleBone == null) return;

        // Show string draw path
        Gizmos.color = Color.yellow;
        Vector3 restPos = Application.isPlaying ? middleBoneRestPosition : stringMiddleBone.position;
        Vector3 maxDrawPos = restPos + stringMiddleBone.TransformDirection(Vector3.back * maxDrawDistance);

        Gizmos.DrawLine(restPos, maxDrawPos);
        Gizmos.DrawWireSphere(maxDrawPos, 0.02f);
    }
}