using NUnit.Framework.Constraints;
using UnityEngine;

public class MyRock : MonoBehaviour, IDamageable, IMinenable
{
    public enum Type
    {
        Rock,
        
    }


    [SerializeField] private Type treeType;
    //[SerializeField] private Transform fxTreeDestroyed;
    //[SerializeField] private Transform fxTreeLogDestroyed;
    //[SerializeField] private Transform fxTreeLogHalfDestroyed;
    //[SerializeField] private Transform fxTreeStumpDestroyed;
  

    private HealthSystem healthSystem;


    private void Awake()
    {
        int healthAmount;



        switch (treeType)
        {
            default:
            case Type.Rock: healthAmount = 30; break;
           
        }

        healthSystem = new HealthSystem(healthAmount);
        healthSystem.OnDead += HealthSystem_OnDead;


    }

    private void HealthSystem_OnDead()
    {

       
        Destroy(gameObject);
    }


    public void Damage(int amount)
    {

        healthSystem.Damage(amount);

    }



    private void OnCollisionEnter(Collision collision)
    {
        Debug.Log($"Collision with {collision.gameObject.name}");

        //IDamageable damageSource = collision.gameObject.GetComponent<IDamageable>();
        //if (damageSource != null && collision.relativeVelocity.magnitude > 1f)
        //{
        //    int damageAmount = Random.Range(5, 20);
        //    DamagePopup.Create(collision.GetContact(0).point, damageAmount, damageAmount > 14);
        //    Damage(damageAmount);
        //}
    }
    

    public ResourceType GetResourceType() => ResourceType.Rock;
    public HealthSystem GetHealthSystem()
    {
        return healthSystem;
    }
}