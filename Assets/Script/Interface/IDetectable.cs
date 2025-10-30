
using UnityEngine;

public enum TargetCategory { Player, Structure, Enemy, Neutral,Predator }

public interface IDetectable
{
    TargetCategory Category { get; }
    // Base importance weight (higher = more attractive)
    float Priority { get; }
    // Optional override detection radius (<= 0 -> use controller radius)
    float DetectionRadius { get; }
}