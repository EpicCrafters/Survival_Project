using UnityEngine;

public class TriggerTest : MonoBehaviour
{
    public void OnTriggerEnter(Collider other)
    {
        Debug.Log("Trigger Enter");
    }


    public void OnTriggerStay(Collider other)
    {
        Debug.Log("Trigger Stay");
    }
}
