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

        fillImage.fillAmount = healthSystem.GetHealthPercent();

        if (canvas != null)
            canvas.enabled = healthSystem.GetHealthPercent() < 1f;

       
    }

    public void SetHealthSystem(HealthSystem hs)
    {
        healthSystem = hs;
    }



  
}
