using UnityEngine;
using Mirror;
using System;
using Mirror.Examples.Benchmark;
using System.Collections;

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
    [SerializeField] private float hungerDrainRateFullHealth = 0.5f; // Slower drain when at full health
    [SerializeField] private float hungerDrainRateDamaged = 1.5f;    // Faster drain when damaged
    [SerializeField] private float hungerTickInterval = 5f;
    private float hungerTimer;

    [SyncVar(hook = nameof(OnHungerSync))]
    private float currentHunger;

    [Header("Hunger Effects")]
    [SerializeField] private float starvationDamageInterval = 2f;
    [SerializeField] private int starvationDamage = 1;
    [SerializeField] private int minimumHealthFromStarvation = 20;
    [SerializeField] private float regenInterval = 1.5f;
    [SerializeField] private int regenAmount = 1;

    private float starvationTimer;
    private float regenTimer;

    [Header("Ragdoll System")]
    [SerializeField] private PlayerRagdoll playerRagdoll;
    [SerializeField] private Animator animator;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private PlayableAnimationBlender playableAnimationBlender;
    [SerializeField] private PlayerCameraManager playerCameraManager;

    [Header("Screen Damage Effect")]
    [SerializeField] private Material screenDamageMaterial;
    [SerializeField] private float damagedVignetteRadius = 0.3f;
    [SerializeField] private float vignetteRecoveryDelay = 1f;
    [SerializeField] private float vignetteRecoverySpeed = 1f;

    private float currentVignetteRadius = 1f;
    private float vignetteRecoveryTimer = 0f;
    private bool isRecoveringVignette = false;

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

        if (isLocalPlayer && screenDamageMaterial != null)
        {
            currentVignetteRadius = 1f;
            screenDamageMaterial.SetFloat("_Vignette_Radius", 1f);
        }
    }

    public void UpdatePlayerStat(float deltaTime)
    {
        if (!isLocalPlayer) return;

        HandleStaminaClient();
        HandleScreenDamageEffect();
    }

    // ==========================================================
    // SCREEN DAMAGE EFFECT
    // ==========================================================
    private void HandleScreenDamageEffect()
    {
        if (screenDamageMaterial == null) return;

        if (vignetteRecoveryTimer > 0f)
        {
            vignetteRecoveryTimer -= Time.deltaTime;

            if (vignetteRecoveryTimer <= 0f)
            {
                isRecoveringVignette = true;
            }
        }

        if (isRecoveringVignette)
        {
            currentVignetteRadius = Mathf.MoveTowards(
                currentVignetteRadius,
                1f,
                vignetteRecoverySpeed * Time.deltaTime
            );

            screenDamageMaterial.SetFloat("_Vignette_Radius", currentVignetteRadius);

            if (currentVignetteRadius >= 1f)
            {
                isRecoveringVignette = false;
            }
        }
    }

    private void TriggerScreenDamage()
    {
        Debug.Log($"TriggerScreenDamage called! Material null? {screenDamageMaterial == null}");

        if (screenDamageMaterial == null) return;

        currentVignetteRadius = damagedVignetteRadius;
        screenDamageMaterial.SetFloat("_Vignette_Radius", currentVignetteRadius);

        Debug.Log($"Set Vignette_Radius to {currentVignetteRadius}");

        vignetteRecoveryTimer = vignetteRecoveryDelay;
        isRecoveringVignette = false;
    }

    private void FixedUpdate()
    {
        if (isServer)
        {
            HandleHungerServer();
            HandleRegenAndStarvationServer();
        }
    }

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
    // HUNGER - WITH DYNAMIC DRAIN RATE
    // ==========================================================
    private void HandleHungerServer()
    {
        hungerTimer += Time.fixedDeltaTime;
        if (hungerTimer >= hungerTickInterval)
        {
            hungerTimer = 0f;

            // Use slower drain rate when at full health, faster when damaged
            float drainRate = (currentHealth >= maxHealth)
                ? hungerDrainRateFullHealth
                : hungerDrainRateDamaged;

            ChangeHunger(-drainRate);
        }
    }

    private void HandleRegenAndStarvationServer()
    {
        // Health regeneration when hunger is high (80%+)
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

        // Starvation damage when hunger is completely depleted
        if (currentHunger <= 0f && currentHealth > minimumHealthFromStarvation)
        {
            starvationTimer += Time.fixedDeltaTime;
            if (starvationTimer >= starvationDamageInterval)
            {
                starvationTimer = 0f;

                int damageToApply = starvationDamage;
                if (currentHealth - damageToApply < minimumHealthFromStarvation)
                {
                    damageToApply = currentHealth - minimumHealthFromStarvation;
                }

                if (damageToApply > 0)
                {
                    DamageFromStarvation(damageToApply);
                }
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

    /// <summary>
    /// Check if player can eat (hunger is not full)
    /// </summary>
    public bool CanEat()
    {
        return currentHunger < maxHunger;
    }

    private void OnHungerSync(float oldValue, float newValue)
        => OnHungerChanged?.Invoke(newValue, maxHunger);

    // ==========================================================
    // DAMAGE & HEAL - WITH BODY PART SUPPORT
    // ==========================================================

    [Server]
    private void DamageFromStarvation(int amount)
    {
        currentHealth = Mathf.Max(minimumHealthFromStarvation, currentHealth - amount);
        OnHealthSync(currentHealth, currentHealth);

        TargetTriggerScreenDamage(amount);

        Debug.Log($"Starvation damage: {amount}. Health: {currentHealth}/{maxHealth}");
    }

    [Server]
    public void Damage(int amount)
    {
        Damage(amount, new HitInfo(Vector3.zero, Vector3.zero, Vector3.zero, null, null));
    }

    [Server]
    public void Damage(int amount, HitInfo hit)
    {
        if (isDead) return;

        if (playerRagdoll != null && hit.point != Vector3.zero)
        {
            PlayerRagdoll.BodyPartType hitBodyPart = playerRagdoll.GetBodyPartTypeFromHitPoint(hit.point, 1f);
            var bodyPart = playerRagdoll.bodyParts.Find(x => x.type == hitBodyPart);

            if (bodyPart != null)
            {
                int modifiedDamage = Mathf.RoundToInt(amount * bodyPart.damageMultiplier);
                Debug.Log($"🎯 PlayerMovement hit on {bodyPart.name}! {amount} → {modifiedDamage} (x{bodyPart.damageMultiplier})");
                amount = modifiedDamage;
            }
        }

        currentHealth = Mathf.Max(0, currentHealth - amount);
        OnHealthSync(currentHealth, currentHealth);
        playerCameraManager.ShakeCamera(0.5f, 1f, 0.2f);
        TargetTriggerScreenDamage(amount);
        RpcPlayHitReaction();

        if (currentHealth <= 0)
        {
            DieServer(hit);
        }
    }

    [TargetRpc]
    private void TargetTriggerScreenDamage(int damageAmount)
    {
        Debug.Log($"TargetRpc received! Damage: {damageAmount}, Material: {screenDamageMaterial != null}");
        TriggerScreenDamage();
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

            if (hit.point != Vector3.zero && hit.itemData != null)
            {
                ApplyDeathKnockback(hit);
            }
        }

        RpcDie();
    }

    private void ApplyDeathKnockback(HitInfo hit)
    {
        float horizontalForce = 10f;
        float searchRadius = 1f;

        if (hit.itemData != null)
        {
            KnockbackSettings kb = hit.itemData.GetKnockbackSettings();
            horizontalForce = kb.horizontalForce;
            searchRadius = kb.boneSearchRadius;
        }

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
    private void RpcDie()
    {
        isDead = true;

        if (isLocalPlayer)
        {
            var movement = GetComponent<Mirror.Examples.Benchmark.PlayerMovement>();
            if (movement != null)
                movement.enabled = false;

            if (UIManager.Instance != null)
                UIManager.Instance.SetHUDActive(false);

            if (DeathUIManager.Instance != null)
                DeathUIManager.Instance.ShowDeathScreen(this);
        }

        if (animator != null)
            animator.enabled = false;

        if (playerRagdoll != null)
            playerRagdoll.SetRagdoll(true);

        Debug.Log($"{gameObject.name} client died");
    }

    private void OnHealthSync(int oldValue, int newValue)
    {
        OnHealthChanged?.Invoke(newValue, maxHealth);
    }

    // ==========================================================
    // IDamageable IMPLEMENTATION
    // ==========================================================
    public bool CanTriggerHitStop() => false;
    public bool IsDead() => isDead;

    // ==========================================================
    // RESPAWN SYSTEM
    // ==========================================================

    [Command]
    public void RequestRespawn()
    {
        if (!isDead) return;

        Vector3 spawnPosition = Vector3.zero;
        if (DeathUIManager.Instance != null)
        {
            spawnPosition = DeathUIManager.Instance.GetSpawnPoint();
        }

        Respawn(spawnPosition);
    }

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

        if (isLocalPlayer && screenDamageMaterial != null)
        {
            currentVignetteRadius = 1f;
            screenDamageMaterial.SetFloat("_Vignette_Radius", 1f);
        }

        if (playerRagdoll != null)
            playerRagdoll.SetRagdoll(false);

        StartCoroutine(RespawnAnimationSystemReset());
    }

    private IEnumerator RespawnAnimationSystemReset()
    {
        if (playableAnimationBlender != null)
        {
            playableAnimationBlender.enabled = false;
        }

        if (animator != null)
        {
            animator.enabled = false;
        }

        yield return null;
        yield return null;

        if (animator != null)
        {
            animator.enabled = true;
            animator.Rebind();
        }

        yield return null;

        var playerAnimator = GetComponent<PlayerAnimator>();
        if (playerAnimator != null)
        {
            playerAnimator.ResetAnimatorState();
        }

        yield return null;

        if (playableAnimationBlender != null)
        {
            playableAnimationBlender.ResetSystem();
            playableAnimationBlender.enabled = true;
            playableAnimationBlender.Reinitialize();
        }

        yield return null;

        if (isLocalPlayer)
        {
            var movement = GetComponent<Mirror.Examples.Benchmark.PlayerMovement>();
            if (movement != null)
                movement.enabled = true;

            if (UIManager.Instance != null)
                UIManager.Instance.SetHUDActive(true);

            if (DeathUIManager.Instance != null)
                DeathUIManager.Instance.HideDeathScreen();
        }

        Debug.Log($"{gameObject.name} respawned - animation system fully reset");
    }

    // ==========================================================
    // DEBUG
    // ==========================================================
    [ContextMenu("Damage Test")]
    private void DamageTestBtn()
    {
        if (isServer)
            Damage(damageTest);
        else
            TriggerScreenDamage();
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

    [ContextMenu("Kill PlayerMovement")]
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

    [ContextMenu("Drain Hunger Completely")]
    private void DrainHungerTest()
    {
        if (isServer)
            ChangeHunger(-currentHunger);
    }
}