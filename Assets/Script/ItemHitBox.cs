using UnityEngine;
using System.Collections.Generic;

public class ItemHitBox : MonoBehaviour
{
    public Collider hitbox;  // Assign in inspector or auto-find in Awake
    private ItemData itemData;

    private HashSet<GameObject> alreadyHit = new HashSet<GameObject>();

    private void Awake()
    {
        // Auto-assign hitbox if not assigned in Inspector
        if (hitbox == null)
        {
            // Find first child collider marked as trigger
            Collider[] cols = GetComponentsInChildren<Collider>(true);
            foreach (var col in cols)
            {
                if (col.isTrigger)
                {
                    hitbox = col;
                    break;
                }
            }

            if (hitbox == null)
            {
                Debug.LogWarning("ItemHitBox: No trigger collider found in children!");
            }
        }

        if (hitbox != null)
        {
            hitbox.enabled = false;
            
        }
      
    }

    public void SetItemData(ItemData data)
    {
        itemData = data;
    }

    public void EnableHitbox()
    {
        Debug.Log("Enable Hitbox");
        alreadyHit.Clear();
        if (hitbox != null)
            hitbox.enabled = true;
    }

    public void DisableHitbox()
    {
        Debug.Log("Disable Hitbox");
        if (hitbox != null)
            hitbox.enabled = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("Hit something: " + other.gameObject.name);

        if (alreadyHit.Contains(other.gameObject)) return;
        alreadyHit.Add(other.gameObject);

        if (itemData == null) return;

        if (itemData.type == ItemType.Tool)
        {
            IMinenable minable = other.GetComponent<IMinenable>();
            if (minable != null)
            {
                ToolType heldTool = itemData.tool.toolType;
                ResourceType resourceType = minable.GetResourceType();

                if (IsToolValidForResource(heldTool, resourceType))
                {
                    if (other.TryGetComponent<IDamageable>(out var target))
                    {
                        target.Damage(itemData.tool.damage);
                        Debug.Log($"Tool damaged {other.gameObject.name} for {itemData.tool.damage}");
                    }
                }
                return; // tools only hit minable resources
            }
        }

        if (itemData.type == ItemType.Weapon)
        {
            if (other.TryGetComponent<IDamageable>(out var target))
            {
                target.Damage(itemData.weapon.damage);
                Debug.Log($"Weapon damaged {other.gameObject.name} for {itemData.weapon.damage}");
            }
        }
    }

    private bool IsToolValidForResource(ToolType tool, ResourceType resource)
    {
        // Example logic: customize as needed
        return (tool == ToolType.Axe && resource == ResourceType.Tree)
            || (tool == ToolType.Pickaxe && resource == ResourceType.Rock)
            || (tool == ToolType.Hammer && resource == ResourceType.Bush);
    }
}
