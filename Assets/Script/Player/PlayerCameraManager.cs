using UnityEngine;
using Unity.Cinemachine;
using Mirror;

public class PlayerCameraManager : NetworkBehaviour
{
    [Header("Camera References")]
    [SerializeField] private CinemachineCamera normalFreeLookCamera;
    [SerializeField] private CinemachineCamera aimFreeLookCamera;

    private Camera mainCamera;
    private bool isAiming = false;

    // Cache Cinemachine Orbital Follow components
    private CinemachineOrbitalFollow normalOrbitalFollow;
    private CinemachineOrbitalFollow aimOrbitalFollow;

    // Khởi tạo cameras từ CameraManager (gọi bởi PlayerSetup)
    public void InitializeCameras(CinemachineCamera normalCam, CinemachineCamera aimCam, Transform playerTransform)
    {
        if (!isLocalPlayer) return;

        // Gán cameras từ CameraManager
        normalFreeLookCamera = normalCam;
        aimFreeLookCamera = aimCam;

        // Tự động gán Follow và LookAt targets cho cả 2 cameras
        if (normalFreeLookCamera != null)
        {
            normalFreeLookCamera.Follow = playerTransform;
            normalFreeLookCamera.LookAt = playerTransform;
            normalOrbitalFollow = normalFreeLookCamera.GetComponent<CinemachineOrbitalFollow>();
            Debug.Log($"[PlayerCameraManager] Normal camera đã gán target: {playerTransform.name}");
        }

        if (aimFreeLookCamera != null)
        {
            aimFreeLookCamera.Follow = playerTransform;
            aimFreeLookCamera.LookAt = playerTransform;
            aimOrbitalFollow = aimFreeLookCamera.GetComponent<CinemachineOrbitalFollow>();
            Debug.Log($"[PlayerCameraManager] Aim camera đã gán target: {playerTransform.name}");
        }

        // Lấy main camera
        mainCamera = Camera.main;

        // Set priorities và active states
        if (normalFreeLookCamera != null)
        {
            normalFreeLookCamera.Priority.Value = 15;
            normalFreeLookCamera.gameObject.SetActive(true);
            Debug.Log($"[PlayerCameraManager] Normal camera kích hoạt với priority {normalFreeLookCamera.Priority.Value}");
        }

        if (aimFreeLookCamera != null)
        {
            aimFreeLookCamera.Priority.Value = 10;
            aimFreeLookCamera.gameObject.SetActive(false);
            Debug.Log($"[PlayerCameraManager] Aim camera tắt");
        }

        Debug.Log("[PlayerCameraManager] Cameras khởi tạo thành công!");
    }

    // Đồng bộ góc xoay giữa 2 camera
    private void SyncCameraRotations(CinemachineOrbitalFollow fromCamera, CinemachineOrbitalFollow toCamera)
    {
        if (fromCamera == null || toCamera == null) return;

        // Copy giá trị xoay từ camera cũ sang camera mới
        toCamera.HorizontalAxis.Value = fromCamera.HorizontalAxis.Value;
        toCamera.VerticalAxis.Value = fromCamera.VerticalAxis.Value;

        Debug.Log($"[PlayerCameraManager] Đồng bộ góc xoay: H={toCamera.HorizontalAxis.Value:F2}, V={toCamera.VerticalAxis.Value:F2}");
    }

    // Set camera về chế độ aim - chỉ chuyển nếu đang cầm cung
    public void SetAimingMode(bool aiming, ItemData itemData = null)
    {
        if (!isLocalPlayer) return;

        // Chỉ cho phép chuyển camera aim nếu đang cầm CUNG
        bool shouldSwitchCamera = aiming;

        if (aiming && itemData != null)
        {
            // Kiểm tra nếu item là cung
            if (itemData.weapon == null || itemData.weapon.weaponType != WeaponType.Bow)
            {
                Debug.Log("[PlayerCameraManager] Không chuyển sang aim camera - không phải cung");
                shouldSwitchCamera = false;
            }
        }
        else if (aiming && itemData == null)
        {
            // Nếu không có item data khi đang aim, không chuyển
            Debug.Log("[PlayerCameraManager] Không chuyển sang aim camera - không có item data");
            shouldSwitchCamera = false;
        }

        // Chuyển đổi giữa cameras
        if (aimFreeLookCamera != null && normalFreeLookCamera != null)
        {
            if (shouldSwitchCamera)
            {
                // Đồng bộ góc xoay từ normal sang aim
                SyncCameraRotations(normalOrbitalFollow, aimOrbitalFollow);

                // Kích hoạt aim camera với priority cao hơn
                aimFreeLookCamera.Priority.Value = 15;
                aimFreeLookCamera.gameObject.SetActive(true);

                // Tắt normal camera
                normalFreeLookCamera.Priority.Value = 10;
                normalFreeLookCamera.gameObject.SetActive(false);

                Debug.Log("[PlayerCameraManager] Chuyển sang AIM camera (Cung được trang bị)");
            }
            else
            {
                // Đồng bộ góc xoay từ aim về normal (nếu đang aim)
                if (isAiming && aimOrbitalFollow != null)
                {
                    SyncCameraRotations(aimOrbitalFollow, normalOrbitalFollow);
                }

                // Kích hoạt normal camera với priority cao hơn
                normalFreeLookCamera.Priority.Value = 15;
                normalFreeLookCamera.gameObject.SetActive(true);

                // Tắt aim camera
                aimFreeLookCamera.Priority.Value = 10;
                aimFreeLookCamera.gameObject.SetActive(false);

                Debug.Log("[PlayerCameraManager] Chuyển về NORMAL camera");
            }
        }

        isAiming = shouldSwitchCamera;
    }

    // Lấy hướng forward của camera (hữu ích cho di chuyển)
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

    // Lấy hướng right của camera (hữu ích cho di chuyển ngang)
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

    // Lấy camera transform (hữu ích cho raycast)
    public Transform GetCameraTransform()
    {
        return mainCamera != null ? mainCamera.transform : transform;
    }

    // Kiểm tra đang aim hay không
    public bool IsAiming() => isAiming;

    private void OnDisable()
    {
        // Dọn dẹp cameras khi player disconnect
        if (isLocalPlayer)
        {
            if (normalFreeLookCamera != null)
                normalFreeLookCamera.gameObject.SetActive(false);
            if (aimFreeLookCamera != null)
                aimFreeLookCamera.gameObject.SetActive(false);
        }
    }
}