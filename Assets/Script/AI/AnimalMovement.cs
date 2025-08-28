using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class AnimalMovement : MonoBehaviour
{
    public NavMeshAgent agent; // NavMeshAgent của thú

    public float detectionRange = 30f; // Tầm phát hiện mục tiêu
    public float fleeDistance = 15f;   // Khoảng cách khi bỏ chạy

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();

        // Kiểm tra agent có đang trên NavMesh không, nếu không thì đặt lại vị trí
        if (!agent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            {
                transform.position = hit.position;
            }
            else
            {
                Debug.LogWarning("Animal không nằm trên NavMesh!");
            }
        }
    }

    // Dừng agent
    public void Stop()
    {
        if (!agent.isOnNavMesh) return; // Chỉ dừng khi agent đã trên NavMesh

        agent.isStopped = true;         // Dừng di chuyển
        agent.ResetPath();              // Xóa path hiện tại
    }

    // Bắt đầu trạng thái lang thang (wander)
    public void StartWander()
    {
        if (!agent.isOnNavMesh) return; // Kiểm tra NavMesh

        agent.isStopped = false;
        agent.speed = GetComponent<BaseAnimalAI>().animalData.moveSpeed; // Lấy tốc độ từ AnimalData

        // Tính vị trí ngẫu nhiên trên NavMesh
        Vector3 randomPos = RandomDirection(transform.position, Random.Range(10f, 40f), NavMesh.AllAreas);
        agent.SetDestination(randomPos); // Đặt đích đến
    }

    // Bắt đầu trạng thái bỏ chạy (flee)
    public void StartFlee(Transform target)
    {
        if (target == null || !agent.isOnNavMesh) return;

        agent.isStopped = false;
        agent.speed = GetComponent<BaseAnimalAI>().animalData.fleeSpeed; // Tốc độ bỏ chạy

        Vector3 dir = (transform.position - target.position).normalized; // Hướng bỏ chạy
        Vector3 fleePos = transform.position + dir * fleeDistance;

        if (NavMesh.SamplePosition(fleePos, out NavMeshHit hit, fleeDistance, NavMesh.AllAreas))
            agent.SetDestination(hit.position);
    }

    // Bắt đầu trạng thái đuổi theo (chase)
    public void StartChase(Transform target)
    {
        if (target == null || !agent.isOnNavMesh) return;

        agent.isStopped = false;
        agent.speed = GetComponent<BaseAnimalAI>().animalData.moveSpeed; // Tốc độ chase
        agent.SetDestination(target.position);
    }

    // Cập nhật vị trí đích khi đang chase
    public void UpdateChaseDestination(Transform target, float stopDistance)
    {
        if (target == null || !agent.isOnNavMesh) return;

        Vector3 dir = (transform.position - target.position).normalized;
        Vector3 destination = target.position + dir * stopDistance;

        if (NavMesh.SamplePosition(destination, out NavMeshHit hit, 2f, NavMesh.AllAreas))
            agent.SetDestination(hit.position);
    }

    // Kiểm tra xem agent đã tới đích chưa
    public bool IsAtDestination()
    {
        if (!agent.isOnNavMesh) return true; // Nếu không trên NavMesh thì coi như đã tới

        return !agent.pathPending &&
               agent.remainingDistance <= agent.stoppingDistance &&
               (!agent.hasPath || agent.velocity.sqrMagnitude < 0.1f);
    }

    // Tạo vị trí ngẫu nhiên trong bán kính distance và kiểm tra NavMesh
    private Vector3 RandomDirection(Vector3 origin, float distance, int areaMask)
    {
        Vector2 rnd = Random.insideUnitCircle * distance;
        Vector3 dest = origin + new Vector3(rnd.x, 0f, rnd.y);

        if (NavMesh.SamplePosition(dest, out NavMeshHit hit, distance, areaMask))
            return hit.position;

        return origin; // Nếu không tìm được vị trí trên NavMesh thì giữ nguyên
    }
}
