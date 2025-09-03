using UnityEngine;

public class PlayerCombat : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameInput gameInput;
    [SerializeField] private PlayerAnimator playerAnimator;
    [SerializeField] private WeaponAnimatorHandler weaponHandler; // gives us current weapon data

    private bool isAttacking = false;
    private bool isReadyToAttack = true;

    private int comboStep = 0;
    private bool canCombo = false;
    private bool queuedAttack = false;

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
        if (isReadyToAttack)
        {
            DoAttack();
        }
        else if (canCombo)
        {
            queuedAttack = true; // player clicked early, remember it
        }
    }

    private void DoAttack()
    {
        if (weaponHandler.CurrentWeapon == null)
            return;

        int maxCombo = weaponHandler.CurrentWeapon.weapon.combos.Length;

        // reset if we go over max combo
        if (comboStep >= maxCombo)
            comboStep = 0;

        comboStep++;

        //isAttacking = true;
        //isReadyToAttack = false;

        Debug.Log($"Playing Combo {comboStep}/{maxCombo} for {weaponHandler.CurrentWeapon.itemName}");
        playerAnimator.TriggerAttack();
       
        // ^ pass comboStep so animator knows which animation to play
    }

    // Called from animation event 
    public void OpenComboWindow()
    {
        canCombo = true;

        if (queuedAttack)
        {
            queuedAttack = false;
            DoAttack(); // immediately continue combo
        }
    }

    // Called from animation event 
    public void CloseComboWindow()
    {
        canCombo = false;
    }

    // Called at end of last combo animation
    public void EndCombo()
    {
        comboStep = 0;
        isAttacking = false;
        isReadyToAttack = true;
        canCombo = false;
        queuedAttack = false;
    }


    public int CurrentCombo()
    {
        return comboStep;
    }
}
