using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIManager : MonoBehaviour
{
    // Singleton: Đảm bảo trong game chỉ có 1 UIManager tồn tại
    public static UIManager Instance { get; private set; }

    [Header("Inventory UI")]
    [SerializeField] private GameObject mainInventory;

    [Header("Player HUD")]
    [SerializeField] private Image healthFill;          // Thanh máu chính 
    [SerializeField] private Image healthDamageFill;    // Thanh máu phụ 
    [SerializeField] private float healthDelaySpeed = 1.5f; // Tốc độ thanh trắng tụt xuống (mượt hơn)

    [SerializeField] private Image staminaFill;   // Thanh stamina (thể lực)
    [SerializeField] private Image hungerFill;    // Thanh đói

    [Header("Interact UI")]
    [SerializeField] private TextMeshProUGUI eWord; // Hiển thị chữ "E" hoặc thông báo khi có thể tương tác

    [Header("Target Health Bar (Screen UI)")]
    [SerializeField] private GameObject HealthBar; // GameObject chứa thanh máu của mục tiêu (kẻ địch, công trình,...)
    [SerializeField] private Image fillImage;      // Hình ảnh thanh máu của mục tiêu

    [Header("Death UI")]
    [SerializeField] private GameObject playerHUD; // Main HUD to hide when dead

    public HealthBarUI healthBar { get; private set; }

    private float targetHealthFill; // Lưu tỉ lệ máu hiện tại của player (0-1)

    private void Awake()
    {
        // Singleton setup: Nếu đã có UIManager thì huỷ cái mới
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        mainInventory.SetActive(false);
        // Khởi tạo hệ thống thanh máu cho mục tiêu
        healthBar = new HealthBarUI(HealthBar, fillImage);
    }

    // Gắn player vào UI để lắng nghe sự kiện thay đổi stat
    public void HookPlayer(PlayerStatManager player)
    {
        player.OnHealthChanged += UpdateHealthUI;   // Khi máu thay đổi → gọi UpdateHealthUI
        player.OnStaminaChanged += UpdateStaminaUI; // Khi stamina thay đổi → gọi UpdateStaminaUI
        player.OnHungerChanged += UpdateHungerUI;   // Khi hunger thay đổi → gọi UpdateHungerUI

        UpdateHealthUI(player.CurrentHealth, player.MaxHealth);
        UpdateStaminaUI(player.CurrentStamina, player.MaxStamina);
        UpdateHungerUI(player.CurrentHunger, player.MaxHunger);
        if (healthDamageFill != null && healthDamageFill.fillAmount == 0f)
            healthDamageFill.fillAmount = targetHealthFill; // Chỉ khởi tạo lúc đầu game
    }

    // Cập nhật thanh máu player
    private void UpdateHealthUI(int current, int max)
    {
        targetHealthFill = Mathf.Clamp01((float)current / max);

        if (healthFill != null)
            healthFill.fillAmount = targetHealthFill; // Cập nhật thanh đỏ ngay lập tức

    }

    private void LateUpdate()
    {
        if (healthDamageFill == null) return;

        // Điều khiển hiệu ứng thanh máu trắng tụt xuống chậm hơn thanh đỏ
        if (healthDamageFill.fillAmount > targetHealthFill)
        {
            // Khi bị mất máu → thanh trắng tụt dần xuống thanh đỏ 
            healthDamageFill.fillAmount = Mathf.MoveTowards(
                healthDamageFill.fillAmount,
                targetHealthFill,
               healthDelaySpeed * Time.deltaTime
            );
        }
        else if (healthDamageFill.fillAmount < targetHealthFill)
        {
            // Khi hồi máu → thanh trắng nhảy lên ngay bằng thanh đỏ 
            healthDamageFill.fillAmount = targetHealthFill;
        }
    }

    // Cập nhật thanh stamina
    private void UpdateStaminaUI(float current, float max)
    {
        if (staminaFill != null)
            staminaFill.fillAmount = Mathf.Clamp01(current / max);
    }

    // Cập nhật thanh hunger
    private void UpdateHungerUI(float current, float max)
    {
        if (hungerFill != null)
            hungerFill.fillAmount = Mathf.Clamp01(current / max);
    }

    // Hiển thị chữ "E" khi có thể tương tác
    public void ShowInteractUI()
    {
        if (eWord != null)
            eWord.gameObject.SetActive(true);
    }

    // Ẩn chữ "E" khi không còn vật để tương tác
    public void HideInteractUI()
    {
        if (eWord != null)
            eWord.gameObject.SetActive(false);
    }

    // Đổi nội dung chữ "E" → ví dụ "Nhấn E để nhặt"
    public void ChangeInteractText(string newText)
    {
        if (eWord != null)
            eWord.text = newText;
    }

    public void ToggleInventory(bool show)
    {
        if (mainInventory != null)
            mainInventory.SetActive(show);
    }

    /// <summary>
    /// Show/hide the main player HUD
    /// </summary>
    public void SetHUDActive(bool active)
    {
        if (playerHUD != null)
            playerHUD.SetActive(active);
    }

    // Lớp con để quản lý thanh máu của mục tiêu 
    public class HealthBarUI
    {
        private GameObject healthBarObject; // Thanh máu mục tiêu
        private Image fillImage;            // Hình ảnh thanh máu mục tiêu

        public HealthBarUI(GameObject healthBarObject, Image fillImage)
        {
            this.healthBarObject = healthBarObject;
            this.fillImage = fillImage;
        }

        // Hiển thị thanh máu của mục tiêu và cập nhật giá trị
        public void SetTarget(HealthSystem healthSystem)
        {
            if (healthBarObject != null) healthBarObject.SetActive(true);
            Update(healthSystem);
        }

        // Ẩn thanh máu khi không có mục tiêu
        public void ClearTarget()
        {
            if (healthBarObject != null) healthBarObject.SetActive(false);
        }

        // Cập nhật tỉ lệ máu của mục tiêu
        public void Update(HealthSystem healthSystem)
        {
            if (healthSystem != null && fillImage != null)
                fillImage.fillAmount = (float)healthSystem.health / healthSystem.healthMax;
        }

    }
}