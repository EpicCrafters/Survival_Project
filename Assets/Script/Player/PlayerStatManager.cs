using UnityEngine;
using Mirror;
using System;

public class PlayerStatManager : NetworkBehaviour, IDamageable
{
    [Header("Test")]
    public int damageTest;
    public int healTest;

    [Header("Health")]
    [SerializeField] private int maxHealth = 100;
    private HealthSystem healthSystem;

    [SyncVar(hook = nameof(OnHealthSync))]
    private int currentHealth;
    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;

    [Header("Stamina")]
    [SerializeField] private int maxStamina = 100;
    [SerializeField] private float staminaRestoreRate = 5f;
    [SerializeField] private float staminaRegenDelay = 2f; // delay before regen starts
    [SyncVar(hook = nameof(OnStaminaSync))]
    private float currentStamina;
    private float lastStaminaUseTime;
    public event Action OnStaminaDepleted;

    [Header("Hunger")]
    [SerializeField] private int maxHunger = 100;
    [SerializeField] private float hungerDrainRate = 1f; // drain per tick
    [SerializeField] private float hungerTickInterval = 5f; // seconds between drains
    private float hungerTimer;

    [SyncVar(hook = nameof(OnHungerSync))]
    private float currentHunger;

    [Header("Hunger Effects")]
    [SerializeField] private float starvationDamageInterval = 2f;
    [SerializeField] private int starvationDamage = 1;
    [SerializeField] private float regenInterval = 1.5f;
    [SerializeField] private int regenAmount = 1;

    private float starvationTimer;
    private float regenTimer;

    [Header("FX")]
    public GameObject hitEffectPrefab;

    [Header("References")]
    [Tooltip("Assign the player's main hurtbox (trigger collider).")]
    public Collider hurtbox;

    private bool isDead = false;

    public float CurrentStamina => currentStamina;
    public float MaxStamina => maxStamina;
    public float CurrentHunger => currentHunger;
    public float MaxHunger => maxHunger;

    // ==========================================================
    // EVENTS
    // ==========================================================
    public event Action<int, int> OnHealthChanged;
    public event Action<float, float> OnStaminaChanged;
    public event Action<float, float> OnHungerChanged;

    private void Awake()
    {
        if (hurtbox == null)
        {
            hurtbox = GetComponentInParent<Collider>();
            if (hurtbox != null)
                hurtbox.isTrigger = true;
        }

        if (hurtbox != null && !hurtbox.isTrigger)
            hurtbox.isTrigger = true;
    }

    public override void OnStartServer()
    {
        healthSystem = new HealthSystem(maxHealth);
        currentHealth = maxHealth;
        currentStamina = maxStamina;
        currentHunger = maxHunger;

        healthSystem.OnDead += DieServer;
    }

    public override void OnStartClient()
    {
        if (UIManager.Instance != null && isLocalPlayer)
            UIManager.Instance.HookPlayer(this);
    }

    private void Update()
    {
        if (!isLocalPlayer) return;

        HandleStaminaClient();
    }

    private void FixedUpdate()
    {
        if (isServer)
        {
            HandleHungerServer();
            HandleRegenAndStarvationServer();
        }
    }

    // ==========================================================
    // CLIENT: STAMINA DISPLAY + REGEN DELAY
    // ==========================================================
    private void HandleStaminaClient()
    {
        if (Time.time - lastStaminaUseTime >= staminaRegenDelay)
        {
            if (currentStamina < maxStamina)
            {
                currentStamina = Mathf.Min(maxStamina, currentStamina + staminaRestoreRate * Time.deltaTime);
                OnStaminaChanged?.Invoke(currentStamina, maxStamina);
            }
        }
    }

    // ==========================================================
    // SERVER: HUNGER SYSTEM
    // ==========================================================
    private void HandleHungerServer()
    {
        hungerTimer += Time.fixedDeltaTime;
        if (hungerTimer >= hungerTickInterval)
        {
            hungerTimer = 0f;
            ChangeHunger(-hungerDrainRate);
        }
    }

    private void HandleRegenAndStarvationServer()
    {
        // Regeneration if well fed
        if (currentHunger >= maxHunger * 0.8f && currentHealth < maxHealth)
        {
            regenTimer += Time.fixedDeltaTime;
            if (regenTimer >= regenInterval)
            {
                regenTimer = 0f;
                Heal(regenAmount);
            }
        }
        else
        {
            regenTimer = 0f;
        }

        // Starvation damage if hunger is zero
        if (currentHunger <= 0f && currentHealth > 0)
        {
            starvationTimer += Time.fixedDeltaTime;
            if (starvationTimer >= starvationDamageInterval)
            {
                starvationTimer = 0f;
                Damage(starvationDamage);
            }
        }
        else
        {
            starvationTimer = 0f;
        }
    }

    // ==========================================================
    // NETWORKED STAMINA API
    // ==========================================================
    [Server]

    public void UseStamina(float amount)
    {
        if (isDead) return;
        float previousStamina = currentStamina;
        currentStamina = Mathf.Max(0f, currentStamina - amount);
        lastStaminaUseTime = Time.time;
        OnStaminaSync(currentStamina, currentStamina);

        // Notify when stamina hits zero
        if (previousStamina > 0 && currentStamina <= 0)
        {
            RpcStaminaDepleted();
        }
    }

    [ClientRpc]
    private void RpcStaminaDepleted()
    {
        OnStaminaDepleted?.Invoke();
    }

    [Server]
    public void RestoreStamina(float amount)
    {
        if (isDead) return;
        currentStamina = Mathf.Min(maxStamina, currentStamina + amount);
        OnStaminaSync(currentStamina, currentStamina);
    }

    private void OnStaminaSync(float oldValue, float newValue)
    {
        OnStaminaChanged?.Invoke(newValue, maxStamina);
    }

    // ==========================================================
    // NETWORKED HUNGER API
    // ==========================================================
    [Server]
    public void ChangeHunger(float amount)
    {
        currentHunger = Mathf.Clamp(currentHunger + amount, 0f, maxHunger);
        OnHungerSync(currentHunger, currentHunger);
    }

    private void OnHungerSync(float oldValue, float newValue)
    {
        OnHungerChanged?.Invoke(newValue, maxHunger);
    }

    // ==========================================================
    // DAMAGE & HEAL
    // ==========================================================
    [Server]
    public void Damage(int amount)
    {
        Damage(amount, new HitInfo(Vector3.zero, Vector3.zero, Vector3.zero, null, null));
    }

    [Server]
    public void Damage(int amount, HitInfo hit)
    {
        if (isDead) return;

        currentHealth = Mathf.Max(0, currentHealth - amount);
        OnHealthSync(currentHealth, currentHealth);
        RpcSpawnHitEffect(hit.point, hit.normal);
        if (currentHealth <= 0)
        {
            DieServer();
        }
    }

    [ClientRpc]
    private void RpcSpawnHitEffect(Vector3 pos, Vector3 normal)
    {
        if (hitEffectPrefab != null)
            Instantiate(hitEffectPrefab, pos, Quaternion.LookRotation(normal));
    }

    [Server]
    public void Heal(int amount)
    {
        if (isDead) return;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        OnHealthSync(currentHealth, currentHealth);
    }

    [Server]
    private void DieServer()
    {
        if (isDead) return;
        isDead = true;
        RpcDie();
    }

    [ClientRpc]
    private void RpcDie()
    {
        Debug.Log($"{gameObject.name} đã chết!");
        // TODO: play death animation, disable controls, etc.
    }

    private void OnHealthSync(int oldValue, int newValue)
    {
        OnHealthChanged?.Invoke(newValue, maxHealth);
    }

    public bool CanTriggerHitStop() => false;
    public bool IsDead() => isDead;

    // ==========================================================
    // DEBUG BUTTONS
    // ==========================================================
    [ContextMenu("Damage Test")]
    private void DamageTestBtn()
    {
        if (isServer)
            Damage(damageTest);
    }

    [ContextMenu("Heal Test")]
    private void HealTestBtn()
    {
        if (isServer)
            Heal(healTest);
    }

    [ContextMenu("Use 20 Stamina")]
    private void UseStaminaTest()
    {
        if (isServer)
            UseStamina(20);
    }
}
