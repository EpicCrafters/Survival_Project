using UnityEngine;
using System.Collections;

public class AnimalCombat : MonoBehaviour
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

        readyToAttack = true; // Đánh xong, sẵn sàng tấn công tiếp
        //Debug.Log("Hitbox Enable");
    }

    // Tắt hitbox sau khi đánh xong
    public void DisableHitbox()
    {
        if (hitbox != null)
            hitbox.enabled = false;

        readyToAttack = false; // Đang cooldown, chưa sẵn sàng tấn công
        //Debug.Log("Hitbox Disable");
    }

    // Xử lý khi hitbox va chạm với đối tượng khác
    private void OnTriggerEnter(Collider other)
    {
        var ai = GetComponent<BaseAnimalAI>();
        if (ai.chasingTarget != null && other.transform == ai.chasingTarget)
        {
            // Nếu mục tiêu có interface IDamageable, gọi Damage
            var damageable = other.GetComponent<IDamageable>();
            if (damageable != null)
            {
                damageable.Damage(ai.animalData.damage);
            }
        }
    }

    // Coroutine cooldown sau khi tấn công
    public IEnumerator AttackCooldown(System.Action onCooldownComplete)
    {
        FaceTarget(); // Quay về hướng mục tiêu

        yield return new WaitForSeconds(attackCooldown); // Chờ cooldown
        onCooldownComplete?.Invoke(); // Gọi lại khi cooldown xong
    }

    // Quay hướng thú về phía mục tiêu trước khi tấn công
    private void FaceTarget()
    {
        var ai = GetComponent<BaseAnimalAI>();
        if (ai.chasingTarget == null) return;

        Vector3 lookDir = ai.chasingTarget.position - transform.position;
        lookDir.y = 0; // Chỉ quay theo trục Y

        if (lookDir != Vector3.zero)
        {
            Quaternion targetRot = Quaternion.LookRotation(lookDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 10f);
        }
    }
}
