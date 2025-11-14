using UnityEngine;
using UnityEngine.UI;

public class HealthBarUI : MonoBehaviour
{
    public HealthSystem healthSystem;
    public Image fillImage;
    public Canvas canvas;

    private void LateUpdate()
    {
        if (healthSystem == null || fillImage == null) return;

        float healthPercent = healthSystem.GetHealthPercent();
        fillImage.fillAmount = healthPercent;

        // ✅ Hide when full
        if (canvas != null)
            canvas.enabled = healthPercent < 0.999f;
    }

    public void SetHealthSystem(HealthSystem hs)
    {
        healthSystem = hs;

        // ✅ Immediately update fill & visibility when set
        if (fillImage != null)
            fillImage.fillAmount = hs.GetHealthPercent();

        if (canvas != null)
            canvas.enabled = hs.GetHealthPercent() < 0.999f;
    }
}
