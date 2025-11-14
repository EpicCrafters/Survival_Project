using Mirror;
using UnityEngine;
using System.Collections.Generic;

public class ItemHitBox : MonoBehaviour
{
    [Header("Hitbox collider của vũ khí / công cụ")]
    public Collider hitbox;

    // Tham chiếu tới script ItemHeld (chứa dữ liệu item hiện tại mà người chơi đang cầm)
    private ItemHeld itemHeld;

    // Dữ liệu của item (chứa damage, loại item, tool type...)
    private ItemData itemData;

    // Danh sách lưu lại những đối tượng đã bị đánh trong cùng một lần swing
    // để tránh việc gây damage nhiều lần cho cùng 1 đối tượng trong 1 cú đánh.
    private HashSet<GameObject> alreadyHit = new HashSet<GameObject>();

    // Tham chiếu đến script PlayerCombat (dùng để gửi lệnh CmdDealDamage lên server)
    private PlayerCombat playerCombat;


    // Gọi khi object được tạo hoặc kích hoạt
    private void Awake()
    {
        // Đảm bảo collider tắt khi khởi tạo (tránh va chạm ngoài ý muốn)
        if (hitbox != null) hitbox.enabled = false;

        // Lấy component ItemHeld từ parent (vì thường HitBox nằm trong prefab của item)
        itemHeld = GetComponentInParent<ItemHeld>();
    }


    private void Start()
    {
        // Tìm PlayerCombat trong parent hierarchy nếu chưa được set
        if (playerCombat == null)
            playerCombat = GetComponentInParent<PlayerCombat>();

        // Báo lỗi nếu không tìm thấy (quan trọng vì sẽ không thể gửi damage lên server)
        if (playerCombat == null)
            Debug.LogError($"[ItemHitBox] PlayerCombat not found in parent hierarchy of {gameObject.name}!");
    }


    // Hàm này được gọi từ ItemHeld khi cầm item để gán PlayerCombat tương ứng
    public void SetPlayerCombat(PlayerCombat combat)
    {
        playerCombat = combat;
    }


    // Cập nhật lại dữ liệu của item (dùng khi itemHeld thay đổi)
    private void UpdateItemData()
    {
        if (itemHeld != null)
            itemData = itemHeld.itemData;
    }


    // Bật collider hitbox để bắt đầu kiểm tra va chạm (gọi khi swing bắt đầu)
    public void EnableHitbox()
    {
        UpdateItemData();

        if (hitbox != null)
        {
            hitbox.enabled = true;
            hitbox.isTrigger = true; // dùng trigger để không va chạm vật lý thật
        }

        // Xóa danh sách đối tượng đã trúng trước đó
        alreadyHit.Clear();
    }


    // Tắt hitbox sau khi kết thúc swing
    public void DisableHitbox()
    {
        if (hitbox != null)
        {
            hitbox.enabled = false;
            hitbox.isTrigger = false;
        }

        alreadyHit.Clear();
    }


    // Khi collider của vũ khí chạm vào collider khác
    private void OnTriggerEnter(Collider other)
    {
        // Đảm bảo itemData đã được cập nhật
        UpdateItemData();

        if (itemData == null)
        {
            Debug.LogWarning($"[ItemHitBox] itemData is null");
            return;
        }

        // Nếu đối tượng này đã bị đánh trong cú đánh này => bỏ qua
        if (alreadyHit.Contains(other.gameObject)) return;
        alreadyHit.Add(other.gameObject);


        // Tìm interface IDamageable để xác định có thể nhận sát thương không
        IDamageable target = other.GetComponent<IDamageable>();
        if (target == null)
            target = other.GetComponentInParent<IDamageable>();

        // Nếu không có interface => bỏ qua
        if (target == null) return;


        // -------------------------------------------
        // Tính toán sát thương dựa vào loại item
        // -------------------------------------------
        int dmg = 0;

        if (itemData.type == ItemType.Tool)
        {
            // Nếu là Tool (cuốc, rìu...) thì kiểm tra có khai thác được không
            if (other.TryGetComponent<IMinenable>(out var minable))
            {
                // Nếu loại tool phù hợp với resource (vd: rìu -> cây, pickaxe -> đá)
                if (IsToolValidForResource(itemData.tool.toolType, minable.GetResourceType()))
                    dmg = itemData.tool.damage;
            }
        }
        else if (itemData.type == ItemType.Weapon)
        {
            // Nếu là vũ khí (kiếm, gậy...) thì lấy damage từ dữ liệu weapon
            dmg = itemData.weapon.damage;
        }

        // Nếu damage = 0 => bỏ qua (vũ khí không hợp lệ)
        if (dmg <= 0) return;


        // -------------------------------------------
        // Tìm NetworkIdentity của target để gửi lên server
        // -------------------------------------------
        NetworkIdentity targetNetId = other.GetComponent<NetworkIdentity>();
        if (targetNetId == null)
            targetNetId = other.GetComponentInParent<NetworkIdentity>();

        if (targetNetId == null)
        {
            Debug.LogWarning($"[ItemHitBox] No NetworkIdentity found on {other.name}");
            return;
        }


        // -------------------------------------------
        // Xác định vị trí và hướng va chạm
        // -------------------------------------------
        Vector3 hitPoint = other.ClosestPoint(transform.position);
        Vector3 hitNormal = (other.transform.position - transform.position).normalized;

        // Lấy thông tin knockback từ ItemData (nếu có)
        KnockbackSettings knockback = itemData.GetKnockbackSettings();


        // -------------------------------------------
        // Gửi lệnh xử lý damage lên server qua PlayerCombat
        // -------------------------------------------
        if (playerCombat != null)
        {
            playerCombat.CmdDealDamage(
                targetNetId.netId,                // ID mạng của mục tiêu
                dmg,                              // lượng damage
                hitPoint,                         // vị trí trúng
                hitNormal,                        // hướng va chạm
                itemData.id,                      // ID item (để xác định loại vũ khí)
                knockback.horizontalForce,        // lực knockback
                knockback.boneSearchRadius,       // bán kính tìm bone để tác động vật lý
                knockback.enableKnockback         // có bật knockback hay không
            );

            Debug.Log($"[ItemHitBox] Sent damage command: {dmg} to {other.name} with knockback={knockback.enableKnockback}");
        }
        else
        {
            Debug.LogError($"[ItemHitBox] PlayerCombat not found on parent!");
        }


        // -------------------------------------------
        // Hiệu ứng hit stop (dừng nhẹ camera khi đánh trúng)
        // Chỉ xử lý ở client cho cảm giác impact tốt hơn
        // -------------------------------------------
        if (target.CanTriggerHitStop())
        {
            var hitStop = GetComponentInParent<LocalHitStop>();

            // Nếu có hitStop và mục tiêu đã chết thì thực hiện hiệu ứng
            if (hitStop != null && target.IsDead())
                hitStop.DoHitStop(0.08f);
        }
    }


    // Hàm phụ: kiểm tra loại công cụ có phù hợp loại tài nguyên không
    private bool IsToolValidForResource(ToolType tool, ResourceType resource)
    {
        return (tool == ToolType.Axe && resource == ResourceType.Tree)
            || (tool == ToolType.Pickaxe && resource == ResourceType.Rock);
    }
}
