using UnityEngine;

public class WolfAI : BaseAnimalAI
{
    [Header("Wolf Specific")]
    public float huntingSpeed = 8f; // Speed when actively hunting
    public float prowlRadius = 15f; // Distance to prowl around when hunting

    protected override void HandleAI()
    {
        // Wolf has specific attack behavior
        if (currentState == AnimalState.Attack || currentState == AnimalState.Hit)
            return;

        HandleCommonAI();
    }

    protected override bool ShouldReactToTarget(float distanceToTarget)
    {
        // Wolf reacts to targets within detection range
        return distanceToTarget <= movement.detectionRange;
    }

    protected override void HandleTargetInRange(float distanceToTarget)
    {
        // Priority 1: Attack if in range and can attack
        if (CanAttackTarget(chasingTarget) && animalData.canAttack && canAttack)
        {
            if (!IsLookingAtTarget())
            {
                LookAtTarget(chasingTarget);
                return;
            }
            Debug.Log("Wolf attacking target!");
            ChangeState(AnimalState.Attack);
            canAttack = false;
            return;
        }

        // Priority 2: Chase if target detected but not in attack range
        if (distanceToTarget <= movement.detectionRange && !CanAttackTarget(chasingTarget))
        {
            if (currentState != AnimalState.Chase)
            {
                Debug.Log("Wolf spotted prey! Chasing...");
                ChangeState(AnimalState.Chase);
            }
            return;
        }
    }

    public override void ChangeState(AnimalState newState)
    {
        base.ChangeState(newState);

        // Wolf-specific state handling
        switch (newState)
        {
            case AnimalState.Chase:
                Debug.Log("Wolf is hunting its prey!");
                // Boost speed when chasing
                if (movement != null && movement.agent != null)
                {
                    movement.agent.speed = huntingSpeed;
                }
                break;

            case AnimalState.Attack:
                Debug.Log("Wolf is attacking!");
                // Could add growling sound, aggressive animations, etc.
                break;
        }
    }

    public override void Damage(int amount)
    {
        base.Damage(amount);

        // Wolf becomes more aggressive after being hit
        if (!isDead)
        {
            animalData.damage = Mathf.RoundToInt(animalData.damage * 1.1f); // 10% damage boost
            Debug.Log("Wolf is now enraged! Damage increased!");
        }
    }
}