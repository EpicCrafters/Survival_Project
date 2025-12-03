using Mirror;
using System;
using UnityEngine;

public class HealthSystem
{
    public int health;
    public int healthMax;

    public event Action OnDead;
    public event Action<int, int> OnHealthChanged; // current, max

    public HealthSystem(int healthMax)
    {
        this.healthMax = healthMax;
        this.health = healthMax;
    }

    public int GetHealth() => health;
    public int GetHealthMax() => healthMax;
    public void SetHealth(int curHealth) { health = curHealth; }

    public float GetHealthPercent() => (float)health / healthMax;

    public void Damage(int amount)
    {
        health -= amount;
        if (health < 0) health = 0;

        OnHealthChanged?.Invoke(health, healthMax);

        if (health == 0)
        {
            OnDead?.Invoke();
        }
    }

    public void Heal(int amount)
    {
        health += amount;
        if (health > healthMax) health = healthMax;

        OnHealthChanged?.Invoke(health, healthMax);
    }
}
