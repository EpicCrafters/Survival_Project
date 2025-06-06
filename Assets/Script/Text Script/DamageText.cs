using UnityEngine;

public class DamageTestObject : MonoBehaviour, IDamageable
{
    public void Damage(int amount)
    {
        Debug.Log("I took " + amount + " damage!");
    }
}