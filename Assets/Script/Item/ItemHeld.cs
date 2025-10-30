using UnityEngine;

public class ItemHeld : MonoBehaviour
{
    [Header("Runtime Info")]
    public ItemData itemData;
    public PlayerHoldingItem owner;

    public void Init(ItemData data, PlayerHoldingItem holder)
    {
        itemData = data;
        owner = holder;
        Debug.Log($"[ItemHeld] Initialized with {itemData?.itemName ?? "NULL"} for {holder.name}");

        // Pass PlayerCombat reference to ItemHitBox if it exists
        ItemHitBox hitBox = GetComponentInChildren<ItemHitBox>();
        if (hitBox != null)
        {
            PlayerCombat playerCombat = holder.GetComponent<PlayerCombat>();
            if (playerCombat != null)
            {
                hitBox.SetPlayerCombat(playerCombat);
                Debug.Log($"[ItemHeld] Set PlayerCombat reference for {itemData.itemName}");
            }
            else
            {
                Debug.LogWarning($"[ItemHeld] PlayerCombat not found on {holder.name}");
            }
        }
    }

  
}