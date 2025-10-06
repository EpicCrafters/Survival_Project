using UnityEngine;

public interface IDamageable
{
    // Main damage entrypoint
    void Damage(int amount, HitInfo hitInfo);

    // Shortcut for raw damage (no hit info)
    void Damage(int amount)
    {
        var dummyHit = new HitInfo(Vector3.zero, Vector3.zero, Vector3.zero, null, null);
        Damage(amount, dummyHit);
    }

    // Optional helpers for things like hit stop
    bool CanTriggerHitStop();
    bool IsDead();
}
