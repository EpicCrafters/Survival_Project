using UnityEngine;

public class PlayerCombat : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameInput gameInput;
    [SerializeField] private PlayerAnimator playerAnimator;

    public bool isAttacking=false;
    public bool isReadyToAttack = true;
    private int comboStep = 0;
    private float lastAttackTime;
    private bool canCombo;
    private bool queuedAttack;

    private void Awake()
    {
        if (gameInput != null)
            gameInput.OnAttack += HandleAttackInput;
    }

    private void OnDestroy()
    {
        if (gameInput != null)
            gameInput.OnAttack -= HandleAttackInput;
    }

    private void HandleAttackInput(object sender, System.EventArgs e)
    {
       
       
       DoAttack();
        

       
       
    }

    private void DoAttack()
    {
    

        playerAnimator.TriggerAttack();
        
    }

   
    public void OpenComboWindow()
    {
        isAttacking = true;

        isReadyToAttack = false;
        //canCombo = true;

        //// auto-continue if player clicked early
        //if (queuedAttack)
        //{
        //    DoAttack();
        //}
    }

    public void CloseComboWindow()
    {
        isAttacking = false;
        isReadyToAttack = true;
        //canCombo = false;
    }

    // Reset after animation finishes
    public void EndCombo()
    {
        comboStep = 0;
        canCombo = false;
        queuedAttack = false;
    }

    public bool IsAttacking()
    {
        return isAttacking;
    }

    public bool IsReadyToAttack()
    {
        return isReadyToAttack;
    }
}
