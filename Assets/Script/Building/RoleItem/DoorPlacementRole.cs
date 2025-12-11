using UnityEngine;
using System.Linq;
using Mirror;

[CreateAssetMenu(menuName = "Scriptable Objects/PlacementRole/Door")]
public class DoorPlacementRole : DefaultPlacementRole
{
    public float snapRange = 15.0f;
    //sprivate uint previewSocketNetId = 0;

    public override void ComputePreview(
    Vector3 hitPoint,
    float rotY,
    Transform anchor,
    BuildManager manager,
    ItemData item,
    out Vector3 outPos,
    out float outRotY,
    out bool canPlace)
    {
        outPos = hitPoint;
        outRotY = rotY;
        canPlace = false;

        // --- Lấy ray theo chuột ---
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        // --- 1) Tìm socket bằng raycast (ưu tiên trigger collider) ---
        DoorFrameSocket socket = null;
        Transform socketAnchor = null;

        if (Physics.Raycast(ray, out RaycastHit hit, 25f, ~0, QueryTriggerInteraction.Collide))
        {
            socket = hit.collider.GetComponentInParent<DoorFrameSocket>();

            if (socket != null)
            {
                socketAnchor = socket.transform;
                //Debug.Log($"[DOOR] → Found DoorFrameSocket from Raycast: {socket.name}");
            }
        }

        // --- 2) Nếu chưa tìm thấy socket → thử RaycastAll ---
        if (socket == null)
        {
            RaycastHit[] allHits = Physics.RaycastAll(ray, 25f, ~0, QueryTriggerInteraction.Collide)
                                          .OrderBy(h => h.distance).ToArray();

            foreach (var h in allHits)
            {
                var s = h.collider.GetComponentInParent<DoorFrameSocket>();
                if (s != null)
                {
                    socket = s;
                    socketAnchor = s.transform;
                    //Debug.Log($"[DOOR] → Found DoorFrameSocket from RaycastAll: {socket.name}");
                    break;
                }
            }
        }

        // --- 3) Vẫn không có socket? → Không thể snap ---
        if (socket == null)
        {
            //Debug.Log("[DOOR] No DoorFrameSocket detected → Free preview, cannot place.");
            return;
        }

        // --- 4) Tính khoảng cách từ hitPoint đến snapPoint ---
        float dist = Vector3.Distance(hitPoint, socket.snapPoint.position);
        //Debug.Log($"[DOOR] Distance to socket = {dist:F2}");

        if (dist > snapRange)
        {
            //Debug.Log("[DOOR] Too far from socket → cannot snap.");
            return;
        }

        // --- 5) Snap vào socket ---
        outPos = socket.snapPoint.position;
        outRotY = socket.snapPoint.eulerAngles.y;

        //Debug.Log($"[DOOR] Snapped to {socket.snapPoint.position}");

        // --- 6) Kiểm tra xem socket có occupied chưa ---
        if (socket.occupied)
        {
            //Debug.Log("[DOOR] Socket already occupied → cannot place.");
            return;
        }

        // --- 7) Thành công ---
        canPlace = true;
        //Debug.Log("[DOOR] Door CAN be placed!");
    }

    public override void OnServerPlaced(GameObject obj, Transform anchor, ItemData item, BuildManager manager)
    {
        if (!anchor.TryGetComponent(out DoorFrameSocket socket))
            return;

        // Server-side protection
        if (socket.occupied)
        {
            //Debug.LogWarning("[DOOR] SERVER: Tried to place a door on an occupied socket!");
            NetworkServer.Destroy(obj);
            return;
        }

        socket.occupied = true;

        obj.transform.SetParent(socket.snapPoint);
        obj.transform.localPosition = Vector3.zero;
        obj.transform.localRotation = Quaternion.identity;
    }

}
