using Mirror;
using UnityEngine;
using System.Collections.Generic;

public class WeaponAnimatorHandler : NetworkBehaviour
{
    [SerializeField] private Animator animator;
    public AnimatorOverrideController overrideController;
    private ItemData currentWeapon;
    public ItemData CurrentWeapon => currentWeapon;

    [SyncVar(hook = nameof(OnWeaponIdChanged))]
    private int currentWeaponId = 0;

    public void EquipWeapon(ItemData weaponData)
    {
        // CRITICAL: Only the local player should change their own animations
        if (!isLocalPlayer) return;

        if (weaponData == null || weaponData.type != ItemType.Weapon)
        {
            UnequipWeapon();
            return;
        }

        currentWeapon = weaponData;

        // Tell server about weapon change (for other clients to see visual)
        if (isLocalPlayer)
            CmdSetWeapon(weaponData.id);

        ApplyWeaponAnimations(weaponData);
    }

    [Command]
    private void CmdSetWeapon(int weaponId)
    {
        currentWeaponId = weaponId;
    }

    // Called on remote clients when host/other players change weapons
    private void OnWeaponIdChanged(int oldId, int newId)
    {
        if (isLocalPlayer) return; // Local player already applied it

        if (newId == 0)
        {
            currentWeapon = null;
            return;
        }

        ItemData weaponData = ItemDatabase.Get(newId);
        if (weaponData != null && weaponData.type == ItemType.Weapon)
        {
            currentWeapon = weaponData;
            ApplyWeaponAnimations(weaponData);
        }
    }

    private void ApplyWeaponAnimations(ItemData weaponData)
    {
        if (weaponData == null || animator == null) return;

        // Clone a fresh override controller instance for THIS animator
        AnimatorOverrideController localOverride = new AnimatorOverrideController(animator.runtimeAnimatorController);
        var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        localOverride.GetOverrides(overrides);

        for (int i = 0; i < weaponData.weapon.combos.Length; i++)
        {
            string targetState = $"Combo{i + 1}";
            AnimationClip newClip = weaponData.weapon.combos[i].animation;

            for (int j = 0; j < overrides.Count; j++)
            {
                if (overrides[j].Key.name == targetState)
                {
                    overrides[j] = new KeyValuePair<AnimationClip, AnimationClip>(overrides[j].Key, newClip);
                    Debug.Log($"[WeaponAnimatorHandler] {name} replaced {targetState} with {newClip.name}");
                }
            }
        }

        localOverride.ApplyOverrides(overrides);

        // Assign this player's animator a UNIQUE controller
        animator.runtimeAnimatorController = localOverride;
    }


    public void UnequipWeapon()
    {
        if (!isLocalPlayer) return;

        currentWeapon = null;

        if (isLocalPlayer)
            CmdSetWeapon(0);

        // Reset to default animator controller if needed
        // animator.runtimeAnimatorController = originalController;
    }

    public ComboData GetComboData(int index)
    {
        if (currentWeapon == null) return null;
        if (index < 0 || index >= currentWeapon.weapon.combos.Length) return null;
        return currentWeapon.weapon.combos[index];
    }
}