using NUnit.Framework.Constraints;
using UnityEngine;

public class MyTree : MonoBehaviour, IDamageable,Iinteractable
{
    public enum Type
    {
        Tree,
        Log,
        LogHaft,
        Stump
    }


    [SerializeField] private Type treeType;
    //[SerializeField] private Transform fxTreeDestroyed;
    //[SerializeField] private Transform fxTreeLogDestroyed;
    //[SerializeField] private Transform fxTreeLogHalfDestroyed;
    //[SerializeField] private Transform fxTreeStumpDestroyed;
    [SerializeField] private Transform treeLog;
    [SerializeField] private Transform treeLogHalf;
    [SerializeField] private Transform treeStump;

    private HealthSystem healthSystem;


    private void Awake()
    {
        int healthAmount;



        switch (treeType)
        {
            default:
            case Type.Tree: healthAmount = 30; break;
            case Type.Log: healthAmount = 50; break;
            case Type.LogHaft: healthAmount = 50; break;
            case Type.Stump: healthAmount = 50; break;
        }

        healthSystem = new HealthSystem(healthAmount);
        healthSystem.OnDead += HealthSystem_OnDead;


    }

    private void HealthSystem_OnDead()
    {

        switch (treeType)
        {
            default:
            case Type.Tree:
                //Instantiate(fxTreeDestroyed,transform.position,transform.rotation);
                Vector3 treeLogOffset = transform.up * 0.4f;
                Instantiate(treeLog, transform.position + treeLogOffset, Quaternion.Euler(Random.Range(-1.5f, +1.5f), 0, Random.Range(-1.5f, +1.5f)));
                Instantiate(treeStump, transform.position, transform.rotation);
                break;

            case Type.Log:
                //Instantiate(fxTreeLogDestroyed,transform.position, transform.rotation);


                float logYPositionAboveStump = 0.8f;
                treeLogOffset = transform.up * logYPositionAboveStump;
                Instantiate(treeLogHalf, transform.position + treeLogOffset, transform.rotation);

                float logYPositionAboveFirstLogHalf = 3.1f;
                treeLogOffset = transform.up * logYPositionAboveFirstLogHalf;
                Instantiate(treeLogHalf, transform.position + treeLogOffset, transform.rotation);


                break;


            case Type.LogHaft:


                //Instantiate(fxTreeLogHalfDestroyed,transform.position, transform.rotation);
                break;

            case Type.Stump:
                //Instantiate (fxTreeStumpDestroyed,transform.position, transform.rotation);
                break;
        }
        Destroy(gameObject);
    }


    public void Damage(int amount)
    {

        healthSystem.Damage(amount);
       
    }



    private void OnCollisionEnter(Collision collision)
    {
        Debug.Log($"Collision with {collision.gameObject.name}");

        IDamageable damageSource = collision.gameObject.GetComponent<IDamageable>();
        if (damageSource != null && collision.relativeVelocity.magnitude > 1f)
        {
            int damageAmount = Random.Range(5, 20);
            DamagePopup.Create(collision.GetContact(0).point, damageAmount, damageAmount > 14);
            Damage(damageAmount);
        }
    }
    public void Interact()
    {
        //int damageAmount = Random.Range(5, 20);
        //Damage(damageAmount);
        //Vector3 popupPosition = transform.position + Vector3.up * 2f;
        //DamagePopup.Create(popupPosition, damageAmount, damageAmount > 14);
        Debug.Log("Player started chopping animation.");
    }
  
}