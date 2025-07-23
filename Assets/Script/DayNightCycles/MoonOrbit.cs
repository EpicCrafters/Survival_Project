using UnityEngine;

public class MoonOrbit : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform dailyRotation;  // dailyRotation của Sun
    [SerializeField] private float moonOffset = 90f;

    void LateUpdate()
    {
       
        if (dailyRotation == null) return;

        Vector3 sunForward = dailyRotation.forward;

        Quaternion offsetRot = Quaternion.Euler(moonOffset, 0, 0);

        Vector3 moonDir = offsetRot * -sunForward;

        transform.rotation = Quaternion.LookRotation(moonDir, Vector3.up);
    }
}
