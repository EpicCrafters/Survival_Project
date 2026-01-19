using UnityEngine;

public class SleepBed : MonoBehaviour,Iinteractable
{
    [SerializeField] private float sleepDuration;
    public void Interact()
    {
        Debug.Log("PlayerMovement Sleep");
        StartCoroutine(SleepRoutine());
    }

    public void Interact(PlayerHoldingItem playerHoldingItem)
    {
        throw new System.NotImplementedException();
    }

    private System.Collections.IEnumerator SleepRoutine()
    {
        yield return new WaitForSeconds(sleepDuration);
        Debug.Log("Wake up");

        //Add logic ngay va dem
    }
}
