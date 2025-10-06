using UnityEngine;
using System.Collections;

public class AICombat : MonoBehaviour
{
    public float attackRange = 3f;        // Khoảng cách tấn công
    public float attackCooldown = 2f;     // Thời gian chờ giữa các đòn
    public bool readyToAttack = true;     // Kiểm tra có thể tấn công hay không

    public Collider hitbox;               // Collider dùng để phát hiện trúng mục tiêu

    private void Awake()
    {
        // Tắt hitbox khi bắt đầu
        if (hitbox != null)
            hitbox.enabled = false;
    }

    // Bật hitbox khi đang tấn công
    public void EnableHitbox()
    {
        if (hitbox != null)
            hitbox.enabled = true;

       
        //Debug.Log("Hitbox Enable");
    }

    // Tắt hitbox sau khi đánh xong
    public void DisableHitbox()
    {
        if (hitbox != null)
            hitbox.enabled = false;

       
        //Debug.Log("Hitbox Disable");
    }

    // Xử lý khi hitbox va chạm với đối tượng khác
    private void OnTriggerEnter(Collider other)
    {
        //var ai = GetComponent<AIController>();
        {
            // Nếu mục tiêu có interface IDamageable, gọi Damage
            //var damageable = other.GetComponent<IDamageable>();
            //if (damageable != null)
            //{
            //    damageable.Damage(ai.animalData.damage);
            //}

        }


    }
}
