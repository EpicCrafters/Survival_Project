using UnityEngine;
using Unity.Cinemachine;
using Mirror;

public class PlayerCameraManager : NetworkBehaviour
{
    [Header("Camera Reference")]
    [SerializeField] private CinemachineCamera freeLookCamera;

    [Header("Normal Mode Settings (Center/Back)")]
    [SerializeField] private Vector3 normalCameraOffset = new Vector3(0, 0.5f, 0);
    [SerializeField] private float normalOrbitRadius = 5f;
    [SerializeField] private Vector2 normalOrbitAngles = new Vector2(0f, 0f);

    [Header("Aim Mode Settings (Over-Shoulder)")]
    [SerializeField] private Vector3 aimCameraOffset = new Vector3(0.8f, 0.3f, 0);
    [SerializeField] private float aimOrbitRadius = 2.5f;
    [SerializeField] private Vector2 aimOrbitAngles = new Vector2(0f, 0f);

    [Header("Transition Settings")]
    [SerializeField] private float transitionSpeed = 5f;

    [Header("Camera Shake Settings")]
    [SerializeField] private float shakeAmplitude = 1f;
    [SerializeField] private float shakeFrequency = 2f;
    [SerializeField] private float shakeDuration = 0.3f;

    private Camera mainCamera;
    private bool isAiming = false;
    private bool isTransitioning = false;

    // Cache Cinemachine components
    private CinemachineOrbitalFollow orbitalFollow;
    private GameObject followOffsetObject;
    private CinemachineBasicMultiChannelPerlin noiseComponent;

    // Target values for smooth transition
    private Vector3 targetCameraOffset;
    private float targetOrbitRadius;
    private Vector2 targetOrbitAngles;

    // Current values (for smooth lerp)
    private Vector3 currentCameraOffset;
    private float currentOrbitRadius;
    private Vector2 currentOrbitAngles;

    // Shake variables
    private float shakeTimer = 0f;
    private bool isShaking = false;

    public void InitializeCameras(CinemachineCamera freeLook, CinemachineCamera aimCam, Transform playerTransform)
    {
        if (!isLocalPlayer) return;

        freeLookCamera = freeLook;

        if (freeLookCamera != null)
        {
            // Create a dynamic follow offset object as child of player
            followOffsetObject = new GameObject("CameraFollowOffset");
            followOffsetObject.transform.SetParent(playerTransform);
            followOffsetObject.transform.localPosition = normalCameraOffset;
            followOffsetObject.transform.localRotation = Quaternion.identity;

            // Set the offset object as follow/lookat target
            freeLookCamera.Follow = followOffsetObject.transform;
            freeLookCamera.LookAt = followOffsetObject.transform;
            freeLookCamera.Priority.Value = 10;
            freeLookCamera.gameObject.SetActive(true);

            orbitalFollow = freeLookCamera.GetComponent<CinemachineOrbitalFollow>();

            // Add or get the noise component for camera shake
            noiseComponent = freeLookCamera.GetComponent<CinemachineBasicMultiChannelPerlin>();
            if (noiseComponent == null)
            {
                noiseComponent = freeLookCamera.GetComponent<CinemachineBasicMultiChannelPerlin>();
            }
            noiseComponent.AmplitudeGain = 0f;
            noiseComponent.FrequencyGain = 0f;
        }

        // Disable aim camera if exists
        if (aimCam != null)
        {
            aimCam.gameObject.SetActive(false);
        }

        mainCamera = Camera.main;
        Debug.Log("[PlayerCameraManager] Camera setup complete with camera shake support!");
    }

    private void Update()
    {
        if (!isLocalPlayer) return;

        // Update camera shake
        //UpdateCameraShake();
    }

    private void UpdateCameraShake()
    {
        if (!isShaking || noiseComponent == null) return;

        shakeTimer += Time.deltaTime;

        if (shakeTimer < shakeDuration)
        {
            // Calculate shake progress (0 to 1)
            float progress = shakeTimer / shakeDuration;

            // Reduce amplitude and frequency over time for smooth fade-out
            float currentAmplitude = shakeAmplitude * (1f - progress);
            float currentFrequency = shakeFrequency * (1f - progress);

            // Apply shake to noise component
            noiseComponent.AmplitudeGain = currentAmplitude;
            noiseComponent.FrequencyGain = currentFrequency;
        }
        else
        {
            // Stop shaking
            noiseComponent.AmplitudeGain = 0f;
            noiseComponent.FrequencyGain = 0f;
            isShaking = false;
        }
    }

    // Call this function when player takes damage
    public void ShakeCamera(float amplitude = 0f, float frequency = 0f, float duration = 0f)
    {
        if (!isLocalPlayer || noiseComponent == null) return;

        // Use custom values or defaults
        shakeAmplitude = amplitude > 0 ? amplitude : this.shakeAmplitude;
        shakeFrequency = frequency > 0 ? frequency : this.shakeFrequency;
        shakeDuration = duration > 0 ? duration : this.shakeDuration;

        shakeTimer = 0f;
        isShaking = true;
    }

    public Vector3 GetCameraForward()
    {
        if (mainCamera != null)
        {
            Vector3 forward = mainCamera.transform.forward;
            forward.y = 0;
            return forward.normalized;
        }
        return transform.forward;
    }

    public Vector3 GetCameraRight()
    {
        if (mainCamera != null)
        {
            Vector3 right = mainCamera.transform.right;
            right.y = 0;
            return right.normalized;
        }
        return transform.right;
    }

    public Transform GetCameraTransform()
    {
        return mainCamera != null ? mainCamera.transform : transform;
    }

    public bool IsAiming() => isAiming;

    private void OnDisable()
    {
        if (isLocalPlayer && freeLookCamera != null)
        {
            freeLookCamera.Priority.Value = 0;
            freeLookCamera.gameObject.SetActive(false);
        }

        // Clean up offset object
        if (followOffsetObject != null)
        {
            Destroy(followOffsetObject);
        }
    }
}

// How to use:
// When player takes damage, call:
// playerCameraManager.ShakeCamera(); // Uses default amplitude, frequency and duration
// 
// Or with custom values:
// playerCameraManager.ShakeCamera(1.5f, 3f, 0.5f); // amplitude: 1.5, frequency: 3, duration: 0.5s
// 
// Amplitude controls the intensity/strength of the shake (how far the camera moves)
// Frequency controls the speed of the shake (how fast it vibrates)