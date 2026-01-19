using Unity.VisualScripting;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private PlayerMovement player;
    [SerializeField] private PlayerAnimator playerAnimator;
    [SerializeField] private PlayerStatManager playerStatManager;
    [SerializeField] private PlayerItemUseHandler playerItemUseHandler;
    [SerializeField] private PlayerIKController playerIKController;
    [SerializeField] private PlayerInteract playerInteract;
    [SerializeField] private bool isLocalPlayer;





    private void Awake()
    {
        player = GetComponent<PlayerMovement>();
        playerAnimator = GetComponentInChildren<PlayerAnimator>();
        playerInteract = GetComponent<PlayerInteract>();
        playerStatManager = GetComponent<PlayerStatManager>();
        playerItemUseHandler = GetComponent<PlayerItemUseHandler>();
      
        playerIKController= GetComponentInChildren<PlayerIKController>();
    }


    private void Update()
    {
        if (!isLocalPlayer)
        {
            if (player != null)
            {
                player.UpdatePlayer(Time.deltaTime);
            }
            if(playerAnimator != null)
            {
                playerAnimator.UpdatePlayerAnimator(Time.deltaTime);
            }

            if(playerStatManager != null) 
            {
                playerStatManager.UpdatePlayerStat(Time.deltaTime);
            }
            if(playerItemUseHandler != null)
            {
                playerItemUseHandler.UpdatePlayerItemUse(Time.deltaTime);
            }
            if(playerInteract != null)
            {
                playerInteract.UpdatePlayerInteract(Time.deltaTime);
            }
            if(playerIKController!= null)
            {
                playerIKController.UpdatePlayerIk(Time.deltaTime);
            }
            
        }
    }

    private void LateUpdate()
    {
        if (playerIKController != null)
        {
            playerIKController.LateUpdatePlayerIK(Time.deltaTime);
        }
    }
}
