using Mirror;
using UnityEngine;
using System.Collections.Generic;

public class ItemHitBox : MonoBehaviour
{
    [Header("Hitbox collider của vũ khí / công cụ")]
    public Collider hitbox;
    // Collider dùng để kiểm tra va chạm khi vung vũ khí (chỉ bật khi swing)

    private ItemHeld itemHeld;       // Script chứa thông tin item mà người chơi đang cầm
    private ItemData itemData;       // Dữ liệu item (damage, loại tool, loại weapon...)
    private HashSet<GameObject> alreadyHit = new HashSet<GameObject>();
    // Dùng HashSet để lưu những đối tượng đã trúng trong 1 cú đánh → tránh đánh nhiều lần

    private PlayerCombat playerCombat;  // Script xử lý damage (CmdDealDamage gửi lên server)


    // -----------------------------------------------------------
    // Gọi khi object được tạo hoặc bật
    // -----------------------------------------------------------
    private void Awake()
    {
        // Luôn đảm bảo collider tắt khi khởi tạo
        if (hitbox != null) hitbox.enabled = false;

        // Hitbox nằm trong prefab của item nên lấy từ parent
        itemHeld = GetComponentInParent<ItemHeld>();
    }


    private void Start()
    {
        // Tìm PlayerCombat ở parent nếu chưa được gán
        if (playerCombat == null)
            playerCombat = GetComponentInParent<PlayerCombat>();

        // Báo lỗi nếu không tìm thấy → rất quan trọng đối với Mirror
        if (playerCombat == null)
            Debug.LogError($"[ItemHitBox] Không tìm thấy PlayerCombat trong parent của {gameObject.name}");
    }


    // Hàm được gọi từ ItemHeld để gán đúng PlayerCombat
    public void SetPlayerCombat(PlayerCombat combat)
    {
        playerCombat = combat;
    }


    // Cập nhật dữ liệu item mỗi khi swing (phòng trường hợp đổi item khi đang cầm)
    private void UpdateItemData()
    {
        if (itemHeld != null)
            itemData = itemHeld.itemData;
    }


    // -----------------------------------------------------------
    // Bật hitbox – gọi khi animation bắt đầu vung
    // -----------------------------------------------------------
    public void EnableHitbox()
    {
        UpdateItemData();

        if (hitbox != null)
        {
            hitbox.enabled = true;
            hitbox.isTrigger = true;   // trigger để không tạo va chạm vật lý thật
        }

        // Reset danh sách đã trúng
        alreadyHit.Clear();
    }


    // Tắt hitbox – gọi khi animation kết thúc
    public void DisableHitbox()
    {
        if (hitbox != null)
        {
            hitbox.enabled = false;
            hitbox.isTrigger = false;
        }

        alreadyHit.Clear();
    }


    // -----------------------------------------------------------
    // Xử lý logic khi hitbox va chạm với vật thể khác
    // -----------------------------------------------------------
    private void OnTriggerEnter(Collider other)
    {
        UpdateItemData();

        if (itemData == null)
        {
            Debug.LogWarning("[ItemHitBox] itemData bị null, có thể do itemHeld chưa gán.");
            return;
        }

        // --- Ngăn đánh trúng một đối tượng nhiều lần trong cùng một swing ---
        if (alreadyHit.Contains(other.gameObject)) return;
        alreadyHit.Add(other.gameObject);

        Debug.Log($"[ItemHitBox] Va chạm: {other.name}, Tag: {other.tag}, Layer: {LayerMask.LayerToName(other.gameObject.layer)}");

        // -----------------------------------------------------------
        // TRƯỜNG HỢP 1: ITEM LÀ TOOL (Rìu, Cuốc…) - XỬ LÝ TÀI NGUYÊN
        // -----------------------------------------------------------
        if (itemData.type == ItemType.Tool)
        {
            if (other.TryGetComponent<IMinenable>(out var minable))
            {
                if (IsToolValidForResource(itemData.tool.toolType, minable.GetResourceType()))
                {
                    int dmg = itemData.tool.damage;
                    Debug.Log($"[ItemHitBox] Tool hợp lệ → gây damage {dmg}");
                }
                else
                {
                    Debug.Log($"[ItemHitBox] Tool không phù hợp với loại tài nguyên!");
                    return;
                }
            }
            else
            {
                Debug.Log($"[ItemHitBox] Object {other.name} KHÔNG phải tài nguyên khai thác");
                return;
            }
        }

        // -----------------------------------------------------------
        // TRƯỜNG HỢP 2: ITEM LÀ WEAPON (Kiếm, Gậy…) - XỬ LÝ KẺ ĐỊCH
        // -----------------------------------------------------------
        else if (itemData.type == ItemType.Weapon)
        {
            int dmg = itemData.weapon.damage;
            Debug.Log($"[ItemHitBox] Weapon gây damage {dmg}");

            // Tìm IDamageable trên đối tượng bị đánh
            IDamageable target = other.GetComponent<IDamageable>();
            if (target == null)
                target = other.GetComponentInParent<IDamageable>();

            if (target == null)
            {
                Debug.Log($"[ItemHitBox] Không tìm thấy IDamageable trên {other.name}");
                return;
            }

            // Nếu damage bằng 0 → coi như không hợp lệ
            if (dmg <= 0)
            {
                Debug.LogWarning($"[ItemHitBox] Damage = 0 cho item {itemData.itemName}");
                return;
            }

            // -----------------------------------------------------------
            // LẤY NETWORK IDENTITY để server biết target nào bị đánh
            // -----------------------------------------------------------
            NetworkIdentity targetNetId = other.GetComponent<NetworkIdentity>();
            if (targetNetId == null)
                targetNetId = other.GetComponentInParent<NetworkIdentity>();

            // Lấy thông tin vị trí va chạm để gởi qua server (để knockback chính xác)
            Vector3 hitPoint = other.ClosestPoint(transform.position);
            Vector3 hitNormal = (other.transform.position - transform.position).normalized;
            KnockbackSettings knockback = itemData.GetKnockbackSettings();

            // -----------------------------------------------------------
            // Gửi damage lên server qua PlayerCombat
            // -----------------------------------------------------------
            if (playerCombat != null)
            {
                if (targetNetId != null)
                {
                    // SERVER OBJECT → dùng CMD để sync cho tất cả client
                    playerCombat.CmdDealDamage(
                        targetNetId.netId,
                        dmg,
                        hitPoint,
                        hitNormal,
                        itemData.id,
                        knockback.horizontalForce,
                        knockback.boneSearchRadius,
                        knockback.enableKnockback
                    );

                    Debug.Log($"[ItemHitBox] Gửi CmdDealDamage: {dmg} đến netId={targetNetId.netId}");
                }
                else
                {
                    // LOCAL OBJECT → client xử lý trực tiếp
                    Debug.Log($"[ItemHitBox] Không có NetworkIdentity → gây damage local");
                    target.Damage(dmg);
                }
            }
            else
            {
                Debug.LogError("[ItemHitBox] playerCombat bị null!");
            }

            // -----------------------------------------------------------
            // HIT STOP EFFECT – hiệu ứng game feel khi đánh trúng
            // -----------------------------------------------------------
            if (target.CanTriggerHitStop())
            {
                var hitStop = GetComponentInParent<LocalHitStop>();
                if (hitStop != null && target.IsDead())
                    hitStop.DoHitStop(0.08f);
            }
        }
    }


    // -----------------------------------------------------------
    // Kiểm tra loại công cụ có phù hợp loại tài nguyên không
    // -----------------------------------------------------------
    private bool IsToolValidForResource(ToolType tool, ResourceType resource)
    {
        return (tool == ToolType.Axe && resource == ResourceType.Tree)
            || (tool == ToolType.Pickaxe && resource == ResourceType.Rock);
    }
}