using UnityEngine;
using System.Collections.Generic;

public class ItemHitBox : MonoBehaviour
{
    public Collider hitbox;
    private ItemData itemData;

    private HashSet<GameObject> alreadyHit = new HashSet<GameObject>();// Lưu các đối tượng đã trúng trong 1 đòn đánh

    private void Awake()
    {



        if (hitbox != null)
        {
            hitbox.enabled = false;

        }

    }

    public void SetItemData(ItemData data) // Gán dữ liệu vật phẩm cho hitbox
    {
        itemData = data;
    }

    public void EnableHitbox()
    {

        //Debug.Log(" EnableHitbox called on " + gameObject.name);
        if (hitbox != null)
        {
            //Debug.Log(" Enabling hitbox: " + hitbox.name);
            hitbox.enabled = true;
        }
        else
        {
            Debug.LogWarning(" No hitbox assigned in ItemHitBox!");
        }
    }

    public void DisableHitbox()
    {

        alreadyHit.Clear();// Xoá danh sách đối tượng bi danh trung
        //Debug.Log(" DisableHitbox called on " + gameObject.name);
        if (hitbox != null)
        {
            //Debug.Log(" Disabling hitbox: " + hitbox.name);
            hitbox.enabled = false;
        }
        else
        {
            Debug.LogWarning(" No hitbox assigned in ItemHitBox!");
        }
    }


    private void OnTriggerEnter(Collider other)
    {
        //Debug.Log("Hit something: " + other.gameObject.name);

        if (alreadyHit.Contains(other.gameObject)) return;// đã bị đánh trúng trong cùng 1 lần chém 
        alreadyHit.Add(other.gameObject); // Đánh dấu đã trúng


        if (itemData == null) return;

        if (itemData.type == ItemType.Tool)
        {
            IMinenable minable = other.GetComponent<IMinenable>();
            if (minable != null)
            {
                ToolType heldTool = itemData.tool.toolType;
                ResourceType resourceType = minable.GetResourceType();
                // Kiểm tra công cụ có đúng với tài nguyên 
                if (IsToolValidForResource(heldTool, resourceType))
                {
                    if (other.TryGetComponent<IDamageable>(out var target))
                    {
                        target.Damage(itemData.tool.damage);
                        Debug.Log($"Tool damaged {other.gameObject.name} for {itemData.tool.damage}");
                    }
                }
                return; 
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

    private bool IsToolValidForResource(ToolType tool, ResourceType resource) // kiểm tra xem công cụ có đúng với loại tài nguyên 
    {
        // Example logic: customize as needed
        return (tool == ToolType.Axe && resource == ResourceType.Tree)
            || (tool == ToolType.Pickaxe && resource == ResourceType.Rock)
            ;
    }
}
