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


        gameInput = FindFirstObjectByType<GameInput>();
        gameInput.Initialize(true);
        // Cache components
        player = GetComponent<Player>();
        holdingItem = GetComponent<PlayerHoldingItem>();
        //Bind Inventory
        name = $"Player[{netId}] (Local)";
        var invData = GetComponentInChildren<InventoryData>();
        var view = SystemManager.Instance.GetComponentInChildren<InventoryView>();

        view.Bind(invData);
        CraftingManager.Instance.Bind(invData,view.CraftingSlots,view.InventorySlots);
        var input = SystemManager.Instance.GetComponentInChildren<InventoryInput>();

        input.SetGameInput(gameInput);
        input.Bind(invData, holdingItem);
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
        // UI hooks
        UIManager.Instance.HookPlayer(GetComponent<PlayerStatManager>());
        //
        //InventoryManager.instance?.SetPlayerHolding(holdingItem);
        //InventoryManager.instance.SetGameInput(gameInput);

        // ============ 1. Assign Camera to Player ============
        cameraManager = FindObjectOfType<CameraManager>();
        if (cameraManager != null)
            cameraManager.AssignCameraToPlayer(playerCameraManager,transform);
        GameObject aimObj = new GameObject($"AimTarget_{netId}");
        aimTarget = aimObj.transform;

        player.SetAimTarget(aimTarget);
        playerIKController.SetAimTargetIK(aimTarget);
        if (playableAnimationBlender != null)
            playableAnimationBlender.SetLookAtTarget(aimTarget);

        //pLayer set up

      playerCombat.SetUpGameInput(gameInput);


    }

   
}
