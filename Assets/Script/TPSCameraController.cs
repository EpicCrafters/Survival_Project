using UnityEngine;

public class TPSCameraController : MonoBehaviour
{
    public Transform player;       // Assign the player
    public Vector3 offset = new Vector3(0, 1.6f, 0); // shoulder/head height

    void LateUpdate()
    {
        transform.position = player.position + offset;
    }
}
