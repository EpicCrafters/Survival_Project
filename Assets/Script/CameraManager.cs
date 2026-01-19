using UnityEngine;
using Unity.Cinemachine;

public class CameraManager : MonoBehaviour
{
    [Header("Cinemachine Cameras")]
    public CinemachineCamera normalFreeLookCamera;
    public CinemachineCamera aimFreeLookCamera;

    /// <summary>
    /// Assign camera targets to PlayerCameraManager
    /// </summary>
    public void AssignCameraToPlayer(PlayerCameraManager playerCameraManager, Transform playerTransform)
    {
        if (playerCameraManager == null)
        {
            Debug.LogError("[CameraManager] PlayerCameraManager is null!");
            return;
        }

        if (normalFreeLookCamera == null)
        {
            Debug.LogError("[CameraManager] Normal FreeLook Camera is not assigned in CameraManager!");
            return;
        }

        // Pass the cameras to the player's camera manager
        playerCameraManager.InitializeCameras(normalFreeLookCamera, aimFreeLookCamera, playerTransform);

        Debug.Log($"[CameraManager] Assigned cameras to player {playerTransform.name}");
    }
}