using Mirror;
using Unity.VisualScripting;
using UnityEngine;

public class PlayerSetup : NetworkBehaviour
{
    public Player player;
    public GameInput gameInput;
    public PlayerHoldingItem holdingItem;
    public CameraManager cameraManager;

    public override void OnStartLocalPlayer()
    {

        base.OnStartLocalPlayer();
        name = $"Player[{netId}] (Local)";
        // Cache components
        player = GetComponent<Player>();
        holdingItem = GetComponent<PlayerHoldingItem>();



        
        // UI hooks
        FindObjectOfType<GameSceneUI>()?.SetPlayer(player);
        InventoryManager.instance?.SetPlayerHolding(holdingItem);
        InventoryManager.instance.SetGameInput(gameInput);
        // Camera hook
        cameraManager = FindObjectOfType<CameraManager>();
        if (cameraManager != null)
            cameraManager.AssignCameraTargets(transform); // assign this player's transform

      

        //InventoryManager.instance.AddItem(InventoryManager.instance.stick);
        //InventoryManager.instance.AddItem(InventoryManager.instance.stone);
    }

    public override void OnStartServer()
    {
        Debug.Log($"Player spawned on SERVER: {gameObject.name}");
    }

    public override void OnStartClient()
    {
        Debug.Log($"Player spawned on CLIENT: {gameObject.name}");
    }
}
