using UnityEngine;
using Unity.Cinemachine;

public class Player_ChoppingTree : MonoBehaviour, IDamageable
{
    [SerializeField] private Animator animator;
    [SerializeField] private Transform visualTransform;
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private CinemachineImpulseSource treeShake;
    [SerializeField] private GameObject hitArea;
    //[SerializeField] private GameObject fxTreeHit;
    //[SerializeField] private GameObject fxTreeHitBlock;

    public void Damage(int amount)
    {
        throw new System.NotImplementedException();
    }

    private void AnimationEvent_OnHit()
    {
        Debug.Log("chop chop");
        Vector3 colliderSize = Vector3.one * .3f;
        Collider[] colliderArray = Physics.OverlapBox(hitArea.transform.position, colliderSize);
        foreach (Collider collider in colliderArray)
        {
            if (collider.TryGetComponent<IDamageable>(out IDamageable treeDamageable))
            {
                int damageAmount = UnityEngine.Random.Range(10, 20);
                DamagePopup.Create(hitArea.transform.position, damageAmount, damageAmount > 14);


                treeDamageable.Damage(damageAmount);



                treeShake.GenerateImpulse();



            }
        }
    }


    
}
  //Instantiate(fxTreeHit,hitArea.transform.position,Quaternion.identity);
                //Instantiate(fxTreeHitBlock, hitArea.transform.position, Quaternion.identity);