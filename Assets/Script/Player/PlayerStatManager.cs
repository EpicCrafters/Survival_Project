using UnityEngine;
using Mirror;
using System;
using Mirror.Examples.Benchmark;

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
    [SerializeField] private float staminaRegenDelay = 2f;
    [SyncVar(hook = nameof(OnStaminaSync))]
    private float currentStamina;
    private float lastStaminaUseTime;
    public event Action OnStaminaDepleted;

    [Header("Hunger")]
    [SerializeField] private int maxHunger = 100;
    [SerializeField] private float hungerDrainRate = 1f;
    [SerializeField] private float hungerTickInterval = 5f;
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

    [Header("Ragdoll System")]
    [SerializeField] private PlayerRagdoll playerRagdoll;
    [SerializeField] private Animator animator;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private  PlayableAnimationBlender playableAnimationBlender;

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
        // Get references
        if (playerRagdoll == null)
            playerRagdoll = GetComponent<PlayerRagdoll>();

        if (animator == null)
            animator = GetComponent<Animator>();

        if (characterController == null)
            characterController = GetComponent<CharacterController>();
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
    // STAMINA
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

    [Server]
    public void UseStamina(float amount)
    {
        if (isDead) return;
        float previousStamina = currentStamina;
        currentStamina = Mathf.Max(0f, currentStamina - amount);
        lastStaminaUseTime = Time.time;
        OnStaminaSync(currentStamina, currentStamina);

        if (previousStamina > 0 && currentStamina <= 0)
            RpcStaminaDepleted();
    }

    [ClientRpc]
    private void RpcStaminaDepleted() => OnStaminaDepleted?.Invoke();

    [Server]
    public void RestoreStamina(float amount)
    {
        if (isDead) return;
        currentStamina = Mathf.Min(maxStamina, currentStamina + amount);
        OnStaminaSync(currentStamina, currentStamina);
    }

    private void OnStaminaSync(float oldValue, float newValue)
        => OnStaminaChanged?.Invoke(newValue, maxStamina);

    // ==========================================================
    // HUNGER
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
            regenTimer = 0f;

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
            starvationTimer = 0f;
    }

    [Server]
    public void ChangeHunger(float amount)
    {
        currentHunger = Mathf.Clamp(currentHunger + amount, 0f, maxHunger);
        OnHungerSync(currentHunger, currentHunger);
    }

    private void OnHungerSync(float oldValue, float newValue)
        => OnHungerChanged?.Invoke(newValue, maxHunger);

    // ==========================================================
    // DAMAGE & HEAL - WITH BODY PART SUPPORT
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

        // Detect which body part was hit and apply damage multiplier
        if (playerRagdoll != null && hit.point != Vector3.zero)
        {
            PlayerRagdoll.BodyPartType hitBodyPart = playerRagdoll.GetBodyPartTypeFromHitPoint(hit.point, 1f);
            var bodyPart = playerRagdoll.bodyParts.Find(x => x.type == hitBodyPart);

            if (bodyPart != null)
            {
                int modifiedDamage = Mathf.RoundToInt(amount * bodyPart.damageMultiplier);
                Debug.Log($"🎯 Player hit on {bodyPart.name}! {amount} → {modifiedDamage} (x{bodyPart.damageMultiplier})");
                amount = modifiedDamage;
            }
        }

        currentHealth = Mathf.Max(0, currentHealth - amount);
        OnHealthSync(currentHealth, currentHealth);

        RpcPlayHitReaction();

        if (currentHealth <= 0)
        {
            //DieServer(hit);
        }
    }

    [ClientRpc]
    private void RpcPlayHitReaction()
    {
        if (animator != null && !isDead)
            animator.SetTrigger("Hit");
    }

    [Server]
    public void Heal(int amount)
    {
        if (isDead) return;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        OnHealthSync(currentHealth, currentHealth);
    }

    // ==========================================================
    // DEATH SYSTEM - WITH RAGDOLL & KNOCKBACK
    // ==========================================================
    [Server]
    private void DieServer()
    {
        DieServer(new HitInfo(Vector3.zero, Vector3.zero, Vector3.zero, null, null));
    }

    [Server]
    private void DieServer(HitInfo hit)
    {
        if (isDead) return;
        isDead = true;

        Debug.Log($"{gameObject.name} died!");

        if (playerRagdoll != null)
        {
            playerRagdoll.SetRagdoll(true);

            // Apply death knockback if we have hit info
            if (hit.point != Vector3.zero && hit.itemData != null)
            {
                ApplyDeathKnockback(hit);
            }
        }

        //RpcDie(hit);
    }

    private void ApplyDeathKnockback(HitInfo hit)
    {
        // Get knockback settings from weapon
        float horizontalForce = 10f;
        float searchRadius = 1f;

        if (hit.itemData != null)
        {
            KnockbackSettings kb = hit.itemData.GetKnockbackSettings();
            horizontalForce = kb.horizontalForce;
            searchRadius = kb.boneSearchRadius;
        }

        // Find the body part that was hit
        Rigidbody hitBodyPart = playerRagdoll.GetClosestBodyPart(hit.point, searchRadius);

        if (hitBodyPart != null)
        {
            Vector3 forceDirection = hit.direction.normalized;
            Vector3 force = new Vector3(forceDirection.x, 0, forceDirection.z).normalized * horizontalForce;

            RpcApplyDeathKnockback(hitBodyPart.name, hit.point, force);
        }
    }

    [ClientRpc]
    private void RpcApplyDeathKnockback(string bodyPartName, Vector3 hitPoint, Vector3 force)
    {
        if (playerRagdoll == null) return;

        Rigidbody targetBodyPart = null;
        foreach (var part in playerRagdoll.bodyParts)
        {
            if (part.rigidbody != null && part.rigidbody.name == bodyPartName)
            {
                targetBodyPart = part.rigidbody;
                break;
            }
        }

        if (targetBodyPart != null)
        {
            targetBodyPart.AddForceAtPosition(force, hitPoint, ForceMode.Impulse);
            Debug.Log($"Applied death knockback to {bodyPartName}");
        }
    }

    [ClientRpc]
    //private void RpcDie(HitInfo hit)
    //{
    //    isDead = true;

    //    // Disable movement and CharacterController
      

    //    // Disable animator
        
    //    // Enable ragdoll (this also disables CharacterController)
    //    if (playerRagdoll != null)
    //        playerRagdoll.SetRagdoll(true);

      

    //    Debug.Log($"{gameObject.name} client died");
    //}

    private void OnHealthSync(int oldValue, int newValue)
        => OnHealthChanged?.Invoke(newValue, maxHealth);

    // ==========================================================
    // IDamageable IMPLEMENTATION
    // ==========================================================
    public bool CanTriggerHitStop() => false;
    public bool IsDead() => isDead;

    // ==========================================================
    // RESPAWN
    // ==========================================================
    [Server]
    public void Respawn(Vector3 spawnPosition)
    {
        if (!isDead) return;

        isDead = false;
        currentHealth = maxHealth;
        currentStamina = maxStamina;
        currentHunger = maxHunger;

        OnHealthSync(currentHealth, currentHealth);
        OnStaminaSync(currentStamina, currentStamina);
        OnHungerSync(currentHunger, currentHunger);

        transform.position = spawnPosition;

        if (playerRagdoll != null)
            playerRagdoll.SetRagdoll(false);

        RpcRespawn();
    }

    [ClientRpc]
    private void RpcRespawn()
    {
        isDead = false;

        // Re-enable movement
        if (isLocalPlayer)
        {
            var movement = GetComponent<PlayerMovement>();
            if (movement != null)
                movement.enabled = true;
        }

        // Re-enable animator
        if (animator != null)
            animator.enabled = true;

        // Disable ragdoll (this also re-enables CharacterController)
        if (playerRagdoll != null)
            playerRagdoll.SetRagdoll(false);

        Debug.Log($"{gameObject.name} respawned");
    }

    // ==========================================================
    // DEBUG
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

    [ContextMenu("Kill Player")]
    private void KillPlayerTest()
    {
        if (isServer)
        {
            HitInfo testHit = new HitInfo(
                transform.position + transform.forward,
                -transform.forward,
                transform.forward,
                gameObject,
                null
            );
            DieServer(testHit);
        }
    }
}