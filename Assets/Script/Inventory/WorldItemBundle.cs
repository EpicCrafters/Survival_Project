using UnityEngine;
using Mirror;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Rigidbody))]
public class WorldItemBundle : NetworkBehaviour, IPickupAble
{
    [SyncVar] public int itemId;
    [SyncVar] public int count;

    private ItemData cachedData;
    private Rigidbody rb;

    [Header("Physics Settings")]
    [SerializeField] private float minDropForce = 1.2f;
    [SerializeField] private float maxDropForce = 2.0f;
    [SerializeField] private float minUpForce = 1.5f;
    [SerializeField] private float maxUpForce = 2.5f;
    private Vector3 pendingDropDir;

    private bool hasInitialized;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        // Safety defaults
        rb.mass = 0.6f;
        rb.linearDamping = 1.5f;
        rb.angularDamping = 2f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
    }

    // =============================
    // INIT
    // =============================
    public void Init(ItemData data, int amount, Vector3 dropDir)
    {
        itemId = data.id;
        count = amount;
        cachedData = data;

        pendingDropDir = dropDir;
        hasInitialized = true;
    }
    public override void OnStartServer()
    {
        base.OnStartServer();

        if (!hasInitialized)
            return;

        ApplyDropForce(pendingDropDir);
    }

    private void ApplyDropForce(Vector3 dir)
    {
        if (rb == null) return;

        Vector3 force =
            dir.normalized * Random.Range(minDropForce, maxDropForce)
            + Vector3.up * Random.Range(minUpForce, maxUpForce);

        rb.AddForce(force, ForceMode.Impulse);
        rb.AddTorque(Random.insideUnitSphere * 2f, ForceMode.Impulse);
    }

    // =============================
    // DATA ACCESS
    // =============================
    public ItemData GetItemData()
    {
        if (cachedData == null)
            cachedData = ItemDatabase.Get(itemId);

        return cachedData;
    }

    public string GetDisplayName()
    {
        var data = GetItemData();
        return data != null ? data.itemName : "Unknown";
    }

    public Sprite GetIcon()
    {
        var data = GetItemData();
        return data != null ? data.image : null;
    }

    // =============================
    // IPickupAble
    // =============================
    public void Pickup(NetworkIdentity picker)
    {
        // ❗ Không xử lý pickup ở đây
        // Pickup ONLINE được xử lý trong CmdRequestPickup
        // Hàm này tồn tại để tương thích interface
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Nhắc nhẹ nếu thiếu Rigidbody
        if (GetComponent<Rigidbody>() == null)
            Debug.LogWarning($"{name} is missing Rigidbody");
    }
#endif
}
