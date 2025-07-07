using UnityEngine;

public class Weapon : MonoBehaviour
{
    public ItemData itemData; 
    [SerializeField]private Collider hitbox;
    private bool canDealDamage = false;

    private void Awake()
    {
       
        if (hitbox == null)
            Debug.LogError("No hitbox found!");
        else
            hitbox.enabled = false;
    }

    public void EnableHitbox()
    {
        canDealDamage = true;
        hitbox.enabled = true;
    }

    public void DisableHitbox()
    {
        hitbox.enabled = false;
        canDealDamage = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!canDealDamage) return;

        if (other.TryGetComponent<IDamageable>(out var damageable))
        {
            damageable.Damage(itemData.weapon.damage); // Pull damage value from ItemData
            canDealDamage = false; // Avoid hitting twice in one swing
        }
    }
}
