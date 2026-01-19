using System.Collections;
using System.Collections.Generic;
using Mirror;
using TMPro;
using UnityEngine;

[System.Serializable]
public class CookingSlot
{
    [Tooltip("Transform position where food visual will spawn")]
    public Transform slotPosition;
}

[System.Serializable]
public class FuelItem
{
    public ItemData item;
    [Tooltip("How much fuel this item provides")]
    public float fuelAmount = 1f;
}

public class CookingCampFire : NetworkBehaviour, Iinteractable, IHasCustomText, IHasUI
{
    [Header("Available Recipes")]
    [SerializeField] protected List<CampfireRecipeSO> recipes = new List<CampfireRecipeSO>();

    [Header("Fuel System")]
    [SerializeField] protected bool requiresFuel = false;
    [SerializeField] protected List<FuelItem> acceptedFuels = new List<FuelItem>();
    [SerializeField] protected int maxFuel = 10;
    [SerializeField] protected float fuelBurnRate = 0.1f;

    [Header("Audio")]
    [SerializeField] private AudioSource fireSound;
    [SerializeField] private float audioFadeSpeed = 1.5f;
    private Coroutine audioCoroutine;

    [Header("Cooking Slots")]
    [SerializeField] protected CookingSlot[] cookingSlots = new CookingSlot[4];

    [Header("UI")]
    [SerializeField] private Canvas campfireCanvas;
    [SerializeField] private TextMeshProUGUI fuelText;

    [Header("Visual Effects")]
    [SerializeField] protected ParticleSystem cookingParticles;
    [SerializeField] protected Light flameLight;
    [SerializeField] protected float maxLightIntensity = 3f;
    [SerializeField] protected float lightFadeSpeed = 2f;

    protected float targetLightIntensity = 0f;
    protected Coroutine lightCoroutine;
    protected Coroutine fuelConsumptionRoutine;

    protected enum CookingState { Empty, Cooking, Cooked, Burned }

    // ================= NETWORK STATE =================

    [SyncVar(hook = nameof(OnFuelChanged))]
    protected float currentFuel = 0f;

    protected SyncList<int> slotStatesSync = new SyncList<int>();
    protected SyncList<float> slotTimersSync = new SyncList<float>();
    protected SyncList<int> slotRecipeIndicesSync = new SyncList<int>();

    // Runtime instantiated visuals for each slot
    protected GameObject[] rawVisuals = new GameObject[4];
    protected GameObject[] cookedVisuals = new GameObject[4];
    protected GameObject[] burnedVisuals = new GameObject[4];

    // ================= INITIALIZATION =================

    protected virtual void Awake()
    {
        // Initialize sync lists
        for (int i = 0; i < 4; i++)
        {
            slotStatesSync.Add((int)CookingState.Empty);
            slotTimersSync.Add(0f);
            slotRecipeIndicesSync.Add(-1);
        }

        // Subscribe to sync list changes
        slotStatesSync.Callback += OnSlotStateChanged;
        slotTimersSync.Callback += OnSlotTimerChanged;
    }

    protected virtual void Start()
    {
        if (recipes == null || recipes.Count == 0)
        {
            Debug.LogError($"{gameObject.name}: No recipes assigned!");
        }

        UpdateFireVisuals();

        if (flameLight != null)
        {
            flameLight.intensity = 0f;
        }

        // Initialize all slot visuals
        for (int i = 0; i < 4; i++)
        {
            UpdateSlotVisual(i);
        }
    }

    protected virtual void Update()
    {
        // Only server processes cooking logic
        if (!isServer) return;

        bool isAnyCooking = false;

        for (int i = 0; i < 4; i++)
        {
            CookingState state = (CookingState)slotStatesSync[i];

            if (state == CookingState.Cooking)
            {
                isAnyCooking = true;

                if (requiresFuel && currentFuel <= 0f)
                    continue;

                int recipeIndex = slotRecipeIndicesSync[i];
                if (recipeIndex < 0 || recipeIndex >= recipes.Count) continue;

                CampfireRecipeSO recipe = recipes[recipeIndex];
                if (recipe == null) continue;

                slotTimersSync[i] += Time.deltaTime;

                if (slotTimersSync[i] >= recipe.cookTime)
                {
                    slotStatesSync[i] = (int)CookingState.Cooked;
                    Debug.Log($"[CookingCampFire][SERVER] Slot {i} COOKED at {slotTimersSync[i]:F1}s");
                }
            }
            else if (state == CookingState.Cooked)
            {
                isAnyCooking = true;

                if (requiresFuel && currentFuel <= 0f)
                    continue;

                int recipeIndex = slotRecipeIndicesSync[i];
                if (recipeIndex < 0 || recipeIndex >= recipes.Count) continue;

                CampfireRecipeSO recipe = recipes[recipeIndex];
                if (recipe == null) continue;

                slotTimersSync[i] += Time.deltaTime;

                if (recipe.canBurn)
                {
                    float totalBurnTime = recipe.cookTime + recipe.burnTime;
                    if (slotTimersSync[i] >= totalBurnTime)
                    {
                        slotStatesSync[i] = (int)CookingState.Burned;
                        Debug.Log($"[CookingCampFire][SERVER] Slot {i} BURNED at {slotTimersSync[i]:F1}s");
                    }
                }
            }
        }

        // Burn fuel if any slot is cooking/cooked
        if (requiresFuel && isAnyCooking && currentFuel > 0f)
        {
            currentFuel -= fuelBurnRate * Time.deltaTime;
            if (currentFuel < 0f) currentFuel = 0f;
        }
    }

    protected virtual void OnDestroy()
    {
        if (lightCoroutine != null)
            StopCoroutine(lightCoroutine);

        if (audioCoroutine != null)
            StopCoroutine(audioCoroutine);

        if (fuelConsumptionRoutine != null)
            StopCoroutine(fuelConsumptionRoutine);

        slotStatesSync.Callback -= OnSlotStateChanged;
        slotTimersSync.Callback -= OnSlotTimerChanged;
    }

    // ================= INTERACTION =================

    public virtual void Interact(PlayerHoldingItem interactor)
    {
        if (!interactor || !interactor.isLocalPlayer)
            return;

        Debug.Log($"[CookingCampFire] Interact called by {interactor.name}");

        ItemData heldItemData = interactor.IsHolding() ? interactor.ItemData : null;

        if (heldItemData == null)
        {
            // Not holding anything - try to take ready food
            int readySlot = FindFirstReadySlot();
            if (readySlot != -1)
            {
                CmdTakeFood(readySlot);
            }
            return;
        }

        FuelItem fuelData = GetFuelData(heldItemData);
        bool holdingFuel = requiresFuel && fuelData != null;

        CampfireRecipeSO matchingRecipe = FindRecipeForIngredient(heldItemData);
        bool holdingIngredient = matchingRecipe != null;

        InventoryInput input = SystemManager.Instance.GetComponentInChildren<InventoryInput>();
        if (input == null) return;

        int slotIndex = input.SelectedHotbarIndex;

        if (holdingFuel)
        {
            if (currentFuel < maxFuel)
            {
                CmdRequestAddFuel(slotIndex, fuelData.fuelAmount);
            }
        }
        else if (holdingIngredient)
        {
            int targetSlot = FindEmptySlot();
            if (targetSlot != -1)
            {
                int recipeIndex = recipes.IndexOf(matchingRecipe);
                CmdRequestAddIngredient(slotIndex, targetSlot, recipeIndex);
            }
        }
        else
        {
            int readySlot = FindFirstReadySlot();
            if (readySlot != -1)
            {
                CmdTakeFood(readySlot);
            }
        }
    }

    public void Interact()
    {
        throw new System.NotImplementedException();
    }

    // ================= SERVER COMMANDS =================

    [Command(requiresAuthority = false)]
    private void CmdRequestAddFuel(int slotIndex, float fuelAmount, NetworkConnectionToClient sender = null)
    {
        Debug.Log("[CookingCampFire][SERVER] CmdRequestAddFuel CALLED");

        if (sender == null || sender.identity == null)
            return;

        if (currentFuel >= maxFuel)
            return;

        InventoryData inv = sender.identity.GetComponentInChildren<InventoryData>();
        if (inv == null)
            return;

        if (!ConsumeFromSlot(inv, slotIndex))
            return;

        currentFuel = Mathf.Min(currentFuel + fuelAmount, maxFuel);
        Debug.Log($"[CookingCampFire][SERVER] Fuel added: {currentFuel}/{maxFuel}");
    }

    [Command(requiresAuthority = false)]
    private void CmdRequestAddIngredient(int slotIndex, int targetSlot, int recipeIndex, NetworkConnectionToClient sender = null)
    {
        Debug.Log($"[CookingCampFire][SERVER] CmdRequestAddIngredient CALLED - slot:{targetSlot}, recipe:{recipeIndex}");

        if (sender == null || sender.identity == null)
            return;

        if (targetSlot < 0 || targetSlot >= 4)
            return;

        if ((CookingState)slotStatesSync[targetSlot] != CookingState.Empty)
            return;

        if (recipeIndex < 0 || recipeIndex >= recipes.Count)
            return;

        InventoryData inv = sender.identity.GetComponentInChildren<InventoryData>();
        if (inv == null)
            return;

        if (!ConsumeFromSlot(inv, slotIndex))
            return;

        // Add ingredient to cooking slot
        slotRecipeIndicesSync[targetSlot] = recipeIndex;
        slotStatesSync[targetSlot] = (int)CookingState.Cooking;
        slotTimersSync[targetSlot] = 0f;

        Debug.Log($"[CookingCampFire][SERVER] Ingredient added to slot {targetSlot}");
    }

    [Command(requiresAuthority = false)]
    private void CmdTakeFood(int slotIndex, NetworkConnectionToClient sender = null)
    {
        Debug.Log($"[CookingCampFire][SERVER] CmdTakeFood CALLED - slot:{slotIndex}");

        if (sender == null || sender.identity == null)
            return;

        if (slotIndex < 0 || slotIndex >= 4)
            return;

        CookingState state = (CookingState)slotStatesSync[slotIndex];
        if (state != CookingState.Cooked && state != CookingState.Burned)
            return;

        int recipeIndex = slotRecipeIndicesSync[slotIndex];
        if (recipeIndex < 0 || recipeIndex >= recipes.Count)
            return;

        CampfireRecipeSO recipe = recipes[recipeIndex];
        if (recipe == null)
            return;

        ItemData resultItem = state == CookingState.Cooked ? recipe.cookedResult : recipe.burnedResult;
        if (resultItem == null)
            return;

        InventoryData inv = sender.identity.GetComponentInChildren<InventoryData>();
        if (inv == null)
            return;

        // Try to add to inventory
        bool added = TryAddToInventory(inv, resultItem);

        if (added)
        {
            // Clear the slot
            slotStatesSync[slotIndex] = (int)CookingState.Empty;
            slotTimersSync[slotIndex] = 0f;
            slotRecipeIndicesSync[slotIndex] = -1;

            Debug.Log($"[CookingCampFire][SERVER] Food taken from slot {slotIndex}");
        }
        else
        {
            Debug.Log($"[CookingCampFire][SERVER] Inventory full - cannot take food");
        }
    }

    // ================= SERVER HELPERS =================

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

    private bool TryAddToInventory(InventoryData inv, ItemData item)
    {
        // Try to stack first if item is stackable
        if (item.IsStackable())
        {
            for (int i = 0; i < inv.slots.Count; i++)
            {
                var slot = inv.GetSlot(i);
                if (slot.itemId == item.id && slot.count < item.GetMaxStack())
                {
                    slot.count++;
                    inv.slots[i] = slot;
                    return true;
                }
            }
        }

        // Find empty slot
        for (int i = 0; i < inv.slots.Count; i++)
        {
            var slot = inv.GetSlot(i);
            if (slot.itemId == -1)
            {
                inv.slots[i] = new InventoryData.SlotState { itemId = item.id, count = 1 };
                return true;
            }
        }

        return false;
    }

    // ================= SYNC HOOKS =================

    private void OnFuelChanged(float oldVal, float newVal)
    {
        UpdateFireVisuals();
    }

    private void OnSlotStateChanged(SyncList<int>.Operation op, int index, int oldItem, int newItem)
    {
        if (op == SyncList<int>.Operation.OP_SET)
        {
            UpdateSlotVisual(index);
        }
    }

    private void OnSlotTimerChanged(SyncList<float>.Operation op, int index, float oldItem, float newItem)
    {
        // Timer changes don't need visual updates
    }

    // ================= FUEL MANAGEMENT =================

    protected FuelItem GetFuelData(ItemData item)
    {
        if (!requiresFuel || item == null) return null;

        foreach (FuelItem fuel in acceptedFuels)
        {
            if (fuel.item != null && fuel.item.id == item.id)
            {
                return fuel;
            }
        }
        return null;
    }

    protected void UpdateFireVisuals()
    {
        bool shouldShowFire = !requiresFuel || currentFuel > 0f;

        if (cookingParticles != null)
        {
            if (shouldShowFire && !cookingParticles.isPlaying)
                cookingParticles.Play();
            else if (!shouldShowFire && cookingParticles.isPlaying)
                cookingParticles.Stop();
        }

        if (flameLight != null)
        {
            targetLightIntensity = shouldShowFire ? maxLightIntensity : 0f;

            if (lightCoroutine != null)
                StopCoroutine(lightCoroutine);

            lightCoroutine = StartCoroutine(FadeLightCoroutine());
        }

        if (fireSound != null)
        {
            float targetVolume = shouldShowFire ? 0.6f : 0f;

            if (audioCoroutine != null)
                StopCoroutine(audioCoroutine);

            audioCoroutine = StartCoroutine(FadeAudio(targetVolume));
        }
    }

    protected IEnumerator FadeLightCoroutine()
    {
        if (flameLight == null) yield break;

        float currentIntensity = flameLight.intensity;

        while (Mathf.Abs(currentIntensity - targetLightIntensity) > 0.01f)
        {
            currentIntensity = Mathf.MoveTowards(currentIntensity, targetLightIntensity, lightFadeSpeed * Time.deltaTime);
            flameLight.intensity = currentIntensity;
            yield return null;
        }

        flameLight.intensity = targetLightIntensity;
        lightCoroutine = null;
    }

    IEnumerator FadeAudio(float targetVolume)
    {
        if (fireSound == null) yield break;

        if (!fireSound.isPlaying && targetVolume > 0)
            fireSound.Play();

        while (!Mathf.Approximately(fireSound.volume, targetVolume))
        {
            fireSound.volume = Mathf.MoveTowards(
                fireSound.volume,
                targetVolume,
                audioFadeSpeed * Time.deltaTime
            );
            yield return null;
        }

        if (targetVolume <= 0f)
            fireSound.Stop();
    }

    // ================= RECIPE MANAGEMENT =================

    protected CampfireRecipeSO FindRecipeForIngredient(ItemData item)
    {
        if (item == null) return null;

        foreach (CampfireRecipeSO recipe in recipes)
        {
            if (recipe != null && recipe.CanAcceptIngredient(item))
            {
                return recipe;
            }
        }
        return null;
    }

    protected int FindEmptySlot()
    {
        for (int i = 0; i < 4; i++)
        {
            if ((CookingState)slotStatesSync[i] == CookingState.Empty)
                return i;
        }
        return -1;
    }

    protected int FindFirstReadySlot()
    {
        for (int i = 0; i < 4; i++)
        {
            CookingState state = (CookingState)slotStatesSync[i];
            if (state == CookingState.Cooked || state == CookingState.Burned)
                return i;
        }
        return -1;
    }

    protected int CountSlotsWithState(CookingState state)
    {
        int count = 0;
        for (int i = 0; i < 4; i++)
        {
            if ((CookingState)slotStatesSync[i] == state)
                count++;
        }
        return count;
    }

    // ================= VISUAL UPDATES =================

    protected virtual void UpdateSlotVisual(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= 4) return;
        if (cookingSlots[slotIndex] == null || cookingSlots[slotIndex].slotPosition == null)
            return;

        CookingState state = (CookingState)slotStatesSync[slotIndex];
        int recipeIndex = slotRecipeIndicesSync[slotIndex];

        CleanupSlotVisual(slotIndex);

        if (state == CookingState.Empty)
            return;

        if (recipeIndex < 0 || recipeIndex >= recipes.Count)
            return;

        CampfireRecipeSO recipe = recipes[recipeIndex];
        if (recipe == null)
            return;

        Transform slotTransform = cookingSlots[slotIndex].slotPosition;
        GameObject prefabToInstantiate = null;

        switch (state)
        {
            case CookingState.Cooking:
                prefabToInstantiate = recipe.rawVisualPrefab;
                break;
            case CookingState.Cooked:
                prefabToInstantiate = recipe.cookedVisualPrefab;
                break;
            case CookingState.Burned:
                prefabToInstantiate = recipe.burnedVisualPrefab;
                break;
        }

        if (prefabToInstantiate != null)
        {
            GameObject visual = Instantiate(prefabToInstantiate, slotTransform.position, slotTransform.rotation, slotTransform);
            visual.name = $"{state}Visual_Slot{slotIndex}";

            switch (state)
            {
                case CookingState.Cooking:
                    rawVisuals[slotIndex] = visual;
                    break;
                case CookingState.Cooked:
                    cookedVisuals[slotIndex] = visual;
                    break;
                case CookingState.Burned:
                    burnedVisuals[slotIndex] = visual;
                    break;
            }
        }
    }

    protected void CleanupSlotVisual(int slotIndex)
    {
        if (rawVisuals[slotIndex] != null)
        {
            Destroy(rawVisuals[slotIndex]);
            rawVisuals[slotIndex] = null;
        }

        if (cookedVisuals[slotIndex] != null)
        {
            Destroy(cookedVisuals[slotIndex]);
            cookedVisuals[slotIndex] = null;
        }

        if (burnedVisuals[slotIndex] != null)
        {
            Destroy(burnedVisuals[slotIndex]);
            burnedVisuals[slotIndex] = null;
        }
    }

    // ================= UI =================

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

    private void UpdateFuelUI()
    {
        if (fuelText != null)
        {
            if (requiresFuel)
                fuelText.text = $"Fuel: {Mathf.FloorToInt(currentFuel)}/{maxFuel}";
            else
                fuelText.text = "No Fuel Required";
        }
    }

    // ================= UI TEXT =================

    public virtual string GetInteractText()
    {
        PlayerHoldingItem holdingItem = GameObject.FindGameObjectWithTag("PlayerMovement")?.GetComponent<PlayerHoldingItem>();
        ItemData heldItemData = holdingItem != null && holdingItem.IsHolding() ? holdingItem.ItemData : null;

        FuelItem fuelData = GetFuelData(heldItemData);
        bool holdingFuel = requiresFuel && fuelData != null;

        CampfireRecipeSO matchingRecipe = FindRecipeForIngredient(heldItemData);
        bool holdingIngredient = matchingRecipe != null;

        int emptyCount = CountSlotsWithState(CookingState.Empty);
        int cookingCount = CountSlotsWithState(CookingState.Cooking);
        int cookedCount = CountSlotsWithState(CookingState.Cooked);
        int burnedCount = CountSlotsWithState(CookingState.Burned);
        int readyCount = cookedCount + burnedCount;

        string fuelText = requiresFuel ? $" [Fuel: {Mathf.FloorToInt(currentFuel)}/{maxFuel}]" : "";

        if (holdingFuel)
        {
            if (currentFuel < maxFuel)
                return $"Add {heldItemData.itemName} (+{fuelData.fuelAmount} fuel){fuelText}";
            else
                return $"Fuel Full{fuelText}";
        }
        else if (holdingIngredient)
        {
            if (requiresFuel && currentFuel <= 0f)
                return $"Need fuel to cook!{fuelText}";

            if (emptyCount > 0)
                return $"Cook {matchingRecipe.recipeName} ({4 - emptyCount}/4){fuelText}";
            else
                return $"All slots full{fuelText}";
        }
        else
        {
            if (readyCount > 0)
                return $"Take food ({readyCount} ready){fuelText}";
            else if (cookingCount > 0)
            {
                if (requiresFuel && currentFuel <= 0f)
                    return $"Out of fuel! ({cookingCount} items waiting){fuelText}";
                return $"Cooking... ({cookingCount} items){fuelText}";
            }
            else
                return $"Hold fuel or ingredients{fuelText}";
        }
    }
}