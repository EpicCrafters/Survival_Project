using UnityEngine;
using System.Collections.Generic;

public class ItemHitBox : MonoBehaviour
{
    public Collider hitbox;
    private ItemData itemData;
    private HashSet<GameObject> alreadyHit = new HashSet<GameObject>();

    private void Awake()
    {
        if (hitbox != null) hitbox.enabled = false;
    }

    public void SetItemData(ItemData data) => itemData = data;

    public void EnableHitbox()
    {
        if (hitbox != null)
        {
            hitbox.enabled = true;
            hitbox.isTrigger = true;
        }
        alreadyHit.Clear();
    }

    public void DisableHitbox()
    {
        if (hitbox != null)
        {
            hitbox.enabled = false;
            hitbox.isTrigger = false;
        }
        alreadyHit.Clear();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (alreadyHit.Contains(other.gameObject)) return;
        alreadyHit.Add(other.gameObject);

        if (itemData == null) return;
        if (!other.TryGetComponent<IDamageable>(out var target)) return;

        int dmg = 0;
        if (itemData.type == ItemType.Tool)
        {
            if (other.TryGetComponent<IMinenable>(out var minable))
            {
                if (IsToolValidForResource(itemData.tool.toolType, minable.GetResourceType()))
                    dmg = itemData.tool.damage;
            }
        }
        else if (itemData.type == ItemType.Weapon)
        {
            dmg = itemData.weapon.damage;
        }

        if (dmg > 0)
        {
            Vector3 hitPoint = other.ClosestPoint(transform.position);
            Vector3 hitNormal = (other.transform.position - transform.position).normalized;

            HitInfo hit = new HitInfo(hitPoint, hitNormal, transform.forward, gameObject, itemData);

            // Apply damage
            target.Damage(dmg, hit);

            // ✅ Only trigger hit stop if target allows it
            if (target.CanTriggerHitStop())
            {
                var hitStop = GetComponentInParent<LocalHitStop>();
                if (hitStop != null)
                {
                    // Optional: only do hit stop if the hit killed the target
                    if (target.IsDead())
                        hitStop.DoHitStop(0.08f);
                    //else
                    //    hitStop.DoHitStop(0.08f);
                }
            }
        }
    }

    private bool IsToolValidForResource(ToolType tool, ResourceType resource)
    {
        return (tool == ToolType.Axe && resource == ResourceType.Tree)
            || (tool == ToolType.Pickaxe && resource == ResourceType.Rock);
    }
}
