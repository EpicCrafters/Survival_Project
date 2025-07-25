using TMPro;
using UnityEngine;
using System.Collections;

public class CampFire : MonoBehaviour, Iinteractable
{
    [Header("Thiết lập nhiên liệu")]
    [SerializeField] private int maxFuel = 10;
    [SerializeField] private int currentFuel = 0;

    [Header("UI")]
    [SerializeField] private Canvas campfireCanvas;
    [SerializeField] private TextMeshProUGUI fuelText;

    [Header("Hiệu ứng")]
    [SerializeField] private ParticleSystem Fire;
    [SerializeField] private Light flameLight;

    [Header("Cài đặt lửa cháy")]
    [SerializeField] private int burnTimerMax = 5; // thời gian để đốt 1 đơn vị nhiên liệu
    [SerializeField] private int currentLightIntensityMax = 3;

    private float currentLightIntensity = 0f;
    private Coroutine burnCoroutine;
    private PlayerHoldingItem playerHoldingItem;

    private void Awake()
    {
        playerHoldingItem = FindObjectOfType<PlayerHoldingItem>();
        Fire.Stop();
        flameLight.intensity = 0f;
        HideUI();
    }

    // Gọi hàm này khi thêm nhiên liệu
    private void OnFuelChanged()
    {
        UpdateFuelUI();

        // Nếu đang không cháy và có nhiên liệu, bắt đầu cháy
        if (burnCoroutine == null && currentFuel > 0)
        {
            burnCoroutine = StartCoroutine(BurnFuelCoroutine());
        }
    }

    // Coroutine xử lý lửa cháy và tiêu hao nhiên liệu
    private IEnumerator BurnFuelCoroutine()
    {
        Fire.Play();

        while (currentFuel > 0)
        {
            float t = 0f;

            // Dần tăng ánh sáng tới mức tối đa trong thời gian cháy
            while (t < burnTimerMax)
            {
                t += Time.deltaTime;
                currentLightIntensity = Mathf.Lerp(0f, currentLightIntensityMax, t / burnTimerMax);
                flameLight.intensity = currentLightIntensity;

                yield return null;
            }

            currentFuel--;
            UpdateFuelUI();
        }

        // Khi hết nhiên liệu
        Fire.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        // Giảm dần ánh sáng xuống 0
        while (currentLightIntensity > 0f)
        {
            currentLightIntensity = Mathf.Max(currentLightIntensity - Time.deltaTime * 2f, 0f);
            flameLight.intensity = currentLightIntensity;
            yield return null;
        }

        burnCoroutine = null;
    }

    private void UpdateFuelUI()
    {
        if (fuelText != null)
            fuelText.text = $"Fuel: {currentFuel} / {maxFuel}";
    }

    public void ShowUI()
    {
        if (campfireCanvas != null)
            campfireCanvas.enabled = true;
        UpdateFuelUI();
    }

    public void HideUI()
    {
        if (campfireCanvas != null)
            campfireCanvas.enabled = false;
    }

    // Xử lý khi player tương tác
    public void Interact()
    {
        if (playerHoldingItem == null) return;

        GameObject heldObject = playerHoldingItem.GetCurrentHeldObject();
        if (heldObject == null) return;

        Item itemComponent = heldObject.GetComponent<Item>();
        if (itemComponent == null) return;

        ItemData data = itemComponent.itemData;

        if (data.type == ItemType.Resource && data.itemName == "Stick")
        {
            if (currentFuel >= maxFuel)
            {
                Debug.Log("Campfire đã đầy nhiên liệu");
                return;
            }

            bool removed = InventoryManager.instance.RemoveItem(data, 1);
            if (!removed)
            {
                Debug.LogWarning("Không còn Stick để thêm vào lửa");
                return;
            }

            currentFuel++;

            // Cập nhật UI & hiệu ứng cháy
            OnFuelChanged();

            // Làm mới vật phẩm cầm
            int newCount = InventoryManager.instance.GetItemCount(data);
            playerHoldingItem.RefreshHoldingItem(data, newCount);
        }
        else
        {
            Debug.Log("Phải cầm Stick mới có thể đốt lửa");
        }
    }
}
