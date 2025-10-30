//using System.Collections.Generic;
//using UnityEngine;
//using Mirror;

//public class AIPoolManager : MonoBehaviour
//{
//    public static AIPoolManager Instance;

//    public readonly Dictionary<string, Queue<GameObject>> poolDictionary = new();

//    void Awake()
//    {
//        Instance = this;
//    }

//    // ✅ Create a pool for this prefab
//    public void CreatePool(string key, GameObject prefab, int size)
//    {
//        if (poolDictionary.ContainsKey(key)) return;

//        Queue<GameObject> pool = new Queue<GameObject>();
//        for (int i = 0; i < size; i++)
//        {
//            GameObject obj = Instantiate(prefab);
//            obj.name = $"{key}_Pooled_{i}";
//            obj.SetActive(false);
//            pool.Enqueue(obj);
//        }

//        poolDictionary[key] = pool;
//        Debug.Log($"[AIPool] Created pool '{key}' with size {size}");
//    }

//    // ✅ Basic spawn from pool
//    public GameObject SpawnFromPool(string key, Vector3 pos, Quaternion rot)
//    {
//        if (!poolDictionary.ContainsKey(key))
//        {
//            Debug.LogWarning($"⚠️ No pool found for key: {key}. Trying to auto-create...");
//            var prefab = Resources.Load<GameObject>(key);
//            if (prefab == null)
//            {
//                Debug.LogError($"❌ Could not find prefab in Resources with key '{key}'!");
//                return null;
//            }
//            CreatePool(key, prefab, 1);
//        }

//        GameObject obj = poolDictionary[key].Count > 0 ? poolDictionary[key].Dequeue() : null;

//        if (obj == null)
//        {
//            Debug.LogWarning($"⚠️ Pool '{key}' empty! Instantiating new object (temporary).");
//            var prefab = Resources.Load<GameObject>(key);
//            if (prefab == null)
//            {
//                Debug.LogError($"❌ Could not find prefab for key '{key}' during fallback instantiation!");
//                return null;
//            }
//            obj = Instantiate(prefab);
//        }

//        obj.transform.SetPositionAndRotation(pos, rot);
//        obj.SetActive(true);

//        var controller = obj.GetComponent<HFSMController>();
//        if (controller != null)
//        {
//            controller.pooled = false;
//            controller.enabled = true;
//            Debug.Log($"[AIPool] SpawnFromPool: {obj.name}, uniqueId={controller.uniqueId}");
//        }

//        return obj;
//    }

//    // ✅ Return object to pool
//    public void ReturnToPool(string key, GameObject obj)
//    {
//        if (!poolDictionary.ContainsKey(key))
//        {
//            Debug.LogWarning($"[AIPool] No pool found for key: {key}, destroying instead.");
//            Destroy(obj);
//            return;
//        }

//        var controller = obj.GetComponent<HFSMController>();
//        if (controller != null)
//        {
//            // ✅ DON'T clear uniqueId - keep it for next spawn
//            controller.pooled = true;
//            controller.enabled = false;
//            controller.agent.enabled = false;
//            controller.isSleeping = true;
//            Debug.Log($"[AIPool] Returning to pool - uniqueId={controller.uniqueId} (PRESERVED)");
//        }

//        // ✅ UNREGISTER from AISyncManager before pooling
//        var entity = obj.GetComponent<AIEntity>();
//        if (entity != null && AISyncManager.Instance != null)
//        {
//            AISyncManager.Instance.UnregisterAI(entity);
//            Debug.Log($"[AIPool] Unregistered '{obj.name}' from AISyncManager");
//        }

//        obj.SetActive(false);
//        poolDictionary[key].Enqueue(obj);
//        Debug.Log($"[AIPool] Returned '{obj.name}' to pool '{key}'");
//    }

   
   
//}