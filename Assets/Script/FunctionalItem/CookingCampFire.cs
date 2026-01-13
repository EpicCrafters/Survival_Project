using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CookingCampFire : MonoBehaviour, Iinteractable, IHasCustomText
{
    [Header("Available Recipes")]
    [SerializeField] protected List<CampfireRecipeSO> recipes = new List<CampfireRecipeSO>();

    [Header("Fuel System")]
    [SerializeField] protected bool requiresFuel = false;
    [SerializeField] protected List<FuelItem> acceptedFuels = new List<FuelItem>();
    [SerializeField] protected int maxFuel = 10;
    [SerializeField] protected float fuelBurnRate = 0.1f;
    protected float currentFuel = 0f;


    [Header("Audio")]
    [SerializeField] private AudioSource fireSound;
    [SerializeField] private float audioFadeSpeed = 1.5f;
    private Coroutine audioCoroutine;

    [Header("Cooking Slots")]
    [SerializeField] protected CookingSlot[] cookingSlots = new CookingSlot[4];

    [Header("Visual Effects")]
    [SerializeField] protected ParticleSystem cookingParticles;
    [SerializeField] protected Light flameLight;
    [SerializeField] protected float maxLightIntensity = 3f;
    [SerializeField] protected float lightFadeSpeed = 2f;

    protected float targetLightIntensity = 0f;
    protected Coroutine lightCoroutine;

    protected enum CookingState { Empty, Cooking, Cooked, Burned }

    protected CookingState[] slotStates = new CookingState[4];
    protected float[] slotTimers = new float[4];
    protected int[] slotRecipeIndices = new int[4];

    // Runtime instantiated visuals for each slot
    protected GameObject[] rawVisuals = new GameObject[4];
    protected GameObject[] cookedVisuals = new GameObject[4];
    protected GameObject[] burnedVisuals = new GameObject[4];

    protected virtual void Start()
    {
        // Initialize arrays
        for (int i = 0; i < 4; i++)
        {
            slotStates[i] = CookingState.Empty;
            slotTimers[i] = 0f;
            slotRecipeIndices[i] = -1;
        }

        if (recipes == null || recipes.Count == 0)
        {
            Debug.LogError($"{gameObject.name}: No recipes assigned!");
        }

        Debug.Log($"[CookingCampFire] START - requiresFuel: {requiresFuel}");
        Debug.Log($"[CookingCampFire] acceptedFuels count: {acceptedFuels.Count}");
        for (int i = 0; i < acceptedFuels.Count; i++)
        {
            if (acceptedFuels[i].item != null)
            {
                Debug.Log($"[CookingCampFire] Fuel #{i}: {acceptedFuels[i].item.itemName} (ID: {acceptedFuels[i].item.id}) = {acceptedFuels[i].fuelAmount} fuel");
            }
        }

        UpdateFireVisuals();

        // Initialize light
        if (flameLight != null)
        {
            flameLight.intensity = 0f;
        }
    }

    protected virtual void Update()
    {
        bool isAnyCooking = false;

        // Duyệt qua tất cả 4 slot nấu ăn
        for (int i = 0; i < 4; i++)
        {
            // ========================================
            // XỬ LÝ TRẠNG THÁI ĐANG NẤU (COOKING)
            // ========================================
            if (slotStates[i] == CookingState.Cooking)
            {
                isAnyCooking = true;

                // Kiểm tra nhiên liệu - nếu cần nhiên liệu và hết nhiên liệu thì bỏ qua
                if (requiresFuel && currentFuel <= 0f)
                {
                    continue;
                }

                int recipeIndex = slotRecipeIndices[i];
                if (recipeIndex < 0 || recipeIndex >= recipes.Count) continue;

                CampfireRecipeSO recipe = recipes[recipeIndex];
                if (recipe == null) continue;

                // Tăng bộ đếm thời gian
                slotTimers[i] += Time.deltaTime;

                // Kiểm tra xem đã đến thời gian chín chưa
                if (slotTimers[i] >= recipe.cookTime)
                {
                    slotStates[i] = CookingState.Cooked;
                    UpdateSlotVisual(i);
                    Debug.Log($"[CookingCampFire] Slot {i} ĐÃ CHÍN tại {slotTimers[i]:F1}s");
                }
            }
            // ========================================
            // XỬ LÝ TRẠNG THÁI ĐÃ CHÍN (COOKED)
            // ========================================
            else if (slotStates[i] == CookingState.Cooked)
            {
                isAnyCooking = true;

                // Kiểm tra nhiên liệu - nếu cần nhiên liệu và hết nhiên liệu thì bỏ qua
                if (requiresFuel && currentFuel <= 0f)
                {
                    continue;
                }

                int recipeIndex = slotRecipeIndices[i];
                if (recipeIndex < 0 || recipeIndex >= recipes.Count) continue;

                CampfireRecipeSO recipe = recipes[recipeIndex];
                if (recipe == null) continue;

                // Tiếp tục tăng bộ đếm thời gian (để kiểm tra cháy)
                slotTimers[i] += Time.deltaTime;

                // Kiểm tra xem có thể bị cháy không (chỉ khi canBurn = true)
                if (recipe.canBurn)
                {
                    // Tính tổng thời gian = thời gian nấu + thời gian chờ đến khi cháy
                    float totalBurnTime = recipe.cookTime + recipe.burnTime;
                    if (slotTimers[i] >= totalBurnTime)
                    {
                        slotStates[i] = CookingState.Burned;
                        UpdateSlotVisual(i);
                        Debug.Log($"[CookingCampFire] Slot {i} ĐÃ CHÁY tại {slotTimers[i]:F1}s (chín lúc {recipe.cookTime}s + delay {recipe.burnTime}s)");
                    }
                }
            }
        }

        // Đốt nhiên liệu nếu có bất kỳ slot nào đang nấu/chín và cần nhiên liệu
        if (requiresFuel && isAnyCooking && currentFuel > 0f)
        {
            currentFuel -= fuelBurnRate * Time.deltaTime;
            if (currentFuel < 0f) currentFuel = 0f;
        }
    }

    protected virtual void OnDestroy()
    {
        if (lightCoroutine != null)
        {
            StopCoroutine(lightCoroutine);
        }
    }

    // ========================================== 
    // INTERACTION - NON-NETWORKED
    // ==========================================

    public virtual void Interact()
    {
        Debug.Log($"[CookingCampFire] ========== INTERACT CALLED ==========");

        // Get held item data directly from PlayerHoldingItem
        ItemData heldItemData = GetHeldItemData();
        Debug.Log($"[CookingCampFire] Held ItemData: {(heldItemData != null ? heldItemData.itemName : "NULL")}");

        // ✅ VERIFY player is actually holding the item in their hands
        if (heldItemData == null)
        {
            Debug.Log("[CookingCampFire] Not holding anything - checking for ready food");
            int readySlot = FindFirstReadySlot();
            if (readySlot != -1)
            {
                TakeFood(readySlot);
            }
            else
            {
                Debug.Log("Hold an item to interact with the campfire!");
            }
            return;
        }

        FuelItem fuelData = GetFuelData(heldItemData);
        bool holdingFuel = requiresFuel && fuelData != null;
        Debug.Log($"[CookingCampFire] Holding fuel: {holdingFuel}");

        CampfireRecipeSO matchingRecipe = FindRecipeForIngredient(heldItemData);
        bool holdingIngredient = matchingRecipe != null;
        Debug.Log($"[CookingCampFire] Holding ingredient: {holdingIngredient}");

        if (holdingFuel)
        {
            Debug.Log($"[CookingCampFire] FUEL BRANCH - Current fuel: {currentFuel}/{maxFuel}");
            if (currentFuel < maxFuel)
            {
                if (InventoryManager.instance != null)
                {
                    bool removed = InventoryManager.instance.RemoveItem(heldItemData, 1);
                    Debug.Log($"[CookingCampFire] Item removed from inventory: {removed}");

                    if (removed)
                    {
                        // ADD FUEL DIRECTLY
                        float oldFuel = currentFuel;
                        currentFuel += fuelData.fuelAmount;
                        if (currentFuel > maxFuel) currentFuel = maxFuel;
                        Debug.Log($"[CookingCampFire] ✅ Fuel added! {oldFuel} -> {currentFuel}");

                        // Update visuals
                        UpdateFireVisuals();

                        // Refresh held item
                        int remainingCount = InventoryManager.instance.GetItemCount(heldItemData);
                        Debug.Log($"[CookingCampFire] Remaining item count: {remainingCount}");

                        if (remainingCount <= 0)
                        {
                            ClearPlayerHeldItem();
                        }
                        else
                        {
                            // Refresh the held item visual
                            PlayerHoldingItem holdingItem = GameObject.FindGameObjectWithTag("Player")?.GetComponent<PlayerHoldingItem>();
                            if (holdingItem != null)
                            {
                                holdingItem.RefreshHoldingItem(heldItemData, remainingCount);
                            }
                        }
                    }
                }
                else
                {
                    Debug.LogError("[CookingCampFire] InventoryManager.instance is NULL!");
                }
            }
            else
            {
                Debug.Log("[CookingCampFire] Fuel is already full!");
            }
        }
        else if (holdingIngredient)
        {
           

            int targetSlot = FindEmptySlot();
            if (targetSlot != -1)
            {
                if (InventoryManager.instance != null)
                {
                    bool removed = InventoryManager.instance.RemoveItem(heldItemData, 1);
                    if (removed)
                    {
                        int recipeIndex = recipes.IndexOf(matchingRecipe);
                        AddIngredient(targetSlot, recipeIndex);

                        int remainingCount = InventoryManager.instance.GetItemCount(heldItemData);
                        if (remainingCount <= 0)
                        {
                            ClearPlayerHeldItem();
                        }
                        else
                        {
                            PlayerHoldingItem holdingItem = GameObject.FindGameObjectWithTag("Player")?.GetComponent<PlayerHoldingItem>();
                            if (holdingItem != null)
                            {
                                holdingItem.RefreshHoldingItem(heldItemData, remainingCount);
                            }
                        }
                    }
                }
            }
            else
            {
                Debug.Log("All cooking slots are full!");
            }
        }
        else
        {
            // Not holding fuel or ingredient - try to take ready food
            int readySlot = FindFirstReadySlot();
            if (readySlot != -1)
            {
                TakeFood(readySlot);
            }
            else
            {
                Debug.Log("Hold fuel or ingredients to add to campfire!");
            }
        }

        Debug.Log($"[CookingCampFire] ========== INTERACT END ==========");
    }

    public virtual string GetInteractText()
    {
        // Get held item data directly from PlayerHoldingItem
        ItemData heldItemData = GetHeldItemData();

        // Check what player is currently holding
        FuelItem fuelData = GetFuelData(heldItemData);
        bool holdingFuel = requiresFuel && fuelData != null;

        CampfireRecipeSO matchingRecipe = FindRecipeForIngredient(heldItemData);
        bool holdingIngredient = matchingRecipe != null;

        // Count slot states
        int emptyCount = CountSlotsWithState(CookingState.Empty);
        int cookingCount = CountSlotsWithState(CookingState.Cooking);
        int cookedCount = CountSlotsWithState(CookingState.Cooked);
        int burnedCount = CountSlotsWithState(CookingState.Burned);
        int readyCount = cookedCount + burnedCount;

        string fuelText = requiresFuel ? $" [Fuel: {Mathf.FloorToInt(currentFuel)}/{maxFuel}]" : "";

        // ✅ PRIORITY: Show what the HELD item can do
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
            // Not holding anything valid
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

    // ========================================== 
    // FUEL MANAGEMENT
    // ==========================================

    protected FuelItem GetFuelData(ItemData item)
    {
        if (!requiresFuel || item == null) return null;

        Debug.Log($"[CookingCampFire] GetFuelData: Checking {item.itemName} (ID: {item.id})");

        foreach (FuelItem fuel in acceptedFuels)
        {
            if (fuel.item != null)
            {
                Debug.Log($"[CookingCampFire] GetFuelData: Compare with {fuel.item.itemName} (ID: {fuel.item.id})");
                if (fuel.item.id == item.id)
                {
                    Debug.Log($"[CookingCampFire] GetFuelData: ✅ MATCH! Returns {fuel.fuelAmount}");
                    return fuel;
                }
            }
        }

        Debug.Log($"[CookingCampFire] GetFuelData: ❌ No match");
        return null;
    }

    protected void UpdateFireVisuals()
    {
        bool shouldShowFire = !requiresFuel || currentFuel > 0f;

        // Handle particle system
        if (cookingParticles != null)
        {
            if (shouldShowFire && !cookingParticles.isPlaying)
            {
                cookingParticles.Play();
            }
            else if (!shouldShowFire && cookingParticles.isPlaying)
            {
                cookingParticles.Stop();
            }
        }

        // Handle light - smooth transition
        if (flameLight != null)
        {
            targetLightIntensity = shouldShowFire ? maxLightIntensity : 0f;

            if (lightCoroutine != null)
            {
                StopCoroutine(lightCoroutine);
            }
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

        // Fade light smoothly to target
        while (Mathf.Abs(currentIntensity - targetLightIntensity) > 0.01f)
        {
            currentIntensity = Mathf.MoveTowards(currentIntensity, targetLightIntensity, lightFadeSpeed * Time.deltaTime);
            flameLight.intensity = currentIntensity;
            yield return null;
        }

        flameLight.intensity = targetLightIntensity;
        lightCoroutine = null;
    }

    // ========================================== 
    // RECIPE & INGREDIENT MANAGEMENT
    // ==========================================

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
            if (slotStates[i] == CookingState.Empty) return i;
        }
        return -1;
    }

    protected void AddIngredient(int slotIndex, int recipeIndex)
    {
        if (slotIndex < 0 || slotIndex >= 4) return;
        if (slotStates[slotIndex] != CookingState.Empty) return;
        if (recipeIndex < 0 || recipeIndex >= recipes.Count) return;

        CampfireRecipeSO recipe = recipes[recipeIndex];
        if (recipe == null) return;

        slotRecipeIndices[slotIndex] = recipeIndex;
        slotStates[slotIndex] = CookingState.Cooking;
        slotTimers[slotIndex] = 0f;

        UpdateSlotVisual(slotIndex);
    }

    protected void TakeFood(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= 4) return;

        CookingState state = slotStates[slotIndex];
        if (state != CookingState.Cooked && state != CookingState.Burned) return;

        int recipeIndex = slotRecipeIndices[slotIndex];
        if (recipeIndex < 0 || recipeIndex >= recipes.Count) return;

        CampfireRecipeSO recipe = recipes[recipeIndex];
        if (recipe == null) return;

        ItemData resultItem = state == CookingState.Cooked ? recipe.cookedResult : recipe.burnedResult;
        if (resultItem == null) return;

        // Try to add to inventory first
        if (InventoryManager.instance != null && InventoryManager.instance.AddItem(resultItem))
        {
            Debug.Log($"[CookingCampFire] Added {resultItem.itemName} to inventory");
        }
        else
        {
            // If inventory is full, spawn in world
            if (resultItem.worldPrefab != null)
            {
                Vector3 spawnPos = transform.position + transform.forward * 0.5f + Vector3.up * 0.5f;
                GameObject food = Instantiate(resultItem.worldPrefab, spawnPos, Quaternion.identity);
                Debug.Log($"[CookingCampFire] Inventory full - spawned {resultItem.itemName} in world");
            }
        }

        slotStates[slotIndex] = CookingState.Empty;
        slotTimers[slotIndex] = 0f;
        slotRecipeIndices[slotIndex] = -1;

        UpdateSlotVisual(slotIndex);
    }

    // ========================================== 
    // HELPER FUNCTIONS
    // ==========================================

    protected int FindFirstReadySlot()
    {
        for (int i = 0; i < 4; i++)
        {
            if (slotStates[i] == CookingState.Cooked || slotStates[i] == CookingState.Burned)
                return i;
        }
        return -1;
    }

    protected int CountSlotsWithState(CookingState state)
    {
        int count = 0;
        for (int i = 0; i < 4; i++)
        {
            if (slotStates[i] == state) count++;
        }
        return count;
    }

    protected GameObject GetPlayerHeldItem()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            var holdingItem = player.GetComponent<PlayerHoldingItem>();
            if (holdingItem != null) return holdingItem.GetCurrentHeldObject();
        }
        return null;
    }

    protected ItemData GetHeldItemData()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            var holdingItem = player.GetComponent<PlayerHoldingItem>();
            if (holdingItem != null) return holdingItem.GetCurrentItemData();
        }
        return null;
    }

    protected void ClearPlayerHeldItem()
    {
        PlayerHoldingItem holdingItem = GameObject.FindGameObjectWithTag("Player")?.GetComponent<PlayerHoldingItem>();
        if (holdingItem != null)
        {
            holdingItem.Clear();
        }
    }

    protected ItemData GetItemDataFromObject(GameObject obj)
    {
        if (obj == null) return null;

        var itemHeld = obj.GetComponent<ItemHeld>();
        if (itemHeld != null && itemHeld.itemData != null) return itemHeld.itemData;

        var itemComponent = obj.GetComponent<Item>();
        if (itemComponent != null && itemComponent.itemData != null) return itemComponent.itemData;

        return null;
    }

    // ========================================== 
    // VISUAL UPDATES
    // ==========================================

    protected virtual void UpdateSlotVisual(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= 4) return;
        if (cookingSlots[slotIndex] == null || cookingSlots[slotIndex].slotPosition == null)
        {
            Debug.LogWarning($"[CookingCampFire] Slot {slotIndex} is not properly configured!");
            return;
        }

        CookingState state = slotStates[slotIndex];
        int recipeIndex = slotRecipeIndices[slotIndex];

        // Clean up existing visuals for this slot
        CleanupSlotVisual(slotIndex);

        // If empty, nothing to show
        if (state == CookingState.Empty)
        {
            return;
        }

        // Get the recipe to access visual prefabs
        if (recipeIndex < 0 || recipeIndex >= recipes.Count)
        {
            Debug.LogWarning($"[CookingCampFire] Invalid recipe index {recipeIndex} for slot {slotIndex}");
            return;
        }

        CampfireRecipeSO recipe = recipes[recipeIndex];
        if (recipe == null)
        {
            Debug.LogWarning($"[CookingCampFire] Recipe is null at index {recipeIndex}");
            return;
        }

        Transform slotTransform = cookingSlots[slotIndex].slotPosition;
        GameObject prefabToInstantiate = null;

        // Determine which prefab to use based on state
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

        // Instantiate the appropriate visual
        if (prefabToInstantiate != null)
        {
            GameObject visual = Instantiate(prefabToInstantiate, slotTransform.position, slotTransform.rotation, slotTransform);
            visual.name = $"{state}Visual_Slot{slotIndex}";

            // Store reference based on state
            switch (state)
            {
                case CookingState.Cooking:
                    rawVisuals[slotIndex] = visual;
                    Debug.Log($"[CookingCampFire] Slot {slotIndex} - Instantiated RAW visual from recipe");
                    break;

                case CookingState.Cooked:
                    cookedVisuals[slotIndex] = visual;
                    Debug.Log($"[CookingCampFire] Slot {slotIndex} - Instantiated COOKED visual from recipe");
                    break;

                case CookingState.Burned:
                    burnedVisuals[slotIndex] = visual;
                    Debug.Log($"[CookingCampFire] Slot {slotIndex} - Instantiated BURNED visual from recipe");
                    break;
            }
        }
        else
        {
            Debug.LogWarning($"[CookingCampFire] No visual prefab assigned for {state} state in recipe {recipe.recipeName}");
        }
    }
    IEnumerator FadeAudio(float targetVolume)
    {
        if (fireSound == null) yield break;

        if (!fireSound.isPlaying)
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

    public void Interact(PlayerHoldingItem playerHoldingItem)
    {
        throw new System.NotImplementedException();
    }
}

// ========================================== 
// COOKING SLOT & FUEL DATA
// ==========================================

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