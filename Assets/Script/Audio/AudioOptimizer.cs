using UnityEngine;

public class AudioOptimizer : MonoBehaviour
{
    [Header("Optimization")]
    public float maxSFXDistance = 50f;
    public int maxSimultaneousSFX = 8;

    void Start()
    {
        // Set AudioSource max distance
        foreach (AudioSource source in GetComponentsInChildren<AudioSource>())
        {
            source.maxDistance = maxSFXDistance;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
        }
    }

    // Pool management for performance
    private bool ShouldPlaySFX(Vector3 position)
    {
        float distance = Vector3.Distance(Camera.main.transform.position, position);
        return distance <= maxSFXDistance;
    }
}