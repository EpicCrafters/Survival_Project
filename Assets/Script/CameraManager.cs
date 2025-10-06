using UnityEngine;
using Unity.Cinemachine;

public class CameraManager : MonoBehaviour
{
    public CinemachineCamera freeLookCamera;

    public void AssignCameraTargets(Transform playerTransform)
    {
        if (freeLookCamera != null)
        {
            freeLookCamera.Follow = playerTransform;
            freeLookCamera.LookAt = playerTransform;
            Camera.main.transform.SetParent(freeLookCamera.transform, false);
        }
    }
}
