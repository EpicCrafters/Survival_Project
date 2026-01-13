using UnityEngine;
using UnityEngine.UI;

public class CrosshairManager : MonoBehaviour
{
    [Header("Crosshair Image Reference")]
    [SerializeField] private Image crosshairImage; // ✨ Reference to Image in Canvas

    [Header("Crosshair Settings")]
    [SerializeField] private float maxScale = 1.5f; // Kích thước tối đa khi bắt đầu charge
    [SerializeField] private float minScale = 0.5f; // Kích thước tối thiểu khi full charge
    [SerializeField] private float shrinkSpeed = 3f; // Tốc độ thu nhỏ crosshair
    [SerializeField] private float fadeInSpeed = 5f; // Tốc độ hiện crosshair

    [Header("Color Settings")]
    [SerializeField] private Color normalColor = new Color(1f, 1f, 1f, 0.8f);
    [SerializeField] private Color fullChargeColor = new Color(0f, 1f, 0f, 1f);

    private CanvasGroup canvasGroup;
    private RectTransform rectTransform;

    private float currentScale;
    private float targetScale;
    private bool isVisible = false;
    private float currentAlpha = 0f;

    private void Awake()
    {
        // ✨ Tự động tìm crosshair image nếu chưa được gán
        if (crosshairImage == null)
        {
            crosshairImage = GetComponentInChildren<Image>(true);
            if (crosshairImage != null)
            {
                Debug.Log($"[CrosshairManager] ✅ Auto-found crosshair image: {crosshairImage.gameObject.name}");
            }
        }

        // Kiểm tra Image component
        if (crosshairImage == null)
        {
            Debug.LogError("[CrosshairManager] Không tìm thấy Image component! Vui lòng gán Image vào Inspector hoặc đặt Image làm child của GameObject này.");
            return;
        }

        // Lấy components từ crosshair image
        rectTransform = crosshairImage.GetComponent<RectTransform>();
        canvasGroup = crosshairImage.GetComponent<CanvasGroup>();

        // Tạo CanvasGroup nếu chưa có (để fade in/out)
        if (canvasGroup == null)
        {
            canvasGroup = crosshairImage.gameObject.AddComponent<CanvasGroup>();
            Debug.Log("[CrosshairManager] CanvasGroup added to crosshair image");
        }

        // Ẩn crosshair ban đầu
        currentScale = maxScale;
        targetScale = maxScale;
        canvasGroup.alpha = 0f;
        currentAlpha = 0f;
        isVisible = false;

        // Set scale ban đầu
        if (rectTransform != null)
        {
            rectTransform.localScale = Vector3.one * currentScale;
        }
    }

    private void Update()
    {
        if (crosshairImage == null) return;

        // Smooth scale interpolation
        currentScale = Mathf.Lerp(currentScale, targetScale, Time.deltaTime * shrinkSpeed);

        // Update scale
        if (rectTransform != null)
        {
            rectTransform.localScale = Vector3.one * currentScale;
        }

        // Smooth alpha fade
        if (isVisible)
        {
            currentAlpha = Mathf.Lerp(currentAlpha, 1f, Time.deltaTime * fadeInSpeed);
        }
        else
        {
            currentAlpha = Mathf.Lerp(currentAlpha, 0f, Time.deltaTime * fadeInSpeed * 2f); // Fade out nhanh hơn
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = currentAlpha;
        }

        // Update màu sắc dựa trên charge
        if (isVisible)
        {
            UpdateCrosshairColor();
        }
    }

    /// <summary>
    /// Hiển thị crosshair khi bắt đầu charge bow
    /// </summary>
    public void ShowCrosshair()
    {
        isVisible = true;
        currentScale = maxScale;
        targetScale = maxScale;

        if (crosshairImage != null)
        {
            crosshairImage.color = normalColor;
        }

        Debug.Log("[CrosshairManager] Crosshair hiển thị - Bắt đầu charge");
    }

    /// <summary>
    /// Ẩn crosshair khi ngừng charge hoặc bắn
    /// </summary>
    public void HideCrosshair()
    {
        isVisible = false;
        Debug.Log("[CrosshairManager] Crosshair ẩn");
    }

    /// <summary>
    /// Update crosshair dựa trên charge percent (0.0 đến 1.0)
    /// </summary>
    /// <param name="chargePercent">Phần trăm charge từ 0 đến 1</param>
    public void UpdateCharge(float chargePercent)
    {
        if (!isVisible) return;

        // Clamp charge percent
        chargePercent = Mathf.Clamp01(chargePercent);

        // Tính target scale: từ max (0%) đến min (100%)
        targetScale = Mathf.Lerp(maxScale, minScale, chargePercent);
    }

    /// <summary>
    /// Reset crosshair về trạng thái ban đầu
    /// </summary>
    public void ResetCrosshair()
    {
        currentScale = maxScale;
        targetScale = maxScale;

        if (rectTransform != null)
        {
            rectTransform.localScale = Vector3.one * currentScale;
        }

        if (crosshairImage != null)
        {
            crosshairImage.color = normalColor;
        }
    }

    /// <summary>
    /// Update màu sắc crosshair dựa trên charge
    /// </summary>
    private void UpdateCrosshairColor()
    {
        if (crosshairImage == null) return;

        // Tính charge percent dựa trên scale hiện tại
        float chargePercent = 1f - ((currentScale - minScale) / (maxScale - minScale));
        chargePercent = Mathf.Clamp01(chargePercent);

        // Lerp giữa màu bình thường và màu full charge
        Color targetColor = Color.Lerp(normalColor, fullChargeColor, chargePercent);
        crosshairImage.color = targetColor;
    }

    /// <summary>
    /// Kiểm tra crosshair có đang hiển thị không
    /// </summary>
    public bool IsVisible() => isVisible;

    /// <summary>
    /// Set màu crosshair thủ công
    /// </summary>
    public void SetCrosshairColor(Color color)
    {
        if (crosshairImage != null)
        {
            crosshairImage.color = color;
        }
    }

    /// <summary>
    /// Set scale thủ công (để test)
    /// </summary>
    public void SetScale(float scale)
    {
        targetScale = Mathf.Clamp(scale, minScale, maxScale);
    }

    /// <summary>
    /// Set crosshair image reference manually (nếu cần)
    /// </summary>
    public void SetCrosshairImage(Image image)
    {
        crosshairImage = image;

        if (crosshairImage != null)
        {
            rectTransform = crosshairImage.GetComponent<RectTransform>();
            canvasGroup = crosshairImage.GetComponent<CanvasGroup>();

            if (canvasGroup == null)
            {
                canvasGroup = crosshairImage.gameObject.AddComponent<CanvasGroup>();
            }

            Debug.Log($"[CrosshairManager] Crosshair image set: {image.gameObject.name}");
        }
    }
}