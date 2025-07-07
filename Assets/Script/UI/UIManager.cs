using UnityEngine;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Health UI")]
    [SerializeField] private HealthBarScreenUI healthBarUI;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void ShowHealthBar(HealthSystem healthSystem)
    {
        healthBarUI.SetTarget(healthSystem);
    }

    public void HideHealthBar()
    {
        healthBarUI.ClearTarget();
    }
}
