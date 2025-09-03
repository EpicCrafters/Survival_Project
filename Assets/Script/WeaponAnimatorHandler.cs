using UnityEngine;
using System.Collections.Generic;

public class WeaponAnimatorHandler : MonoBehaviour
{
    [SerializeField] private Animator animator;

    public AnimatorOverrideController overrideController;
    private ItemData currentWeapon;

    public ItemData CurrentWeapon => currentWeapon;

    public void EquipWeapon(ItemData weaponData)
    {
        if (weaponData == null || weaponData.type != ItemType.Weapon)
            return;


        
        currentWeapon = weaponData;

        if (overrideController == null)
            overrideController = new AnimatorOverrideController(animator.runtimeAnimatorController);

        var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        overrideController.GetOverrides(overrides);

        for (int i = 0; i < currentWeapon.weapon.combos.Length; i++)
        {
            string targetState = $"Combo{i + 1}";
            AnimationClip newClip = currentWeapon.weapon.combos[i].animation;

            for (int j = 0; j < overrides.Count; j++)
            {
                if (overrides[j].Key.name == targetState)
                {
                    overrides[j] = new KeyValuePair<AnimationClip, AnimationClip>(overrides[j].Key, newClip);
                    Debug.Log($"[WeaponAnimatorHandler] Replaced {targetState} with {newClip.name}");
                }
            }
        }

        overrideController.ApplyOverrides(overrides);
        animator.runtimeAnimatorController = overrideController;
    }


    public ComboData GetComboData(int index)
    {
        if (currentWeapon == null) return null;
        if (index < 0 || index >= currentWeapon.weapon.combos.Length) return null;
        return currentWeapon.weapon.combos[index];
    }
}
