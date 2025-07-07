using UnityEngine;

public class Log : MonoBehaviour
{
    [SerializeField] private Transform treeLogHalf;

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.layer == LayerMask.NameToLayer("Ground"))
        {
            Vector3 spawnPos = transform.position;
            Quaternion spawnRot = transform.rotation;

            Instantiate(treeLogHalf, spawnPos, spawnRot);
            Destroy(gameObject);
        }
    }
}
