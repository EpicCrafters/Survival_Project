using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Player HUD")]
    [SerializeField] private Image healthFill;
    [SerializeField] private Image healthDamageFill;
    [SerializeField] private float healthDelaySpeed = 1.5f;

    [SerializeField] private Image staminaFill;
    [SerializeField] private Image hungerFill;

    [Header("Interact UI")]
    [SerializeField] private TextMeshProUGUI eWord;

    [Header("Target Health Bar (Screen UI)")]
    [SerializeField] private GameObject HealthBar;
    [SerializeField] private Image fillImage;

    public HealthBarUI healthBar { get; private set; }

    private float targetHealthFill;
    private PlayerStatManager hookedPlayer;

    private void Awake()
    {
        // Singleton
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        healthBar = new HealthBarUI(HealthBar, fillImage);
    }

    // Called by PlayerStatManager (local player only)
    public void HookPlayer(PlayerStatManager player)
    {
        if (player == null) return;

        // Unhook previous player if any (useful when respawning)
        UnhookPlayer();

        hookedPlayer = player;
        player.OnHealthChanged += UpdateHealthUI;
        player.OnStaminaChanged += UpdateStaminaUI;
        player.OnHungerChanged += UpdateHungerUI;

        // Initialize UI values immediately
        UpdateHealthUI(player.CurrentHealth, player.MaxHealth);
        UpdateStaminaUI(player.CurrentStamina, player.MaxStamina);
        UpdateHungerUI(player.CurrentHunger, player.MaxHunger);

        if (healthDamageFill != null && healthDamageFill.fillAmount == 0f)
            healthDamageFill.fillAmount = targetHealthFill;
    }

    public void UnhookPlayer()
    {
        if (hookedPlayer == null) return;

        hookedPlayer.OnHealthChanged -= UpdateHealthUI;
        hookedPlayer.OnStaminaChanged -= UpdateStaminaUI;
        hookedPlayer.OnHungerChanged -= UpdateHungerUI;
        hookedPlayer = null;
    }

    // ==========================================================
    // Player HUD
    // ==========================================================
    private void UpdateHealthUI(int current, int max)
    {
        targetHealthFill = Mathf.Clamp01((float)current / max);
        if (healthFill != null)
            healthFill.fillAmount = targetHealthFill;
    }

    private void LateUpdate()
    {
        if (healthDamageFill == null) return;

        // Smooth delayed white bar
        if (healthDamageFill.fillAmount > targetHealthFill)
        {
            healthDamageFill.fillAmount = Mathf.MoveTowards(
                healthDamageFill.fillAmount,
                targetHealthFill,
                healthDelaySpeed * Time.deltaTime
            );
        }
        else if (healthDamageFill.fillAmount < targetHealthFill)
        {
            healthDamageFill.fillAmount = targetHealthFill;
        }
    }

    private void UpdateStaminaUI(float current, float max)
    {
        if (staminaFill != null)
            staminaFill.fillAmount = Mathf.Clamp01(current / max);
    }

    private void UpdateHungerUI(float current, float max)
    {
        if (hungerFill != null)
            hungerFill.fillAmount = Mathf.Clamp01(current / max);
    }

    // ==========================================================
    // Interaction UI
    // ==========================================================
    public void ShowInteractUI() => eWord?.gameObject.SetActive(true);
    public void HideInteractUI() => eWord?.gameObject.SetActive(false);
    public void ChangeInteractText(string newText)
    {
        if (eWord != null)
            eWord.text = newText;
    }

    // ==========================================================
    // Target Health Bar
    // ==========================================================
    public class HealthBarUI
    {
        private GameObject healthBarObject;
        private Image fillImage;

        public HealthBarUI(GameObject healthBarObject, Image fillImage)
        {
            this.healthBarObject = healthBarObject;
            this.fillImage = fillImage;
        }

        public void SetTarget(HealthSystem healthSystem)
        {
            if (healthBarObject != null) healthBarObject.SetActive(true);
            Update(healthSystem);
        }

        public void ClearTarget()
        {
            if (healthBarObject != null) healthBarObject.SetActive(false);
        }

        public void Update(HealthSystem healthSystem)
        {
            if (healthSystem != null && fillImage != null)
                fillImage.fillAmount = (float)healthSystem.health / healthSystem.healthMax;
        }
    }
}
