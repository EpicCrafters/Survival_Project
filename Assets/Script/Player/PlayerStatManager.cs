using UnityEngine;
using System;

// Quản lý trạng thái của Player: máu, thể lực, đói
public class PlayerStatManager : MonoBehaviour, IDamageable
{
    [Header("Test")]
    public int damageTest;
    public int healTest;


    [Header("Health")]
    [SerializeField] private int maxHealth = 100; // Máu tối đa
    private HealthSystem healthSystem; // Hệ thống quản lý máu

    [Header("Stamina")]
    [SerializeField] private int maxStamina = 100; // Thể lực tối đa
    [SerializeField] private float staminaRestoreRate = 5f; // Tốc độ hồi phục thể lực theo giây
    private float currentStamina; // Thể lực hiện tại

    [Header("Hunger")]
    [SerializeField] private int maxHunger = 100; // Mức đói tối đa
    private float currentHunger; // Mức đói hiện tại

    private bool isDead = false; // Kiểm tra player đã chết chưa

    // Properties để các script khác có thể đọc giá trị
    public int CurrentHealth => healthSystem.GetHealth();
    public int MaxHealth => maxHealth;

    public float CurrentStamina => currentStamina;
    public float MaxStamina => maxStamina;

    public float CurrentHunger => currentHunger;
    public float MaxHunger => maxHunger;

    // Event để UI hoặc các hệ thống khác lắng nghe thay đổi
    public event Action<int, int> OnHealthChanged;
    public event Action<float, float> OnStaminaChanged;
    public event Action<float, float> OnHungerChanged;

    private void Start()
    {
        // Khởi tạo hệ thống máu
        healthSystem = new HealthSystem(maxHealth);
        healthSystem.OnDead += Die; // Khi máu về 0, gọi hàm Die
        healthSystem.OnHealthChanged += (current, max) => OnHealthChanged?.Invoke(current, max);

        // Khởi tạo thể lực và đói
        currentStamina = maxStamina;
        currentHunger = maxHunger;

        // Kết nối UI nếu UIManager tồn tại
        if (UIManager.Instance != null)
            UIManager.Instance.HookPlayer(this);
    }

    private void Update()
    {
        // Hồi phục thể lực theo thời gian
        if (currentStamina < maxStamina)
        {
            currentStamina += staminaRestoreRate * Time.deltaTime;
            OnStaminaChanged?.Invoke(currentStamina, maxStamina);
        }

        // TODO: Có thể thêm giảm mức đói theo thời gian ở đây
    }

    // Hàm nhận sát thương
    public void Damage(int amount)
    {
        if (isDead) return;

        healthSystem.Damage(amount);
        Debug.Log("Player nhận sát thương: " + amount);
    }

    // Hàm hồi máu
    public void Heal(int amount)
    {
        if (isDead) return;

        healthSystem.Heal(amount);
        Debug.Log("Player hồi máu: " + amount);
    }

    // Hàm gọi khi player chết
    private void Die()
    {
        if (isDead) return;
        isDead = true;
        Debug.Log("Player đã chết!");
       
    }

    // ContextMenu giúp test trong Inspector
    [ContextMenu("Damage 10")]
    private void Damage10() => Damage(damageTest);

    [ContextMenu("Heal 10")]
    private void Heal10() => Heal(healTest);
}
