using System.Collections;
using Mirror;
using TMPro;
using UnityEngine;

/// <summary>
/// CampFire ONLINE – Server authoritative
/// - Server giữ fuel + burning state
/// - Client gửi request
/// - Client tự render hiệu ứng
/// </summary>
public class CampFire : NetworkBehaviour, Iinteractable
{
    // ================= CONFIG =================

    [Header("Fuel Config")]
    [SerializeField] private int maxFuel = 10;

    [Header("Burn Config")]
    [SerializeField] private float burnTimePerFuel = 5f;
    [SerializeField] private float maxLightIntensity = 3f;

    [Header("Visual")]
    [SerializeField] private ParticleSystem fireFx;
    [SerializeField] private Light flameLight;

    [Header("UI")]
    [SerializeField] private Canvas campfireCanvas;
    [SerializeField] private TextMeshProUGUI fuelText;

    // ================= NETWORK STATE =================

    [SyncVar(hook = nameof(OnFuelChanged))]
    private int currentFuel = 0;

    [SyncVar(hook = nameof(OnBurningChanged))]
    private bool isBurning = false;

    // ================= LOCAL =================

    private Coroutine burnRoutine;
    private float currentLightIntensity;

    // ================= INTERACT =================

    /// <summary>
    /// Được gọi từ PlayerInteract (client)
    /// </summary>
    public void Interact(PlayerHoldingItem interactor)
    {
        if (!interactor.isLocalPlayer)
            return;

        if (!interactor.IsHolding())
            return;

        ItemData item = interactor.ItemData;
        if (!IsValidFuel(item))
            return;

        InventoryInput input = SystemManager.Instance.GetComponentInChildren<InventoryInput>();
        if (input == null)
            return;

        int slotIndex = input.SelectedHotbarIndex;

        CmdRequestAddFuel(slotIndex);
    }


    // ================= SERVER =================
    [Command(requiresAuthority = false)]
    private void CmdRequestAddFuel(int slotIndex, NetworkConnectionToClient sender = null)
    {
        if (currentFuel >= maxFuel)
            return;

        if (sender == null || sender.identity == null)
            return;

        InventoryData inv = sender.identity.GetComponentInChildren<InventoryData>();
        if (inv == null)
            return;

        if (!ConsumeFromSlot(inv, slotIndex))
            return;

        currentFuel++;
        if (!isBurning)
            isBurning = true;
    }


    /// <summary>
    /// SERVER: trừ item từ hotbar
    /// (tạm thời client-authoritative, sau có thể siết chặt)
    /// </summary>
    private bool ConsumeFromSlot(InventoryData inv, int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= inv.slots.Count)
            return false;

        var slot = inv.GetSlot(slotIndex);
        if (slot.itemId < 0 || slot.count <= 0)
            return false;

        slot.count--;
        inv.slots[slotIndex] = slot.count > 0
            ? slot
            : new InventoryData.SlotState { itemId = -1, count = 0 };

        return true;
    }


    // ================= SYNC HOOKS =================

    private void OnFuelChanged(int oldVal, int newVal)
    {
        UpdateFuelUI();
    }

    private void OnBurningChanged(bool oldVal, bool newVal)
    {
        if (newVal)
            StartBurnFX();
        else
            StopBurnFX();
    }

    // ================= CLIENT FX =================

    private void StartBurnFX()
    {
        if (burnRoutine != null)
            StopCoroutine(burnRoutine);

        burnRoutine = StartCoroutine(BurnRoutine());
    }

    private void StopBurnFX()
    {
        if (burnRoutine != null)
            StopCoroutine(burnRoutine);

        burnRoutine = null;
        fireFx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        flameLight.intensity = 0f;
    }

    private IEnumerator BurnRoutine()
    {
        fireFx.Play();

        while (isBurning)
        {
            currentLightIntensity = Mathf.Lerp(
                currentLightIntensity,
                maxLightIntensity,
                Time.deltaTime * 2f
            );

            flameLight.intensity = currentLightIntensity;
            yield return null;
        }
    }

    // ================= UTIL =================

    private bool IsValidFuel(ItemData item)
    {
        return item != null &&
               item.type == ItemType.Resource &&
               item.itemName == "Stick";
    }

    private void UpdateFuelUI()
    {
        if (fuelText != null)
            fuelText.text = $"Fuel: {currentFuel}/{maxFuel}";
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

    public void Interact()
    {
        throw new System.NotImplementedException();
    }
}
