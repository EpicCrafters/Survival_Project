using System.Collections;
using System.Collections.Generic;
using Mirror;
using TMPro;
using UnityEngine;

/// <summary>
/// CampFire ONLINE – Server authoritative
/// - Server giữ fuel + burning state
/// - Client gửi request
/// - Client tự render hiệu ứng
/// </summary>
[System.Serializable]
public struct FuelConfig
{
    public ItemData item;     // loại nhiên liệu
    public int fuelValue;     // cộng bao nhiêu fuel
}

public class CampFire : NetworkBehaviour, Iinteractable, IHasUI
{
    // ================= CONFIG =================

    [Header("Fuel Config")]
    [SerializeField] private int maxFuel = 10;

    [Header("Fuel Inputs")]
    [SerializeField] private List<FuelConfig> acceptedFuels = new();

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
    private Coroutine fuelConsumptionRoutine;
    private float currentLightIntensity;

    // ================= INTERACT =================

    /// <summary>
    /// Được gọi từ PlayerInteract (client)
    /// </summary>
    public void Interact(PlayerHoldingItem interactor)
    {
        if (!interactor || !interactor.isLocalPlayer)
            return;

        if (!interactor.IsHolding())
            return;

        ItemData heldItem = interactor.ItemData;
        if (heldItem == null)
            return;

        if (!TryGetFuelValue(heldItem, out int fuelAdd))
            return;

        InventoryInput input = SystemManager.Instance.GetComponentInChildren<InventoryInput>();
        if (input == null)
            return;

        int slotIndex = input.SelectedHotbarIndex;
        CmdRequestAddFuel(slotIndex, fuelAdd);
    }

    // ================= SERVER =================

    [Command(requiresAuthority = false)]
    private void CmdRequestAddFuel(int slotIndex, int fuelAdd, NetworkConnectionToClient sender = null)
    {
        Debug.Log("[CampFire][SERVER] CmdRequestAddFuel CALLED");
        if (sender == null || sender.identity == null)
            return;

        if (currentFuel >= maxFuel)
            return;

        InventoryData inv = sender.identity.GetComponentInChildren<InventoryData>();
        if (inv == null)
            return;

        if (!ConsumeFromSlot(inv, slotIndex))
            return;

        currentFuel = Mathf.Min(currentFuel + fuelAdd, maxFuel);

        if (!isBurning)
        {
            isBurning = true;
            StartFuelConsumption();
        }
    }

    /// <summary>
    /// SERVER: Bắt đầu tiêu thụ fuel
    /// </summary>
    [Server]
    private void StartFuelConsumption()
    {
        if (fuelConsumptionRoutine != null)
            StopCoroutine(fuelConsumptionRoutine);

        fuelConsumptionRoutine = StartCoroutine(FuelConsumptionRoutine());
    }

    /// <summary>
    /// SERVER: Coroutine giảm fuel theo thời gian
    /// </summary>
    [Server]
    private IEnumerator FuelConsumptionRoutine()
    {
        while (currentFuel > 0)
        {
            yield return new WaitForSeconds(burnTimePerFuel);

            currentFuel--;

            if (currentFuel <= 0)
            {
                isBurning = false;
                fuelConsumptionRoutine = null;
                yield break;
            }
        }
    }

    /// <summary>
    /// SERVER: trừ item từ hotbar
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

    private bool TryGetFuelValue(ItemData item, out int fuelValue)
    {
        foreach (var fuel in acceptedFuels)
        {
            if (fuel.item == item)
            {
                fuelValue = fuel.fuelValue;
                return true;
            }
        }

        fuelValue = 0;
        return false;
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
        currentLightIntensity = 0f;
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

    // ================= CLEANUP =================

    private void OnDestroy()
    {
        if (burnRoutine != null)
            StopCoroutine(burnRoutine);

        if (fuelConsumptionRoutine != null)
            StopCoroutine(fuelConsumptionRoutine);
    }

    // ================= UTIL =================

    private bool IsValidFuel(ItemData item)
    {
        return item != null &&
               item.type == ItemType.Resource &&
               item.id == 7;
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