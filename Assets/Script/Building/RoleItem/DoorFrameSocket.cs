using UnityEngine;

public class DoorFrameSocket : MonoBehaviour
{
    public bool occupied = false;
    public Transform snapPoint;

    public void RegisterDoor()
    {
        occupied = true;
    }

    public void RemoveDoor()
    {
        occupied = false;
    }
}
