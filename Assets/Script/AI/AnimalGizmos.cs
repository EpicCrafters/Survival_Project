using UnityEngine;

public class AnimalGizmos : MonoBehaviour
{
    private BaseAnimalAI ai;

    private void OnDrawGizmosSelected()
    {
        ai = GetComponent<BaseAnimalAI>();
        if (ai == null || ai.animalData == null) return;

        Gizmos.color = ai.animalData.canAttack ? Color.red : Color.green;
        Gizmos.DrawWireSphere(transform.position, ai.GetComponent<AnimalMovement>().detectionRange);

        if (ai.animalData.canAttack)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, ai.GetComponent<AnimalCombat>().attackRange);


        }
        else
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, ai.GetComponent<AnimalMovement>().fleeDistance);
        }
    }
}
