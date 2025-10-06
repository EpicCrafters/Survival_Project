using UnityEngine;
using System.Collections;

public class LocalHitStop : MonoBehaviour
{
    public bool isStopping = false;

    public void DoHitStop(float duration)
    {
        if (!isStopping) StartCoroutine(HitStopRoutine(duration));
    }

    private IEnumerator HitStopRoutine(float duration)
    {
        isStopping = true;

        // Freeze time for this object (player animator + movement)
        var animator = GetComponent<Animator>();
        var rb = GetComponent<Rigidbody>();

        if (animator != null) animator.speed = 0;
        if (rb != null) rb.isKinematic = true; // optional if physics-based movement

        yield return new WaitForSecondsRealtime(duration); // unaffected by Time.timeScale

        if (animator != null) animator.speed = 1;
        if (rb != null) rb.isKinematic = false;

        isStopping = false;
    }
}
