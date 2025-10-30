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
    [Header("Hiệu ứng va chạm")]
    public GameObject hitEffectPrefab;
    [Header("Stamina")]
    [SerializeField] private int maxStamina = 100;
    [SerializeField] private float staminaRestoreRate = 5f;
    private float currentStamina;

    [Header("Hunger")]
    [SerializeField] private int maxHunger = 100;
    private float currentHunger;

    [Header("References")]
    [Tooltip("Assign the player's main hurtbox (trigger collider).")]
    public Collider hurtbox;

    private bool isDead = false;
   
    public float CurrentStamina => currentStamina; 
    public float MaxStamina => maxStamina; 
    public float CurrentHunger => currentHunger; 
    public float MaxHunger => maxHunger;
    // Events

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

        if (currentStamina < maxStamina)
        {
            currentStamina += staminaRestoreRate * Time.deltaTime;
            OnStaminaChanged?.Invoke(currentStamina, maxStamina);
        }
    }

    // ==========================================================
    // IDamageable
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
        // Play death animation or ragdoll
    }

    private void OnHealthSync(int oldValue, int newValue)
    {
        OnHealthChanged?.Invoke(newValue, maxHealth);
    }

    public bool CanTriggerHitStop() => false;
    public bool IsDead() => isDead;

    // Debug buttons (for host only)
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
}
