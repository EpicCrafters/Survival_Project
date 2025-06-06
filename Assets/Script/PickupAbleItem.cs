using UnityEngine;

public class PickupableItem : MonoBehaviour,Iinteractable
{
    public string itemName;
    public bool canBePicked = true;
    public Item item;
    private Rigidbody rb;
    private Collider col;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
    }

    public void PickUp(Transform hand)
    {
        canBePicked = false;
        rb.isKinematic = true;
        col.enabled = false;
        transform.SetParent(hand);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
    }

    public void Drop()
    {
        canBePicked = true;
        transform.SetParent(null);
        rb.isKinematic = false;
        col.enabled = true;
    }

    public void Interact()
    {
       
    }
}
