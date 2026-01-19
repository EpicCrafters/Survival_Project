using Mirror;
using UnityEngine;

public class Item : NetworkBehaviour, IPickupAble
{
    public ItemData itemData;
    private Transform target; // the player's transform
    private float flySpeed = 10f;
    private float destroyDistance = 0.5f;


    [Server]
    public void Pickup(NetworkIdentity picker)
    {
        //// Find the player or set target externally if needed
        //GameObject player = GameObject.FindWithTag("PlayerMovement");
        //if (player != null)
        //{
        //    target = player.transform;
        //    StartCoroutine(FlyToPlayer());
        //}
        //else
        //{
        //    Debug.LogWarning("PlayerMovement not found, destroying item immediately.");
        NetworkServer.Destroy(gameObject);

    }

    //private System.Collections.IEnumerator FlyToPlayer()
    //{
    //    // Disable collider and physics while flying
    //    Collider col = GetComponent<Collider>();
    //    if (col) col.enabled = false;

    //    Rigidbody rb = GetComponent<Rigidbody>();
    //    if (rb) rb.isKinematic = true;

    //    // Fly toward player
    //    while (Vector3.Distance(transform.position, target.position) > destroyDistance)
    //    {
    //        transform.position = Vector3.MoveTowards(transform.position, target.position, flySpeed * Time.deltaTime);
    //        yield return null;
    //    }

    //    // After reaching the player, destroy the item
    //    Destroy(gameObject);
    //}
}
