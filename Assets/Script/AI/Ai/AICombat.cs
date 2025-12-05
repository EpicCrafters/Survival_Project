using UnityEngine;
using Mirror;
using System.Collections;
using System.Collections.Generic;

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

    [Header("Detection - Set to 'Damageable' layer only")]
    public LayerMask hurtboxLayers; // ✅ Just set this to "Damageable" in inspector

    public int maxTargets = 8;
    public bool showDebugLogs = true;

    private Collider[] hitBuffer;
    private bool isDealingDamage = false;
    private HashSet<uint> alreadyHitNetIds = new HashSet<uint>();

    private void Awake()
    {
        hitBuffer = new Collider[maxTargets];
    }

    public void StartDealDamage()
    {
        if (!isServer || !readyToAttack || attackOrigin == null)
            return;

        isDealingDamage = true;
        readyToAttack = false;
        alreadyHitNetIds.Clear();

        PerformHitCheck();
        StartCoroutine(ResetAttackCooldown());
    }

    public void EndDealDamage()
    {
        if (!isServer) return;
        isDealingDamage = false;
        alreadyHitNetIds.Clear();
    }

    private void PerformHitCheck()
    {
        // ✅ Only hits Damageable layer (body parts)
        int hitCount = Physics.OverlapSphereNonAlloc(
            attackOrigin.position,
            attackRange,
            hitBuffer,
            hurtboxLayers, // Set to Damageable layer in inspector
            QueryTriggerInteraction.Collide
        );

        for (int i = 0; i < hitCount; i++)
        {
            Collider target = hitBuffer[i];
            if (target == null || target.transform.root == transform.root)
                continue;

            ProcessHit(target);
        }
    }

    private void ProcessHit(Collider target)
    {
        // Find NetworkIdentity (root entity)
        NetworkIdentity targetNetId = target.GetComponentInParent<NetworkIdentity>();
        if (targetNetId == null) return;

        // ✅ Prevent double hits
        if (alreadyHitNetIds.Contains(targetNetId.netId))
            return;

        alreadyHitNetIds.Add(targetNetId.netId);

        // Find IDamageable
        IDamageable damageable = target.GetComponentInParent<IDamageable>();
        if (damageable == null) return;

        Vector3 hitPoint = target.ClosestPoint(attackOrigin.position);
        Vector3 hitNormal = (hitPoint - attackOrigin.position).normalized;
        Vector3 hitDir = (target.transform.position - transform.position).normalized;

        HitInfo hitInfo = new HitInfo(hitPoint, hitNormal, hitDir, gameObject, null);
        damageable.Damage(attackDamage, hitInfo);

        if (showDebugLogs)
            Debug.Log($"[AICombat] Hit {targetNetId.name} for {attackDamage} damage");
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