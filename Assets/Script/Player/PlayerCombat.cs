using Mirror;
using UnityEngine;

public class PlayerCombat : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private GameInput gameInput;
    [SerializeField] private PlayerAnimator playerAnimator;
    [SerializeField] private WeaponAnimatorHandler weaponHandler;

    private bool isAttacking = false;
    private bool isReadyToAttack = true;

    private int comboStep = 0;
    private bool canCombo = false;
    private bool queuedAttack = false;

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();

        // Grab the GameInput only for this player
        gameInput = GetComponentInChildren<GameInput>(true);

        if (gameInput != null)
        {
            gameInput.gameObject.SetActive(true);
            gameInput.OnAttack += HandleAttackInput;
            Debug.Log($"[{name}] LocalPlayer input enabled.");

            // Let inventory know this player's input
            
        }
    }
    public override void OnStartClient()
    {
        base.OnStartClient();
        Debug.Log($"OnStartClient: name={gameObject.name} netId={netId} owner={isLocalPlayer} animator={playerAnimator?.gameObject.name}");
    }
    public override void OnStopLocalPlayer()
    {
        if (gameInput != null)
            gameInput.OnAttack -= HandleAttackInput;
    }

    private void HandleAttackInput(object sender, System.EventArgs e)
    {
        if (!isLocalPlayer) return;
        if (!isReadyToAttack) return;
        CmdDoAttack();




    }

    // ------------------ Mirror Networking ------------------

    [Command]
    private void CmdDoAttack()
    {

        RpcPlayAttack();
    }

    [ClientRpc]
    private void RpcPlayAttack()
    {

        playerAnimator.TriggerAttack();
    }

    // ------------------ Animation event callbacks ------------------

    public void OpenComboWindow()
    {
        canCombo = true;

        if (queuedAttack)
        {
            queuedAttack = false;
            if (isLocalPlayer)
            {
                Debug.Log($"[{name}] Queued attack executed.");
                CmdDoAttack(); // send queued attack to server
            }
        }
    }

    public void CloseComboWindow() => canCombo = false;

    public void EndCombo()
    {
        comboStep = 0;
        isAttacking = false;
        isReadyToAttack = true;
        canCombo = false;
        queuedAttack = false;

        Debug.Log($"[{name}] Combo ended.");
    }
}
