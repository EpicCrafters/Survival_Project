using UnityEngine;

public class PlayerMining : MonoBehaviour
{
    [SerializeField] Weapon equippedWeapon;

    public void SetEquippedWeapon(Weapon weapon)
    {
        equippedWeapon = weapon;
    }
    public void EnableHitbox()
    {
        equippedWeapon.EnableHitbox();
    }

    public void DisableHitbox()
    {
        equippedWeapon.DisableHitbox();
    }

}
