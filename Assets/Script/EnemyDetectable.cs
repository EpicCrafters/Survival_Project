using UnityEngine;

public class EnemyDetectable : MonoBehaviour, IDetectable
{
    [SerializeField] private TargetCategory category = TargetCategory.Enemy;
    [SerializeField] private float priority = 1f;
    [SerializeField] private float detectionRadius = 0f;

    public TargetCategory Category => category;
    public float Priority => priority;
    public float DetectionRadius => detectionRadius;
}

