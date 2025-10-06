using UnityEngine;
using System;

public class PlayerStatManager : MonoBehaviour, IDamageable
{
    [Header("Test")]
    public int damageTest;
    public int healTest;

    [Header("Health")]
    [SerializeField] private int maxHealth = 100;
    private HealthSystem healthSystem;

    [Header("Stamina")]
    [SerializeField] private int maxStamina = 100;
    [SerializeField] private float staminaRestoreRate = 5f;
    private float currentStamina;

    [Header("Hunger")]
    [SerializeField] private int maxHunger = 100;
    private float currentHunger;

    private bool isDead = false;

    // Properties
    public int CurrentHealth => healthSystem.GetHealth();
    public int MaxHealth => maxHealth;
    public float CurrentStamina => currentStamina;
    public float MaxStamina => maxStamina;
    public float CurrentHunger => currentHunger;
    public float MaxHunger => maxHunger;

    // Events
    public event Action<int, int> OnHealthChanged;
    public event Action<float, float> OnStaminaChanged;
    public event Action<float, float> OnHungerChanged;

    private void Start()
    {
        healthSystem = new HealthSystem(maxHealth);
        healthSystem.OnDead += Die;
        healthSystem.OnHealthChanged += (current, max) => OnHealthChanged?.Invoke(current, max);

        currentStamina = maxStamina;
        currentHunger = maxHunger;

        if (UIManager.Instance != null)
            UIManager.Instance.HookPlayer(this);
    }

    private void Update()
    {
        if (currentStamina < maxStamina)
        {
            currentStamina += staminaRestoreRate * Time.deltaTime;
            OnStaminaChanged?.Invoke(currentStamina, maxStamina);
        }
    }

    // ==========================================================
    // IDamageable
    // ==========================================================
    public void Damage(int amount)
    {
        // fallback: no hit info
        Damage(amount, new HitInfo(Vector3.zero, Vector3.zero, Vector3.zero, null, null));
    }

    public void Damage(int amount, HitInfo hit)
    {
        if (isDead) return;

        healthSystem.Damage(amount);
        Debug.Log($"Player took {amount} damage at {hit.point}");

        // TODO: Add player hit effect (blood flash, screen shake, etc.)
        // Example: UIManager.Instance?.ShowDamageEffect(hit.point);

        if (healthSystem.GetHealth() <= 0)
        {
            Die();
            return;
        }

        // Player hit reaction
        // Example: play hurt animation
        // animator?.SetTrigger("Hurt");

        // If you want player hit stop when damaged:
        // GetComponent<HitStop>()?.DoHitStop(0.1f);
    }

    public bool CanTriggerHitStop() => false; // Player usually shouldn't cause hit stop when hit
    public bool IsDead() => isDead;

    // ==========================================================
    // Heal
    // ==========================================================
    public void Heal(int amount)
    {
        if (isDead) return;
        healthSystem.Heal(amount);
        Debug.Log($"Player healed {amount}");
    }

    // ==========================================================
    // Death
    // ==========================================================
    private void Die()
    {
        if (isDead) return;
        isDead = true;
        Debug.Log("Player đã chết!");
        // Trigger death animation, game over UI, etc.
    }

    // Debug test buttons
    [ContextMenu("Damage Test")]
    private void DamageTestBtn() => Damage(damageTest);

    [ContextMenu("Heal Test")]
    private void HealTestBtn() => Heal(healTest);
}
