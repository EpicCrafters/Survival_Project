using NUnit.Framework.Constraints;
using UnityEngine;

public class MyRock : MonoBehaviour, IDamageable, IMinenable
{
    public enum Type
    {
        Rock,
        
    }


    [SerializeField] private Type rockType;
    [SerializeField] private Transform stonePrefab;
    public int itemDrop;
    //[SerializeField] private Transform fxTreeDestroyed;
    //[SerializeField] private Transform fxTreeLogDestroyed;
    //[SerializeField] private Transform fxTreeLogHalfDestroyed;
    //[SerializeField] private Transform fxTreeStumpDestroyed;


    private HealthSystem healthSystem;


    private void Awake()
    {
        int healthAmount;



        switch (rockType)
        {
            default:
            case Type.Rock: healthAmount = 30; break;
           
        }

        healthSystem = new HealthSystem(healthAmount);
        healthSystem.OnDead += HealthSystem_OnDead;


    }

    private void HealthSystem_OnDead()
    {

        for (int i = 0; i < itemDrop; i++)
        {
            Vector3 offset = new Vector3(Random.Range(-0.2f, 0.2f), 0.1f, Random.Range(-0.2f, 0.2f));
            Quaternion randomRot = Quaternion.Euler(0, Random.Range(0, 360), 0);
            Instantiate(stonePrefab, transform.position + offset, randomRot);
        }
        Destroy(gameObject);
    }


    public void Damage(int amount)
    {

        healthSystem.Damage(amount);

    }



    //private void OnCollisionEnter(Collision collision)
    //{
    //    Debug.Log($"Collision with {collision.gameObject.name}");

    //    //IDamageable damageSource = collision.gameObject.GetComponent<IDamageable>();
    //    //if (damageSource != null && collision.relativeVelocity.magnitude > 1f)
    //    //{
    //    //    int damageAmount = Random.Range(5, 20);
    //    //    DamagePopup.Create(collision.GetContact(0).point, damageAmount, damageAmount > 14);
    //    //    Damage(damageAmount);
    //    //}
    //}
    

    public ResourceType GetResourceType() => ResourceType.Rock;
    public HealthSystem GetHealthSystem()
    {
        return healthSystem;
    }
}