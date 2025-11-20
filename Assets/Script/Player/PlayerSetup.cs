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

    private Transform lookAtTarget;

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        name = $"Player[{netId}] (Local)";

        // Cache components
        player = GetComponent<Player>();
        holdingItem = GetComponent<PlayerHoldingItem>();

        // UI hooks
        UIManager.Instance.HookPlayer(GetComponent<PlayerStatManager>());
        FindObjectOfType<GameSceneUI>()?.SetPlayer(player);
        InventoryManager.instance?.SetPlayerHolding(holdingItem);
        InventoryManager.instance.SetGameInput(gameInput);

        // ============ 1. Assign Camera to Player ============
        cameraManager = FindObjectOfType<CameraManager>();
        if (cameraManager != null)
            cameraManager.AssignCameraTargets(transform);

        // Wait 1 frame so Camera.main updates its parent
        StartCoroutine(SetupLookAtTarget());
    }

    private IEnumerator SetupLookAtTarget()
    {
        // Wait one frame to allow CameraManager to re-parent Camera.main
        yield return null;

        Camera cam = Camera.main;
        if (cam == null)
        {
            Debug.LogError("PlayerSetup: Camera.main is NULL after spawning!");
            yield break;
        }

        // ============ 2. Create LookAtTarget under the correct camera ============
        GameObject target = new GameObject("LookAtTarget");
        target.transform.SetParent(cam.transform);
        target.transform.localPosition = new Vector3(0, 0, 40f);
        target.transform.localRotation = Quaternion.identity;
        lookAtTarget = target.transform;

        // ============ 3. Assign to PlayableAnimationBlender ============
        if (playableAnimationBlender != null)
        {
            playableAnimationBlender.lookAtTarget = lookAtTarget;
            
        }
    }
}
