using UnityEngine;
using Mirror;
using System.Collections;

public class AICombat : NetworkBehaviour
{
    [Header("References")]
    public HFSMController controller;
    public Transform attackOrigin;

    [Header("Combat Settings")]
    public float attackRange = 3f;
    public float attackCooldown = 2f;
    public int attackDamage = 10;
    public bool readyToAttack = true;

    [Header("Detection")]
    public LayerMask hurtboxLayers;
    public int maxTargets = 8;

    private Collider[] hitBuffer;
    private bool isDealingDamage = false;

    private void Awake()
    {
        hitBuffer = new Collider[maxTargets];
    }

    // 🔹 Called from animation event at start of swing
    public void StartDealDamage()
    {
        if (!isServer || !readyToAttack || attackOrigin == null)
            return;

        isDealingDamage = true;
        readyToAttack = false;

        PerformHitCheck();

        // You can end damage manually via animation event
        StartCoroutine(ResetAttackCooldown());
    }

    // 🔹 Called from animation event at end of swing
    public void EndDealDamage()
    {
        if (!isServer) return;
        isDealingDamage = false;
    }

    private void PerformHitCheck()
    {
        int hitCount = Physics.OverlapSphereNonAlloc(
            attackOrigin.position,
            attackRange,
            hitBuffer,
            hurtboxLayers,
            QueryTriggerInteraction.Collide
        );

        for (int i = 0; i < hitCount; i++)
        {
            Collider target = hitBuffer[i];
            if (target == null || target.transform.root == transform.root)
                continue;

            var damageable = target.GetComponentInParent<IDamageable>();
            if (damageable == null)
                continue;

            Vector3 hitPoint = target.ClosestPoint(attackOrigin.position);
            Vector3 hitDir = (target.transform.position - transform.position).normalized;
            var hitInfo = new HitInfo(hitPoint, Vector3.zero, hitDir, gameObject, null);

            damageable.Damage(attackDamage, hitInfo);
            //Debug.Log($"[AICombat] Hit {target.name} for {attackDamage} damage!");
        }
    }

    private IEnumerator ResetAttackCooldown()
    {
        yield return new WaitForSeconds(attackCooldown);
        readyToAttack = true;
    }

    private void OnDrawGizmosSelected()
    {
        if (attackOrigin != null)
        {
            Gizmos.color = isDealingDamage ? Color.red : Color.gray;
            Gizmos.DrawWireSphere(attackOrigin.position, attackRange);
        }
    }
}
