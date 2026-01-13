using UnityEngine;

public class LookAtCamera : MonoBehaviour
{
    private enum Mode
    {
        LookAt,
        LookAtInverted,
        CameraForward,
        CameraForwardInverted,
    }

    [SerializeField] private Mode mode;
    [SerializeField] private bool useLocalPlayerCamera = true; // New option for multiplayer

    private Transform cameraTransform;
    private bool cameraFound = false;

    private void Start()
    {
        FindCamera();
    }

    private void FindCamera()
    {
        if (useLocalPlayerCamera)
        {
            // Try to find the local player's camera manager
            PlayerCameraManager[] cameraManagers = FindObjectsOfType<PlayerCameraManager>();

            foreach (PlayerCameraManager camManager in cameraManagers)
            {
                if (camManager.isLocalPlayer)
                {
                    cameraTransform = camManager.GetCameraTransform();
                    cameraFound = cameraTransform != null;

                    if (cameraFound)
                    {
                        return;
                    }
                }
            }

            // Fallback to Camera.main if no PlayerCameraManager found
            if (!cameraFound && Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
                cameraFound = true;
            }
        }
        else
        {
            // Use Camera.main directly (for non-multiplayer scenarios)
            if (Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
                cameraFound = true;
            }
        }
    }

    private void LateUpdate()
    {
        // If camera not found or lost, try to find it again
        if (!cameraFound || cameraTransform == null)
        {
            FindCamera();

            // Still no camera? Exit early
            if (!cameraFound || cameraTransform == null)
                return;
        }

        // Apply the selected mode
        switch (mode)
        {
            case Mode.LookAt:
                transform.LookAt(cameraTransform);
                break;

            case Mode.LookAtInverted:
                Vector3 dirFromCamera = transform.position - cameraTransform.position;
                transform.LookAt(transform.position + dirFromCamera);
                break;

            case Mode.CameraForward:
                transform.forward = cameraTransform.forward;
                break;

            case Mode.CameraForwardInverted:
                transform.forward = -cameraTransform.forward;
                break;
        }
    }

    // Optional: Manual camera assignment for special cases
    public void SetCamera(Transform camera)
    {
        cameraTransform = camera;
        cameraFound = camera != null;
    }

    // Force refresh camera reference
    public void RefreshCamera()
    {
        cameraFound = false;
        FindCamera();
    }
}