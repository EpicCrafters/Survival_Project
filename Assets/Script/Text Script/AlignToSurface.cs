using UnityEngine;
using UnityEngine.AI;

public class AlignToSurface : MonoBehaviour
{
    public LayerMask groundLayer; // Assign your ground layer in the Inspector
    public float rotationSpeed = 10f; // Adjust for smoother or faster rotation
    private NavMeshAgent navAgent;

    void Awake()
    {
        navAgent = GetComponentInParent<NavMeshAgent>();
        if (navAgent != null)
        {
            navAgent.updateRotation = false; // Disable NavMeshAgent's rotation
        }
    }

    void Update()
    {
        RaycastHit hit;
        if (Physics.Raycast(transform.position, -Vector3.up, out hit, 1f, groundLayer))
        {
            // Calculate forward direction projected onto the surface
            Vector3 projectedForward = Vector3.ProjectOnPlane(navAgent.velocity.normalized, hit.normal);
            if (projectedForward == Vector3.zero) // If agent is stationary
            {
                projectedForward = transform.forward; // Maintain current forward
            }

            // Create target rotation
            Quaternion targetRotation = Quaternion.LookRotation(projectedForward, hit.normal);

            // Smoothly rotate the child object
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
        }
    }
}