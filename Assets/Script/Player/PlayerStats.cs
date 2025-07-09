using UnityEngine;
using UnityEngine.Rendering;

public class PlayerStats : MonoBehaviour
{
    [Header("Health")]
    private HealthSystem healthSystem;

    [Header("Stamina")]
    private int maxStamina = 100;
    [SerializeField] private float staminaRestoreRate;
    [SerializeField] private float currentStamina;

    [Header("Hunger")]
    private int maxHunger = 100;
    [SerializeField] private float currentHunger;


    void Start()
    {
        healthSystem = new HealthSystem(100);
        currentStamina = maxStamina;
        currentHunger = maxHunger;
       
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
