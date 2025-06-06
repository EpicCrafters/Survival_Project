using UnityEngine;

public class PlayerCarryItem : MonoBehaviour
{
    [SerializeField] private Transform carryPoint;

    private GameObject carriedObject;

    public bool IsCarrying => carriedObject != null;

    public void Carry(GameObject item)
    {
        if (IsCarrying) return;

        carriedObject = item;
        carriedObject.transform.SetParent(carryPoint);
        carriedObject.transform.localPosition = Vector3.zero;
        carriedObject.transform.localRotation = Quaternion.identity;

        Rigidbody rb = carriedObject.GetComponent<Rigidbody>();
        if (rb) rb.isKinematic = true;
    }

    public void Drop()
    {
        if (!IsCarrying) return;

        carriedObject.transform.SetParent(null);
        Rigidbody rb = carriedObject.GetComponent<Rigidbody>();
        if (rb) rb.isKinematic = false;

        rb.AddForce(transform.forward * 2f, ForceMode.Impulse); 

        carriedObject = null;
    }
}
