using Mirror;
using UnityEngine;
using System.Collections;

public class PlayerSetup : NetworkBehaviour
{
    public Player player;
    public GameInput gameInput;
    public PlayerHoldingItem holdingItem;
    public CameraManager cameraManager;
    public CrosshairManager crosshairManager;
    public PlayableAnimationBlender playableAnimationBlender;
    public PlayerItemUseHandler playerItemUseHandler;
    public PlayerCombat playerCombat;
    public PlayerInteract playerInteract;
    public PlayerIKController playerIKController;
    public PlayerCameraManager playerCameraManager;

    private Transform aimTarget;

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        name = $"Player[{netId}] (Local)";

        // ============ Cache Components ============
        player = GetComponent<Player>();
        holdingItem = GetComponent<PlayerHoldingItem>();
        playerCameraManager = GetComponent<PlayerCameraManager>();
        playerItemUseHandler = GetComponent<PlayerItemUseHandler>();
        gameInput = FindObjectOfType<GameInput>();

        // ============ Setup Camera ============
        cameraManager = FindFirstObjectByType<CameraManager>();
        if (cameraManager != null && playerCameraManager != null)
        {
            cameraManager.AssignCameraToPlayer(playerCameraManager, transform);
        }
        else
        {
            if (cameraManager == null)
                Debug.LogError("[PlayerSetup] CameraManager is null! Make sure it exists in the scene.");
            if (playerCameraManager == null)
                Debug.LogError("[PlayerSetup] PlayerCameraManager is null! Make sure it's attached to the player.");
        }

        // ============ ✨ Setup Crosshair ============
         crosshairManager = FindFirstObjectByType<CrosshairManager>();
        if (crosshairManager != null && playerItemUseHandler != null)
        {
            playerItemUseHandler.SetCrosshairManager(crosshairManager);
            Debug.Log("[PlayerSetup] ✅ CrosshairManager assigned to PlayerItemUseHandler");
        }
        else
        {
            if (crosshairManager == null)
                Debug.LogWarning("[PlayerSetup] CrosshairManager not found in scene!");
            if (playerItemUseHandler == null)
                Debug.LogWarning("[PlayerSetup] PlayerItemUseHandler is null!");
        }

        // ============ UI Hooks ============
        UIManager.Instance.HookPlayer(GetComponent<PlayerStatManager>());

        // ============ Setup Game Input for Components ============
        player.SetUpGameInput(gameInput);
        playerInteract.SetUpGameInput(gameInput);
        playerCombat.SetUpGameInput(gameInput);

        if (playerItemUseHandler != null && gameInput != null)
        {
            playerItemUseHandler.SetUpGameInput(gameInput);
        }

        // ============ Setup Inventory Manager ============
        if (InventoryManager.instance != null)
        {
            InventoryManager.instance.Initialize(true);
            InventoryManager.instance.SetPlayerHolding(holdingItem);
            InventoryManager.instance.SetGameInput(gameInput);
            Debug.Log("[PlayerSetup] InventoryManager initialized successfully");
        }
        else
        {
            Debug.LogError("[PlayerSetup] InventoryManager.instance is NULL! Make sure InventoryManager exists in the scene.");
            StartCoroutine(TryFindInventoryManager());
        }

        // ============ Create Aim Target ============
        GameObject aimObj = new GameObject($"AimTarget_{netId}");
        aimTarget = aimObj.transform;
        player.SetAimTarget(aimTarget);
        playerIKController.SetAimTargetIK(aimTarget);
        if (playableAnimationBlender != null)
            playableAnimationBlender.SetLookAtTarget(aimTarget);
    }

    private IEnumerator TryFindInventoryManager()
    {
        int attempts = 0;
        int maxAttempts = 10;

        while (InventoryManager.instance == null && attempts < maxAttempts)
        {
            Debug.LogWarning($"[PlayerSetup] Waiting for InventoryManager... (Attempt {attempts + 1}/{maxAttempts})");
            yield return new WaitForSeconds(0.1f);
            attempts++;
        }

        if (InventoryManager.instance != null)
        {
            InventoryManager.instance.Initialize(true);
            InventoryManager.instance.SetPlayerHolding(holdingItem);
            Debug.Log("[PlayerSetup] InventoryManager found and initialized via fallback");
        }
        else
        {
            Debug.LogError("[PlayerSetup] Failed to find InventoryManager after multiple attempts! Please add InventoryManager to your scene.");
        }
    }
}