using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu()]
public class AnimalData : ScriptableObject
{
    public string animalName;
    public int maxHealth;
    public float moveSpeed;
    public float fleeSpeed;
    public int damage;
    public float sighRange;
    public bool canAttack;



    public int idleCount;


    [Header("Allowed States")]
    public List<string> allowedStates = new List<string>();
}
