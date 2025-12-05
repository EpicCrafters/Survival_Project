using Mirror;
using UnityEngine;
using System.Collections.Generic;

public class ItemHitBox : MonoBehaviour
{
    [Header("Multi-Sphere Detection Settings")]
    public int sphereCount = 5; // Số lượng sphere dùng để phát hiện va chạm
    public float sphereRadius = 0.35f; // Bán kính của mỗi sphere

    [Header("Blade Position Settings")]
    public Transform startPoint; // Điểm bắt đầu của lưỡi kiếm/công cụ
    public Transform endPoint; // Điểm kết thúc của lưỡi kiếm/công cụ

    [Header("Fallback Offsets")]
    public Vector3 startOffset = new Vector3(0f, 0f, 0.2f); // Offset dự phòng nếu không có startPoint
    public Vector3 endOffset = new Vector3(0f, 0f, 1.5f); // Offset dự phòng nếu không có endPoint

    [Header("Detection Layers")]
    [Tooltip("Set to 'Mineable' for resources (trees/rocks) and 'Damageable' for enemies/players")]
    public LayerMask hitLayers; // Layer mask để xác định đối tượng nào có thể bị đánh

    public int maxHitsPerCheck = 15; // Số lượng collider tối đa có thể phát hiện trong 1 lần check

    [Header("Hit Behavior")]
    [Tooltip("Nếu bật, hitbox chỉ gây damage 1 lần cho đến khi disable và enable lại")]
    public bool singleHitPerActivation = true; // CHẾ ĐỘ 1 ĐÒNH = 1 DAMAGE

    [Header("Visual Debug")]
    public bool showDebugGizmos = true; // Hiển thị Gizmos để debug
    public Color inactiveColor = new Color(1f, 1f, 0f, 0.5f); // Màu khi hitbox chưa active
    public Color activeColor = new Color(1f, 0f, 0f, 0.8f); // Màu khi hitbox đang active
    public Color exhaustedColor = new Color(0.5f, 0.5f, 0.5f, 0.5f); // Màu khi đã hit và hết hiệu lực

    private ItemHeld itemHeld;
    private ItemData itemData;
    private PlayerCombat playerCombat;
    private HashSet<uint> alreadyHitNetIds = new HashSet<uint>(); // Lưu các đối tượng đã hit để tránh hit lặp
    private bool isHitboxActive = false; // Trạng thái hitbox có đang hoạt động không
    private bool hasHitSomething = false; // ĐÃ ĐÁNH TRÚNG GÌ ĐÓ CHƯA? (cho chế độ single hit)
    private Collider[] hitBuffer; // Buffer để lưu các collider phát hiện được
    private Vector3[] spherePositions; // Vị trí các sphere dùng để phát hiện

    private void Awake()
    {
        // Giới hạn số lượng sphere và hit buffer trong khoảng hợp lý
        sphereCount = Mathf.Clamp(sphereCount, 1, 50);
        maxHitsPerCheck = Mathf.Clamp(maxHitsPerCheck, 1, 50);
        spherePositions = new Vector3[sphereCount];
        hitBuffer = new Collider[maxHitsPerCheck];
        itemHeld = GetComponentInParent<ItemHeld>();
    }

    private void Start()
    {
        if (playerCombat == null)
            playerCombat = GetComponentInParent<PlayerCombat>();

        if (playerCombat == null)
            Debug.LogError($"[ItemHitBox] Không tìm thấy PlayerCombat component");
    }

    public void SetPlayerCombat(PlayerCombat combat)
    {
        playerCombat = combat;
    }

    
    // Cập nhật thông tin item data từ ItemHeld
 
    private void UpdateItemData()
    {
        if (itemHeld != null)
        {
            itemData = itemHeld.itemData;
            if (itemData != null)
            {
                Debug.Log($"[ItemHitBox] UpdateItemData: {itemData.itemName} (Loại: {itemData.type})");
            }
        }
        else
        {
            Debug.LogWarning("[ItemHitBox] itemHeld là NULL!");
        }
    }

    // BẬT HITBOX - Được gọi từ Animation Event khi bắt đầu đòn tấn công
   
    public void EnableHitbox()
    {
        UpdateItemData();
        isHitboxActive = true;
        alreadyHitNetIds.Clear();
        hasHitSomething = false; // RESET: Chưa đánh trúng gì cả, sẵn sàng cho đòn mới
        Debug.Log($"[ItemHitBox] Hitbox được bật cho {itemData?.itemName ?? "UNKNOWN"}");
    }

   
    // TẮT HITBOX - Được gọi từ Animation Event khi kết thúc đòn tấn công
  
    public void DisableHitbox()
    {
        isHitboxActive = false;
        alreadyHitNetIds.Clear();
        hasHitSomething = false; // RESET: Chuẩn bị cho đòn tấn công tiếp theo
        Debug.Log($"[ItemHitBox] Hitbox bị tắt");
    }

    private void FixedUpdate()
    {
        // Nếu hitbox không active thì không làm gì cả
        if (!isHitboxActive) return;

        // KIỂM TRA CHẾ ĐỘ SINGLE HIT: Nếu đã đánh trúng rồi thì dừng ngay
        if (singleHitPerActivation && hasHitSomething)
        {
            return; // Đã đánh trúng 1 lần rồi, không check nữa cho đến khi disable
        }

        PerformMultiSphereDetection();
    }

    // Thực hiện phát hiện va chạm bằng nhiều sphere dọc theo lưỡi vũ khí
    
    private void PerformMultiSphereDetection()
    {
        UpdateItemData();
        if (itemData == null)
        {
            Debug.LogWarning("[ItemHitBox] itemData là null trong PerformMultiSphereDetection");
            return;
        }

        // Lấy vị trí đầu và cuối của lưỡi kiếm/công cụ
        Vector3 bladeStart = GetBladeStartPosition();
        Vector3 bladeEnd = GetBladeEndPosition();

        // Tính toán vị trí của từng sphere dọc theo lưỡi
        for (int i = 0; i < sphereCount; i++)
        {
            float t = sphereCount > 1 ? (float)i / (sphereCount - 1) : 0f;
            spherePositions[i] = Vector3.Lerp(bladeStart, bladeEnd, t);
        }

        // Kiểm tra va chạm tại mỗi vị trí sphere
        for (int i = 0; i < sphereCount; i++)
        {
            int hitCount = Physics.OverlapSphereNonAlloc(
                spherePositions[i],
                sphereRadius,
                hitBuffer,
                hitLayers,
                QueryTriggerInteraction.Collide
            );

            if (hitCount > 0)
            {
                Debug.Log($"[ItemHitBox] Sphere {i} phát hiện {hitCount} colliders");
            }

            // Xử lý từng collider được phát hiện
            for (int j = 0; j < hitCount; j++)
            {
                Debug.Log($"[ItemHitBox] Phát hiện va chạm: {hitBuffer[j].name}, Layer: {LayerMask.LayerToName(hitBuffer[j].gameObject.layer)}");
                ProcessHit(hitBuffer[j], spherePositions[i]);

                // KIỂM TRA: Nếu chế độ single hit và đã đánh trúng, DỪNG NGAY
                if (singleHitPerActivation && hasHitSomething)
                {
                    Debug.Log($"[ItemHitBox] Đã đánh trúng 1 lần - hitbox tạm ngưng cho đến khi được enable lại");
                    return; // Thoát khỏi vòng lặp hoàn toàn
                }
            }
        }
    }

    private Vector3 GetBladeStartPosition()
    {
        if (startPoint != null)
            return startPoint.position;
        return transform.position + transform.TransformDirection(startOffset);
    }

    private Vector3 GetBladeEndPosition()
    {
        if (endPoint != null)
            return endPoint.position;
        return transform.position + transform.TransformDirection(endOffset);
    }

  
    // Xử lý khi phát hiện va chạm với một đối tượng
   
    private void ProcessHit(Collider other, Vector3 hitSpherePosition)
    {
        Debug.Log($"[ProcessHit] VÀO HÀM XỬ LÝ cho: {other.name}");

        int dmg = 0;

        // ============================================================
        // TRƯỜNG HỢP 1: ITEM LÀ TOOL (Rìu, Cuốc…) - XỬ LÝ TÀI NGUYÊN
        // ============================================================
        if (itemData.type == ItemType.Tool)
        {
            Debug.Log($"[ProcessHit] Item là CÔNG CỤ: {itemData.tool.toolType}");

            // Kiểm tra xem đối tượng có phải là tài nguyên có thể đào/chặt không
            if (other.TryGetComponent<IMinenable>(out var minable))
            {
                Debug.Log($"[ProcessHit] ✅ Tìm thấy IMinenable: {minable.GetResourceType()}");

                // Kiểm tra công cụ có phù hợp với loại tài nguyên không (rìu cho cây, cuốc cho đá)
                if (IsToolValidForResource(itemData.tool.toolType, minable.GetResourceType()))
                {
                    dmg = itemData.tool.damage;
                    Debug.Log($"[ItemHitBox] ✅ Công cụ hợp lệ → gây damage {dmg}");

                    // Tìm BaseResource để lấy uniqueId (ID duy nhất của tài nguyên)
                    BaseResource baseResource = other.GetComponent<BaseResource>();
                    if (baseResource == null)
                        baseResource = other.GetComponentInParent<BaseResource>();

                    if (baseResource != null)
                    {
                        Debug.Log($"[ProcessHit] ✅ Tìm thấy BaseResource: {baseResource.name}, UniqueId: {baseResource.UniqueId}");

                        
                        uint uniqueIdHash = (uint)baseResource.UniqueId.GetHashCode();
                        if (alreadyHitNetIds.Contains(uniqueIdHash))
                        {
                            Debug.Log($"[ProcessHit] Đã đánh tài nguyên UniqueId={baseResource.UniqueId} rồi, bỏ qua");
                            return;
                        }
                        alreadyHitNetIds.Add(uniqueIdHash);

                        // --- KIỂM TRA XEM CÓ PHẢI LÀ KHÚC GỖ (LOG) KHÔNG ---
                        bool isLog = false;
                        MyTree myTree = baseResource as MyTree;
                        if (myTree != null)
                        {
                            // Kiểm tra xem có phải là dạng log (không phải cây đứng hay gốc cây)
                            isLog = (myTree.GetTreeType() == MyTree.TreeType.Log ||
                                    myTree.GetTreeType() == MyTree.TreeType.LogHalf);
                            Debug.Log($"[ProcessHit] Phát hiện MyTree, isLog={isLog}, TreeType={myTree.GetTreeType()}");
                        }

                        if (isLog)
                        {
                            // KHÚC GỖ: Dùng CmdDamageNonPersistent với NetworkIdentity
                            
                            NetworkIdentity targetNetId = other.GetComponent<NetworkIdentity>();
                            if (targetNetId == null)
                                targetNetId = other.GetComponentInParent<NetworkIdentity>();

                            if (targetNetId != null && playerCombat != null)
                            {
                                Vector3 hitPoint = other.ClosestPoint(hitSpherePosition);
                                Vector3 hitNormal = (other.transform.position - hitSpherePosition).normalized;

                                // Gửi lệnh damage cho khúc gỗ
                                playerCombat.CmdDamageNonPersistent(
                                    targetNetId.netId,
                                    dmg,
                                    hitPoint,
                                    hitNormal,
                                    itemData.id
                                );

                                Debug.Log($"[ItemHitBox] ✅ Gửi CmdDamageNonPersistent cho log: netId={targetNetId.netId}, damage={dmg}");

                                // ĐÁ TRÚNG RỒI! Đánh dấu để không đánh nữa (nếu bật single hit mode)
                                hasHitSomething = true;
                            }
                            else
                            {
                                Debug.LogError($"[ItemHitBox] ❌ Log cần NetworkIdentity nhưng không tìm thấy!");
                            }
                        }
                        else if (!string.IsNullOrEmpty(baseResource.UniqueId))
                        {
                            // TÀI NGUYÊN CỐ ĐỊNH: Dùng CmdDamageResource với uniqueId
                            // (Cây, đá cố định sẽ được lưu trong database)
                            if (playerCombat != null)
                            {
                                playerCombat.CmdDamageResource(
                                    baseResource.UniqueId,
                                    dmg,
                                    itemData.id,
                                    minable.GetResourceType()
                                );
                                Debug.Log($"[ItemHitBox] ✅ Gửi CmdDamageResource: damage={dmg} đến UniqueId={baseResource.UniqueId}");

                               
                                hasHitSomething = true;
                            }
                           
                        }
                       
                    }
                  
                }
               
            }
          

            return; // Kết thúc sớm cho trường hợp công cụ
        }

        // ============================================================
        // TRƯỜNG HỢP 2: ITEM LÀ WEAPON (Kiếm, Gậy…) - XỬ LÝ KẺ ĐỊCH
        // ============================================================
        else if (itemData.type == ItemType.Weapon)
        {
            dmg = itemData.weapon.damage;
            Debug.Log($"[ItemHitBox] Vũ khí gây damage {dmg}");

            // Tìm NetworkIdentity cho vũ khí (quái/người chơi cần nó để đồng bộ qua mạng)
            NetworkIdentity targetNetId = other.GetComponentInParent<NetworkIdentity>();
            if (targetNetId == null)
                targetNetId = other.GetComponent<NetworkIdentity>();

            if (targetNetId == null)
            {
                Debug.LogError($"[ItemHitBox] ❌ Mục tiêu vũ khí cần NetworkIdentity nhưng không tìm thấy trên {other.name}");
                return;
            }

            // Ngăn chặn đánh trúng 2 lần cùng 1 đối tượng trong 1 đòn
            if (alreadyHitNetIds.Contains(targetNetId.netId))
            {
                Debug.Log($"[ProcessHit] Đã đánh netId={targetNetId.netId} rồi, bỏ qua");
                return;
            }
            alreadyHitNetIds.Add(targetNetId.netId);

            // Tìm component IDamageable để có thể gây sát thương
            IDamageable target = other.GetComponentInParent<IDamageable>();
            if (target == null)
            {
                Debug.LogWarning($"[ItemHitBox] Không tìm thấy IDamageable trên {other.name}");
                return;
            }

            if (dmg <= 0)
            {
                Debug.LogWarning($"[ItemHitBox] Damage = 0 cho vũ khí {itemData.itemName}");
                return;
            }

            // Tính toán điểm va chạm và hướng đánh
            Vector3 hitPoint = other.ClosestPoint(hitSpherePosition);
            Vector3 hitNormal = (other.transform.position - hitSpherePosition).normalized;
            KnockbackSettings knockback = itemData.GetKnockbackSettings();

            if (playerCombat != null)
            {
                // Gửi lệnh gây sát thương qua mạng
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

                Debug.Log($"[ItemHitBox] ✅ Gửi CmdDealDamage: {dmg} đến netId={targetNetId.netId}");

              
                hasHitSomething = true;
            }
            else
            {
                Debug.LogError("[ItemHitBox] ❌ playerCombat là NULL!");
            }

            // Hiệu ứng hit stop nếu mục tiêu chết
            if (target.CanTriggerHitStop())
            {
                var hitStop = GetComponentInParent<LocalHitStop>();
                if (hitStop != null && target.IsDead())
                    hitStop.DoHitStop(0.08f);
            }
        }
    }

    // Kiểm tra công cụ có hợp lệ với loại tài nguyên không
    // Ví dụ: Rìu cho Cây, Cuốc cho Đá
   
    private bool IsToolValidForResource(ToolType tool, ResourceType resource)
    {
        bool isValid = (tool == ToolType.Axe && resource == ResourceType.Tree) ||
                       (tool == ToolType.Pickaxe && resource == ResourceType.Rock);

        Debug.Log($"[IsToolValidForResource] {tool} vs {resource} = {isValid}");
        return isValid;
    }

    //Vẽ Gizmos trong Scene view để debug hitbox
    
    private void OnDrawGizmos()
    {
        if (!showDebugGizmos) return;

        // Chọn màu dựa trên trạng thái hitbox
        if (isHitboxActive && singleHitPerActivation && hasHitSomething)
        {
            Gizmos.color = exhaustedColor; // Màu xám: đã đánh trúng, hết hiệu lực
        }
        else
        {
            Gizmos.color = isHitboxActive ? activeColor : inactiveColor; // Đỏ: active, Vàng: inactive
        }

        Vector3 bladeStart = GetBladeStartPosition();
        Vector3 bladeEnd = GetBladeEndPosition();

        // Vẽ các sphere dọc theo lưỡi vũ khí/công cụ
        for (int i = 0; i < sphereCount; i++)
        {
            float t = sphereCount > 1 ? (float)i / (sphereCount - 1) : 0f;
            Vector3 spherePos = Vector3.Lerp(bladeStart, bladeEnd, t);
            Gizmos.DrawWireSphere(spherePos, sphereRadius);
        }

        // Vẽ điểm đầu (xanh lá) và điểm cuối (đỏ) của lưỡi
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(bladeStart, 0.05f);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(bladeEnd, 0.05f);
    }

   
}