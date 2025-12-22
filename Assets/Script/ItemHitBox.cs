using Mirror;
using UnityEngine;
using System.Collections.Generic;

public class ItemHitBox : MonoBehaviour
{
    [Header("Multi-Sphere Detection Settings")]
    [Tooltip("Số lượng sphere dùng để phát hiện va chạm dọc theo lưỡi kiếm/công cụ")]
    public int sphereCount = 5;
    [Tooltip("Bán kính của mỗi sphere phát hiện")]
    public float sphereRadius = 0.35f;

    [Header("Blade Position Settings")]
    [Tooltip("Điểm bắt đầu của lưỡi kiếm/công cụ")]
    public Transform startPoint;
    [Tooltip("Điểm kết thúc của lưỡi kiếm/công cụ")]
    public Transform endPoint;

    [Header("Fallback Offsets")]
    [Tooltip("Offset từ transform nếu không có startPoint")]
    public Vector3 startOffset = new Vector3(0f, 0f, 0.2f);
    [Tooltip("Offset từ transform nếu không có endPoint")]
    public Vector3 endOffset = new Vector3(0f, 0f, 1.5f);

    [Header("Detection Layers")]
    [Tooltip("Layers có thể gây DAMAGE (Mineable | Damageable)")]
    public LayerMask damageableLayers;

    [Tooltip("Layers CHỈ phát EFFECT, không damage (Default | Environment)")]
    public LayerMask effectOnlyLayers;

    [Tooltip("Số lượng collider tối đa có thể phát hiện trong 1 lần check")]
    public int maxHitsPerCheck = 15;

    [Header("Hit Behavior")]
    [Tooltip("Nếu bật, hitbox chỉ gây damage 1 lần cho đến khi disable và enable lại")]
    public bool singleHitPerActivation = true;

    [Header("Hit Effects")]
    [Tooltip("Bật/tắt hiệu ứng khi đánh trúng")]
    public bool enableHitEffects = true;

    [Header("Debug Settings")]
    [Tooltip("Bật debug logs (CHỈ dùng khi cần debug, tắt để tăng performance)")]
    public bool enableDebugLogs = false;

    [Header("Visual Debug")]
    [Tooltip("Hiển thị Gizmos để debug hitbox trong Scene view")]
    public bool showDebugGizmos = true;
    [Tooltip("Màu khi hitbox không hoạt động")]
    public Color inactiveColor = new Color(1f, 1f, 0f, 0.5f);
    [Tooltip("Màu khi hitbox đang hoạt động")]
    public Color activeColor = new Color(1f, 0f, 0f, 0.8f);
    [Tooltip("Màu khi hitbox đã đánh trúng và tạm ngưng")]
    public Color exhaustedColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);

    // ===== PRIVATE VARIABLES =====
    private ItemHeld itemHeld; // Reference đến ItemHeld component
    private ItemData itemData; // Dữ liệu của vật phẩm hiện tại
    private PlayerCombat playerCombat; // Reference đến PlayerCombat để gửi command

    // HashSet để tracking các object đã đánh (tránh đánh trùng)
    private HashSet<uint> alreadyHitNetIds = new HashSet<uint>(); // Cho networked objects
    private HashSet<int> alreadyHitInstanceIds = new HashSet<int>(); // Cho local objects

    // Trạng thái hitbox
    private bool isHitboxActive = false; // Hitbox có đang hoạt động không
    private bool hasHitSomething = false; // Đã đánh trúng gì chưa (dùng cho singleHitPerActivation)

    // Buffer và cache để tối ưu performance
    private Collider[] hitBuffer; // Buffer lưu colliders phát hiện được
    private Vector3[] spherePositions; // Cache vị trí các sphere
    private LayerMask combinedLayers; // Kết hợp của damageableLayers và effectOnlyLayers

    // ===== SỬA LỖI TỰ ĐÁNH BẢN THÂN =====
    // Cache GameObject của player sở hữu vũ khí này để tránh tự đánh chính mình
    private GameObject ownerPlayer; // GameObject của player đang cầm vũ khí
    private Transform ownerTransform; // Transform của player để kiểm tra nhanh hơn
    private NetworkIdentity ownerNetIdentity; // NetworkIdentity của player để kiểm tra netId

  
    private Dictionary<int, IMinenable> minableCache = new Dictionary<int, IMinenable>();
    private Dictionary<int, BaseResource> resourceCache = new Dictionary<int, BaseResource>();
    private Dictionary<int, IDamageable> damageableCache = new Dictionary<int, IDamageable>();
    private Dictionary<int, NetworkIdentity> netIdentityCache = new Dictionary<int, NetworkIdentity>();

  
    private void Awake()
    {
        // Giới hạn số lượng sphere và hits để tránh performance issue
        sphereCount = Mathf.Clamp(sphereCount, 1, 50);
        maxHitsPerCheck = Mathf.Clamp(maxHitsPerCheck, 1, 50);

        // Khởi tạo arrays với kích thước cố định
        spherePositions = new Vector3[sphereCount];
        hitBuffer = new Collider[maxHitsPerCheck];

        // Tìm ItemHeld component từ parent
        itemHeld = GetComponentInParent<ItemHeld>();

        // Cập nhật combined layers
        UpdateCombinedLayers();

        // ===== TÌM OWNER PLAYER =====
        // Lưu reference đến player sở hữu vũ khí này
        CacheOwnerPlayer();
    }


   
    private void CacheOwnerPlayer()
    {
        // Tìm PlayerCombat component (parent của vũ khí)
        PlayerCombat combat = GetComponentInParent<PlayerCombat>();
        if (combat != null)
        {
            ownerPlayer = combat.gameObject;
            ownerTransform = combat.transform;
            ownerNetIdentity = combat.GetComponent<NetworkIdentity>();

            if (enableDebugLogs)
            {
                Debug.Log($"[ItemHitBox] Đã cache owner player: {ownerPlayer.name}, NetId: {ownerNetIdentity?.netId}");
            }
        }
        else
        {
            Debug.LogWarning("[ItemHitBox] Không tìm thấy PlayerCombat - không thể xác định owner!");
        }
    }

   
    private void Start()
    {
        if (playerCombat == null)
            playerCombat = GetComponentInParent<PlayerCombat>();

        if (playerCombat == null)
            Debug.LogError($"[ItemHitBox] Không tìm thấy PlayerCombat component");

        // Đảm bảo đã cache owner (phòng trường hợp Awake chưa chạy)
        if (ownerPlayer == null)
        {
            CacheOwnerPlayer();
        }
    }

    
    private void UpdateCombinedLayers()
    {
        combinedLayers = damageableLayers | effectOnlyLayers;
    }

 
    public void SetPlayerCombat(PlayerCombat combat)
    {
        playerCombat = combat;

        // Cập nhật owner player khi set PlayerCombat mới
        if (combat != null)
        {
            ownerPlayer = combat.gameObject;
            ownerTransform = combat.transform;
            ownerNetIdentity = combat.GetComponent<NetworkIdentity>();
        }
    }

    private void UpdateItemData()
    {
        if (itemHeld != null)
        {
            itemData = itemHeld.itemData;
            if (itemData != null && enableDebugLogs)
            {
                Debug.Log($"[ItemHitBox] UpdateItemData: {itemData.itemName} (Loại: {itemData.type})");
            }
        }
        else if (enableDebugLogs)
        {
            Debug.LogWarning("[ItemHitBox] itemHeld là NULL!");
        }
    }

  
    public void EnableHitbox()
    {
        UpdateItemData();
        UpdateCombinedLayers();
        isHitboxActive = true;

        // Clear tất cả tracking
        alreadyHitNetIds.Clear();
        alreadyHitInstanceIds.Clear();
        hasHitSomething = false;

        // Clear component caches khi bật hitbox mới để tránh cache stale data
        ClearComponentCaches();

        if (enableDebugLogs)
            Debug.Log($"[ItemHitBox] Hitbox được bật cho {itemData?.itemName ?? "UNKNOWN"}");
    }

  
    public void DisableHitbox()
    {
        isHitboxActive = false;

        // Clear tracking
        alreadyHitNetIds.Clear();
        alreadyHitInstanceIds.Clear();
        hasHitSomething = false;

        if (enableDebugLogs)
            Debug.Log($"[ItemHitBox] Hitbox bị tắt");
    }

    private void ClearComponentCaches()
    {
        minableCache.Clear();
        resourceCache.Clear();
        damageableCache.Clear();
        netIdentityCache.Clear();
    }

   
    private void FixedUpdate()
    {
        // Không làm gì nếu hitbox không active
        if (!isHitboxActive) return;

        // Nếu bật singleHitPerActivation và đã đánh trúng rồi thì dừng
        if (singleHitPerActivation && hasHitSomething)
        {
            return;
        }

        // Thực hiện phát hiện va chạm
        PerformMultiSphereDetection();
    }

  
    private void PerformMultiSphereDetection()
    {
        // Đảm bảo có itemData
        UpdateItemData();
        if (itemData == null)
        {
            if (enableDebugLogs)
                Debug.LogWarning("[ItemHitBox] itemData là null trong PerformMultiSphereDetection");
            return;
        }

        // Lấy vị trí đầu và cuối của lưỡi kiếm
        Vector3 bladeStart = GetBladeStartPosition();
        Vector3 bladeEnd = GetBladeEndPosition();

        // Tính toán vị trí các sphere dọc theo lưỡi
        for (int i = 0; i < sphereCount; i++)
        {
            // Interpolate từ start đến end
            float t = sphereCount > 1 ? (float)i / (sphereCount - 1) : 0f;
            spherePositions[i] = Vector3.Lerp(bladeStart, bladeEnd, t);
        }

        // Kiểm tra va chạm cho từng sphere
        for (int i = 0; i < sphereCount; i++)
        {
            // Sử dụng OverlapSphereNonAlloc để tránh garbage collection
            int hitCount = Physics.OverlapSphereNonAlloc(
                spherePositions[i],
                sphereRadius,
                hitBuffer,
                combinedLayers,
                QueryTriggerInteraction.Collide
            );

            // Chỉ log nếu bật debug và có hit
            if (hitCount > 0 && enableDebugLogs)
            {
                Debug.Log($"[ItemHitBox] Sphere {i} phát hiện {hitCount} colliders");
            }

            // Xử lý từng collider phát hiện được
            for (int j = 0; j < hitCount; j++)
            {
                if (enableDebugLogs)
                {
                    Debug.Log($"[ItemHitBox] Phát hiện va chạm: {hitBuffer[j].name}, Layer: {LayerMask.LayerToName(hitBuffer[j].gameObject.layer)}");
                }

                ProcessHit(hitBuffer[j], spherePositions[i]);

                // Dừng lại nếu đã đánh trúng và bật chế độ single hit
                if (singleHitPerActivation && hasHitSomething)
                {
                    if (enableDebugLogs)
                        Debug.Log($"[ItemHitBox] Đã đánh trúng 1 lần - hitbox tạm ngưng");
                    return;
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

   
    private bool IsOwnerPlayer(Collider other)
    {
        // Kiểm tra null trước
        if (other == null || ownerPlayer == null)
            return false;

        GameObject hitObject = other.gameObject;

        // ===== KIỂM TRA 1: So sánh trực tiếp GameObject =====
        if (hitObject == ownerPlayer)
        {
            if (enableDebugLogs)
                Debug.Log($"[ItemHitBox] ⛔ BỎ QUA: Đây là chính player đang cầm vũ khí (GameObject match)");
            return true;
        }

        // ===== KIỂM TRA 2: So sánh Transform (nhanh hơn GetComponentInParent) =====
        if (ownerTransform != null)
        {
            Transform current = other.transform;
            // Duyệt lên parent hierarchy để tìm owner
            while (current != null)
            {
                if (current == ownerTransform)
                {
                    if (enableDebugLogs)
                        Debug.Log($"[ItemHitBox] ⛔ BỎ QUA: Đây là child của player đang cầm vũ khí");
                    return true;
                }
                current = current.parent;
            }
        }

        // ===== KIỂM TRA 3: So sánh NetworkIdentity netId =====
        if (ownerNetIdentity != null)
        {
            NetworkIdentity hitNetId = other.GetComponentInParent<NetworkIdentity>();
            if (hitNetId != null && hitNetId.netId == ownerNetIdentity.netId)
            {
                if (enableDebugLogs)
                    Debug.Log($"[ItemHitBox] ⛔ BỎ QUA: Đây là player đang cầm vũ khí (NetId match: {hitNetId.netId})");
                return true;
            }
        }

        // Không phải owner player
        return false;
    }

   
    private void ProcessHit(Collider other, Vector3 hitSpherePosition)
    {
        if (enableDebugLogs)
            Debug.Log($"[ProcessHit] VÀO HÀM XỬ LÝ cho: {other.name}");

        // ===== KIỂM TRA QUAN TRỌNG: BỎ QUA NẾU LÀ CHÍNH PLAYER =====
        if (IsOwnerPlayer(other))
        {
            // Đã log trong IsOwnerPlayer(), không cần log thêm
            return;
        }

        // Tính toán điểm va chạm và hướng normal
        Vector3 hitPoint = other.ClosestPoint(hitSpherePosition);
        Vector3 hitNormal = (other.transform.position - hitSpherePosition).normalized;

        // Kiểm tra layer của object
        int objectLayer = other.gameObject.layer;
        bool isInDamageableLayer = IsLayerInMask(objectLayer, damageableLayers);
        bool isInEffectOnlyLayer = IsLayerInMask(objectLayer, effectOnlyLayers);

        if (enableDebugLogs)
            Debug.Log($"[ProcessHit] Layer check - Damageable: {isInDamageableLayer}, EffectOnly: {isInEffectOnlyLayer}");

        // XỬ LÝ OBJECT CHỈ PHÁT EFFECT (không cần tracking damage)
        if (isInEffectOnlyLayer && !isInDamageableLayer)
        {
            // Kiểm tra đã phát effect cho object này chưa
            int instanceId = other.GetInstanceID();
            if (alreadyHitInstanceIds.Contains(instanceId))
            {
                if (enableDebugLogs)
                    Debug.Log($"[ProcessHit] Đã phát effect cho object này rồi, bỏ qua");
                return;
            }
            alreadyHitInstanceIds.Add(instanceId);

            // Chỉ phát effect, không gây damage
            if (enableHitEffects && HitEffectManager.Instance != null)
            {
                HitEffectManager.Instance.PlayHitEffect(hitPoint, hitNormal, other, other.gameObject);
                if (enableDebugLogs)
                    Debug.Log($"[ItemHitBox] ✅ Phát hiệu ứng cho {other.name} (effect-only)");
            }

            // KHÔNG set hasHitSomething cho effect-only objects
            return;
        }

        // XỬ LÝ DAMAGEABLE OBJECTS
        if (!isInDamageableLayer)
        {
            if (enableDebugLogs)
                Debug.Log($"[ProcessHit] Object không ở damageable layer, bỏ qua");
            return;
        }

        // Phát effect cho damageable objects
        if (enableHitEffects && HitEffectManager.Instance != null)
        {
            HitEffectManager.Instance.PlayHitEffect(hitPoint, hitNormal, other, other.gameObject);
            if (enableDebugLogs)
                Debug.Log($"[ItemHitBox] ✅ Phát hiệu ứng cho {other.name}");
        }

        // Xử lý damage dựa trên loại vật phẩm
        if (itemData.type == ItemType.Tool)
        {
            ProcessToolDamage(other, hitPoint, hitNormal);
        }
        else if (itemData.type == ItemType.Weapon)
        {
            ProcessWeaponDamage(other, hitPoint, hitNormal);
        }
    }

    
    private void ProcessToolDamage(Collider other, Vector3 hitPoint, Vector3 hitNormal)
    {
        int instanceId = other.GetInstanceID();

        // ===== OPTIMIZATION: TRY CACHE FIRST =====
        // Tìm IMinenable component (cache trước để tránh GetComponent nhiều lần)
        if (!minableCache.TryGetValue(instanceId, out IMinenable minable))
        {
            // Chưa có trong cache, tìm và lưu vào cache
            minable = other.GetComponent<IMinenable>();
            if (minable == null)
                minable = other.GetComponentInParent<IMinenable>();

            minableCache[instanceId] = minable; // Lưu vào cache (kể cả null)
        }

        if (minable == null)
        {
            return;
        }

        // Kiểm tra công cụ có phù hợp với resource không (rìu cho cây, cuốc cho đá)
        if (!IsToolValidForResource(itemData.tool.toolType, minable.GetResourceType()))
        {
            return;
        }

        // Lấy damage từ tool
        int dmg = itemData.tool.damage;

        // ===== OPTIMIZATION: TRY CACHE FIRST =====
        // Tìm BaseResource component (cache trước)
        if (!resourceCache.TryGetValue(instanceId, out BaseResource baseResource))
        {
            // Chưa có trong cache, tìm và lưu vào cache
            baseResource = other.GetComponent<BaseResource>();
            if (baseResource == null)
                baseResource = other.GetComponentInParent<BaseResource>();

            resourceCache[instanceId] = baseResource; // Lưu vào cache
        }

        if (baseResource == null)
        {
            return;
        }

        // Kiểm tra đã đánh resource này chưa (dùng UniqueId hash)
        uint uniqueIdHash = (uint)baseResource.UniqueId.GetHashCode();
        if (alreadyHitNetIds.Contains(uniqueIdHash))
        {
            return;
        }
        alreadyHitNetIds.Add(uniqueIdHash);
        hasHitSomething = true; // CHỈ set sau khi xác nhận damage thành công

        // Kiểm tra xem có phải Log không (cần xử lý khác với cây đứng)
        bool isLog = false;
        MyTree myTree = baseResource as MyTree;
        if (myTree != null)
        {
            isLog = (myTree.GetTreeType() == MyTree.TreeType.Log ||
                    myTree.GetTreeType() == MyTree.TreeType.LogHalf);
        }

        // Xử lý damage cho Log (non-persistent)
        if (isLog)
        {
            // ===== OPTIMIZATION: TRY CACHE FIRST =====
            if (!netIdentityCache.TryGetValue(instanceId, out NetworkIdentity targetNetId))
            {
                targetNetId = other.GetComponent<NetworkIdentity>();
                if (targetNetId == null)
                    targetNetId = other.GetComponentInParent<NetworkIdentity>();

                netIdentityCache[instanceId] = targetNetId;
            }

            if (targetNetId != null && playerCombat != null)
            {
                // Gửi command damage cho non-persistent object
                playerCombat.CmdDamageNonPersistent(
                    targetNetId.netId,
                    dmg,
                    hitPoint,
                    hitNormal,
                    itemData.id
                );
            }
        }
        // Xử lý damage cho resource thường (persistent)
        else if (!string.IsNullOrEmpty(baseResource.UniqueId))
        {
            if (playerCombat != null)
            {
                // Gửi command damage cho persistent resource
                playerCombat.CmdDamageResource(
                    baseResource.UniqueId,
                    dmg,
                    itemData.id,
                    minable.GetResourceType()
                );

                if (enableDebugLogs)
                    Debug.Log($"[ProcessToolDamage] ✅ Gửi CmdDamageResource");
            }
        }
    }


   
    private void ProcessWeaponDamage(Collider other, Vector3 hitPoint, Vector3 hitNormal)
    {
        // Lấy damage từ weapon
        int dmg = itemData.weapon.damage;
        if (enableDebugLogs)
            Debug.Log($"[ProcessWeaponDamage] Vũ khí gây damage {dmg}");

        int instanceId = other.GetInstanceID();

        // ===== OPTIMIZATION: TRY CACHE FIRST =====
        // Tìm IDamageable component (cache trước)
        if (!damageableCache.TryGetValue(instanceId, out IDamageable target))
        {
            target = other.GetComponentInParent<IDamageable>();
            if (target == null)
                target = other.GetComponent<IDamageable>();

            damageableCache[instanceId] = target;
        }

        // ===== OPTIMIZATION: TRY CACHE FIRST =====
        // Tìm NetworkIdentity để gửi command qua network (cache trước)
        if (!netIdentityCache.TryGetValue(instanceId, out NetworkIdentity targetNetId))
        {
            targetNetId = other.GetComponentInParent<NetworkIdentity>();
            if (targetNetId == null)
                targetNetId = other.GetComponent<NetworkIdentity>();

            netIdentityCache[instanceId] = targetNetId;
        }

        if (target == null || targetNetId == null)
        {
            if (enableDebugLogs)
                Debug.Log($"[ProcessWeaponDamage] ⚠️ Không có IDamageable hoặc NetworkIdentity");
            return;
        }

        // Kiểm tra đã đánh netId này chưa
        if (alreadyHitNetIds.Contains(targetNetId.netId))
        {
            if (enableDebugLogs)
                Debug.Log($"[ProcessWeaponDamage] Đã đánh netId này rồi, bỏ qua damage");
            return;
        }
        alreadyHitNetIds.Add(targetNetId.netId);
        hasHitSomething = true; // CHỈ set sau khi xác nhận damage thành công

        if (dmg <= 0)
        {
            Debug.LogWarning($"[ProcessWeaponDamage] Damage = 0");
            return;
        }

        if (playerCombat != null)
        {
            // Lấy knockback settings từ itemData
            KnockbackSettings knockback = itemData.GetKnockbackSettings();

            // Gửi command deal damage qua network
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

            if (enableDebugLogs)
                Debug.Log($"[ProcessWeaponDamage] ✅ Gửi CmdDealDamage");

            // Xử lý hit stop effect (tạm dừng game khi đánh trúng)
            if (target.CanTriggerHitStop())
            {
                var hitStop = GetComponentInParent<LocalHitStop>();
                if (hitStop != null && target.IsDead())
                    hitStop.DoHitStop(0.08f);
            }
        }
    }

   
    private bool IsToolValidForResource(ToolType tool, ResourceType resource)
    {
        bool isValid = (tool == ToolType.Axe && resource == ResourceType.Tree) ||
                       (tool == ToolType.Pickaxe && resource == ResourceType.Rock);

        if (enableDebugLogs)
            Debug.Log($"[IsToolValidForResource] {tool} vs {resource} = {isValid}");

        return isValid;
    }

    
    private bool IsLayerInMask(int layer, LayerMask mask)
    {
        return ((1 << layer) & mask.value) != 0;
    }

   
    private void OnDrawGizmos()
    {
        if (!showDebugGizmos) return;

        // Chọn màu dựa trên trạng thái hitbox
        if (isHitboxActive && singleHitPerActivation && hasHitSomething)
        {
            Gizmos.color = exhaustedColor; // Màu xám - đã đánh trúng, tạm ngưng
        }
        else
        {
            Gizmos.color = isHitboxActive ? activeColor : inactiveColor;
        }

        // Lấy vị trí đầu và cuối lưỡi
        Vector3 bladeStart = GetBladeStartPosition();
        Vector3 bladeEnd = GetBladeEndPosition();

        // Vẽ các sphere dọc theo lưỡi
        for (int i = 0; i < sphereCount; i++)
        {
            float t = sphereCount > 1 ? (float)i / (sphereCount - 1) : 0f;
            Vector3 spherePos = Vector3.Lerp(bladeStart, bladeEnd, t);
            Gizmos.DrawWireSphere(spherePos, sphereRadius);
        }

        // Vẽ điểm start (màu xanh lá) và end (màu đỏ)
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(bladeStart, 0.05f);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(bladeEnd, 0.05f);
    }
}