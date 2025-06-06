
using System;

public class HealthSystem
{
    private int health;
    private int healthMax;
    public event Action OnDead;



    public HealthSystem(int healthMax)
    {
        this.healthMax = healthMax;
        health = healthMax;
    }

    public int GetHealth()
    {
        return health;
    }
    public float GetHealthPercent()
    {
        return (float)health / healthMax;
    }


    public void Damage(int damageAmount)
    {
        health -= damageAmount;
        if (health < 0) health = 0;

        if (health == 0)
        {
            OnDead?.Invoke(); 
        }
    }

    public void Heal(int healAmount)
    {
        health+= healAmount;
        if(health >healthMax) health=healthMax;
    }
}
