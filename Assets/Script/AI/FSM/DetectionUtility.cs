using UnityEngine;
using System.Linq;

public static class DetectionUtility
{
    public static IDetectable FindBestTargetByInterface(
        Transform self, float radius, bool isPredator,
        TargetCategory[] predatorChaseCategories,
        TargetCategory[] preyFearCategories)
    {
        IDetectable bestTarget = null;
        float bestScore = float.MinValue;

        // Find all MonoBehaviours that implement IDetectable within radius
        Collider[] hits = Physics.OverlapSphere(self.position, radius);

        foreach (var hit in hits)
        {
            var detectable = hit.GetComponent<IDetectable>();
            if (detectable == null) continue;

            // Predator chases only allowed categories
            if (isPredator && !predatorChaseCategories.Contains(detectable.Category))
                continue;

            // Prey flees only from allowed categories
            if (!isPredator && !preyFearCategories.Contains(detectable.Category))
                continue;

            // Priority score = base priority - distance
            float distance = Vector3.Distance(self.position, hit.transform.position);
            float score = detectable.Priority - distance;

            if (score > bestScore)
            {
                bestScore = score;
                bestTarget = detectable;
            }
        }

        return bestTarget;
    }
}
