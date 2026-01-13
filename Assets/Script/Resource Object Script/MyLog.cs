using UnityEngine;
using Mirror;

public class MyLog : NetworkedChoppable
{
    public enum LogSize { Small, Medium, Large }

    [Header("Log Spawns")]
    [SerializeField] private Transform halfLogPrefab;
    [SerializeField] private LogSize logSize = LogSize.Medium;

    [Header("Spacing Settings")]
    [Tooltip("Distance between each half log along the log's length (Y-axis)")]
    [SerializeField] private float halfLogSpacing = 2.0f;

    [Header("Offset Settings")]
    [Tooltip("Offset along the log's length direction (local Y-axis). Positive = towards top, Negative = towards bottom")]
    [SerializeField] private float lengthOffset = 0f;

    [Tooltip("Offset perpendicular to log (local X-axis). Positive = right, Negative = left")]
    [SerializeField] private float lateralOffsetX = 0f;

    [Tooltip("Offset perpendicular to log (local Z-axis). Positive = forward, Negative = backward")]
    [SerializeField] private float lateralOffsetZ = 0f;

    [Tooltip("Offset in world up direction (always vertical regardless of log rotation)")]
    [SerializeField] private float worldUpOffset = 0.2f;

    [Header("Randomization")]
    [Tooltip("Add random perpendicular offset to prevent perfect alignment")]
    [SerializeField] private float lateralSpread = 0.3f;

    [Tooltip("Random offset range along length direction")]
    [SerializeField] private float randomLengthOffset = 0f;

    [Header("Physics")]
    [Tooltip("Apply physics force to spread half logs apart")]
    [SerializeField] private bool applyPhysicsForce = true;

    [SerializeField] private float physicsForceStrength = 1.5f;

    protected override int GetHealthAmount() => 25;

    protected override void OnResourceDestroyed()
    {
        if (halfLogPrefab == null)
        {
            Debug.LogWarning($"{name}: halfLogPrefab is null!");
            return;
        }

        int halfCount = logSize switch
        {
            LogSize.Small => 2,
            LogSize.Medium => 4,
            LogSize.Large => 6,
            _ => 4
        };

        DebugLog($"Log chopped - spawning {halfCount} half logs aligned along local Y-axis with spacing {halfLogSpacing}");

        // Use the log's LOCAL UP (Y-axis) - this is the length direction of the log
        Vector3 lengthDirection = transform.up;

        // Get perpendicular directions for offsets
        Vector3 perpendicular1 = transform.right;   // Local X
        Vector3 perpendicular2 = transform.forward; // Local Z

        // Use the log's current rotation for the half logs
        Quaternion baseRotation = transform.rotation;

        // Calculate starting position (centered along the log's length)
        float totalLength = (halfCount - 1) * halfLogSpacing;
        Vector3 centerOffset = lengthDirection * (totalLength / 2f);
        Vector3 startPos = transform.position - centerOffset;

        // Apply base offsets to the starting position
        startPos += lengthDirection * lengthOffset;       // Length offset (along log)
        startPos += perpendicular1 * lateralOffsetX;      // X offset (sideways)
        startPos += perpendicular2 * lateralOffsetZ;      // Z offset (forward/back)

        DebugLog($"Length direction (local Y): {lengthDirection}");
        DebugLog($"Total spread length: {totalLength}, Start position with offsets applied");

        for (int i = 0; i < halfCount; i++)
        {
            // Position each half log along the log's LOCAL Y-axis
            float distanceAlongLog = halfLogSpacing * i;
            Vector3 spawnPos = startPos + (lengthDirection * distanceAlongLog);

            // Add random length offset if enabled
            if (randomLengthOffset > 0f)
            {
                float randomLength = UnityEngine.Random.Range(-randomLengthOffset, randomLengthOffset);
                spawnPos += lengthDirection * randomLength;
            }

            // Add slight random lateral spread in both perpendicular directions
            if (lateralSpread > 0f)
            {
                float lateralX = UnityEngine.Random.Range(-lateralSpread, lateralSpread);
                float lateralZ = UnityEngine.Random.Range(-lateralSpread, lateralSpread);
                spawnPos += perpendicular1 * lateralX;
                spawnPos += perpendicular2 * lateralZ;
            }

            // Add vertical offset from ground (world up, not local)
            spawnPos += Vector3.up * worldUpOffset;

            // Align rotation with parent log, with slight random variation
            Quaternion spawnRot = baseRotation * Quaternion.Euler(
                UnityEngine.Random.Range(-8f, 8f),
                UnityEngine.Random.Range(-8f, 8f),
                UnityEngine.Random.Range(-8f, 8f)
            );

            DebugLog($"Half log {i + 1}/{halfCount}: Distance = {distanceAlongLog:F2}, Position = {spawnPos}");

            // Spawn the networked object
            var spawnedObj = SpawnNetworkedObjectWithReturn(halfLogPrefab, spawnPos, spawnRot);

            // Apply physics force to spread them apart
            if (applyPhysicsForce && spawnedObj != null)
            {
                var rb = spawnedObj.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    // Calculate direction from log center to this half log
                    Vector3 outwardDirection = (spawnPos - transform.position).normalized;

                    // Add slight upward component
                    outwardDirection += Vector3.up * 0.2f;
                    outwardDirection.Normalize();

                    float forceMagnitude = physicsForceStrength * UnityEngine.Random.Range(0.8f, 1.2f);
                    rb.AddForce(outwardDirection * forceMagnitude, ForceMode.Impulse);

                    // Add random spin
                    Vector3 randomTorque = UnityEngine.Random.insideUnitSphere * 0.5f;
                    rb.AddTorque(randomTorque, ForceMode.Impulse);

                    DebugLog($"Applied force: {outwardDirection * forceMagnitude}");
                }
            }
        }
    }

    // Helper method that returns the spawned GameObject
    protected GameObject SpawnNetworkedObjectWithReturn(Transform prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null || !NetworkServer.active)
        {
            Debug.LogWarning("Cannot spawn: prefab null or not server");
            return null;
        }

        var obj = Instantiate(prefab, position, rotation);

        // Ensure it has a Rigidbody for physics
        var rb = obj.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = obj.gameObject.AddComponent<Rigidbody>();
        }

        NetworkServer.Spawn(obj.gameObject);
        DebugLog($"Spawned networked object: {prefab.name} at {position}");

        return obj.gameObject;
    }

    // Visualize the spawn positions in Scene view
    private void OnDrawGizmosSelected()
    {
        if (halfLogPrefab == null) return;

        int halfCount = logSize switch
        {
            LogSize.Small => 2,
            LogSize.Medium => 4,
            LogSize.Large => 6,
            _ => 4
        };

        Vector3 lengthDirection = transform.up;
        Vector3 perpendicular1 = transform.right;
        Vector3 perpendicular2 = transform.forward;

        float totalLength = (halfCount - 1) * halfLogSpacing;
        Vector3 startPos = transform.position - (lengthDirection * (totalLength / 2f));

        // Apply offsets to visualization
        startPos += lengthDirection * lengthOffset;
        startPos += perpendicular1 * lateralOffsetX;
        startPos += perpendicular2 * lateralOffsetZ;

        // Draw the spawn line along the log
        Vector3 lineStart = startPos;
        Vector3 lineEnd = startPos + (lengthDirection * totalLength);

        Gizmos.color = Color.green;
        Gizmos.DrawLine(lineStart, lineEnd);

        // Draw arrows to show direction
        DrawArrow(lineStart, lengthDirection * 0.5f, Color.green);

        // Draw spawn points
        for (int i = 0; i < halfCount; i++)
        {
            Vector3 spawnPos = startPos + (lengthDirection * (halfLogSpacing * i));
            spawnPos += Vector3.up * worldUpOffset;

            // Draw sphere at spawn position
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(spawnPos, 0.3f);

            // Draw vertical line showing world up offset
            Gizmos.color = Color.red;
            Gizmos.DrawLine(spawnPos - Vector3.up * worldUpOffset, spawnPos);

            // Draw number label
#if UNITY_EDITOR
            UnityEditor.Handles.Label(spawnPos + Vector3.up * 0.5f, $"#{i + 1}");
#endif
        }

        // Draw center point of log
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.2f);

        // Draw offset indicators
        if (lengthOffset != 0f)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(transform.position, transform.position + lengthDirection * lengthOffset);
        }

        if (lateralOffsetX != 0f)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, transform.position + perpendicular1 * lateralOffsetX);
        }

        if (lateralOffsetZ != 0f)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(transform.position, transform.position + perpendicular2 * lateralOffsetZ);
        }
    }

    // Helper to draw arrows in gizmos
    private void DrawArrow(Vector3 position, Vector3 direction, Color color)
    {
        Gizmos.color = color;
        Gizmos.DrawRay(position, direction);

        // Arrow head
        Vector3 right = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 180 + 20, 0) * Vector3.forward;
        Vector3 left = Quaternion.LookRotation(direction) * Quaternion.Euler(0, 180 - 20, 0) * Vector3.forward;

        Gizmos.DrawRay(position + direction, right * 0.2f);
        Gizmos.DrawRay(position + direction, left * 0.2f);
    }
}