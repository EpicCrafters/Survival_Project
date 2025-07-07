using UnityEngine;
using UnityEngine.UI;

public class HealthBarScreenUI : MonoBehaviour
{
    [SerializeField] private Image fillImage;
    private HealthSystem targetHealth;

    public void SetTarget(HealthSystem healthSystem)
    {
        targetHealth = healthSystem;
        gameObject.SetActive(true);
        UpdateBar(); 
    }

    public void ClearTarget()
    {
        targetHealth = null;
        gameObject.SetActive(false);
    }

    private void Update()
    {
        if (targetHealth != null)
        {
            fillImage.fillAmount = (float)targetHealth.health / targetHealth.healthMax;
        }
    }

    private void UpdateBar()
    {
        fillImage.fillAmount = (float)targetHealth.health / targetHealth.healthMax;
    }
}
