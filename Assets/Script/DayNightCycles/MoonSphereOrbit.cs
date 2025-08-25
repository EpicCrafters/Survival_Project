using UnityEngine;

public class SphereMoonOrbit : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform sunTransform;  // Gán dailyRotation HOẶC Sun Light
    [SerializeField] private float distance = 1000f;  // Khoảng cách
    [SerializeField] private float scale = 50f;       // Kích thước Moon Sphere

    [Header("Optional Fade")]
    [SerializeField] private Renderer sphereRenderer;

    private MaterialPropertyBlock block;

    private void Awake()
    {
        if (sphereRenderer != null)
            block = new MaterialPropertyBlock();
    }

    private void LateUpdate()
    {
        if (sunTransform == null) return;

        // Lấy hướng Sun
        Vector3 sunForward = sunTransform.forward;

        // Hướng Moon = ngược Sun
        Vector3 moonDirection = -sunForward;

        // Đặt SphereMoon ra xa theo hướng Moon
        transform.position = sunTransform.position + moonDirection * distance;

        // Quay Moon Sphere hướng về Earth => không cần LookAt nếu không có texture
        transform.rotation = Quaternion.LookRotation(-moonDirection, Vector3.up);

        transform.localScale = Vector3.one * scale;

        // Nếu muốn fade:
        if (sphereRenderer != null)
        {
            float dot = Vector3.Dot(sunForward, Vector3.down);
            float sunIntensity = Mathf.Clamp01(dot);
            float alpha = 1f - sunIntensity;

            sphereRenderer.GetPropertyBlock(block);
            block.SetColor("_Color", new Color(1, 1, 1, alpha));
            sphereRenderer.SetPropertyBlock(block);
        }
    }
}
