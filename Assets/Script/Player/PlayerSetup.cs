using Mirror;
using UnityEngine;
using System.Collections;

public class PlayerSetup : NetworkBehaviour
{
    public Player player;
    public GameInput gameInput;
    public PlayerHoldingItem holdingItem;
    public CameraManager cameraManager;
    public PlayableAnimationBlender playableAnimationBlender;
    public PlayerIKController playerIKController;
    private Transform aimTarget;


    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        name = $"Player[{netId}] (Local)";

        // Cache components
        player = GetComponent<Player>();
        holdingItem = GetComponent<PlayerHoldingItem>();

        // UI hooks
        UIManager.Instance.HookPlayer(GetComponent<PlayerStatManager>());
    
        InventoryManager.instance?.SetPlayerHolding(holdingItem);
        InventoryManager.instance.SetGameInput(gameInput);

        // ============ 1. Assign Camera to Player ============
        cameraManager = FindObjectOfType<CameraManager>();
        if (cameraManager != null)
            cameraManager.AssignCameraTargets(transform);
        GameObject aimObj = new GameObject($"AimTarget_{netId}");
        aimTarget = aimObj.transform;

        player.SetAimTarget(aimTarget);
        playerIKController.SetAimTargetIK(aimTarget);
        if (playableAnimationBlender != null)
            playableAnimationBlender.SetLookAtTarget(aimTarget);
    }

   
}
