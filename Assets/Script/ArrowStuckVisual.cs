using UnityEngine;

/// <summary>
/// Simple script for arrow stuck visual - just destroys itself after a timer
/// Attach this to your arrowStuckVisualPrefab
/// </summary>
public class ArrowStuckVisual : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Time before this stuck visual destroys itself")]
    [SerializeField] private float lifetime = 5f;

    private float timer = 0f;

    private void Update()
    {
        timer += Time.deltaTime;

        if (timer >= lifetime)
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Optional: Set lifetime from code if needed
    /// </summary>
    public void SetLifetime(float time)
    {
        lifetime = time;
    }
}