using UnityEngine;

public struct HitInfo
{
    public Vector3 point;     // exact hit position
    public Vector3 normal;    // surface normal
    public Vector3 direction; // attack direction
    public GameObject source; // attacker
    public ItemData itemData; // optional weapon/tool

    public HitInfo(Vector3 point, Vector3 normal, Vector3 direction, GameObject source, ItemData item = null)
    {
        this.point = point;
        this.normal = normal;
        this.direction = direction;
        this.source = source;
        this.itemData = item;
    }
}
