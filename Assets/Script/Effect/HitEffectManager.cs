using UnityEngine;
using Mirror;
using System.Collections;
using System.Collections.Generic;

public class HitEffectManager : MonoBehaviour
{
    // Singleton – để gọi từ bất kỳ đâu
    public static HitEffectManager Instance { get; private set; }

    [Header("Effect Database")]
    [Tooltip("Kéo tất cả HitEffectData vào đây")]
    public List<HitEffectData> effectDataList;

    [Header("Pooling Settings")]
    public int particlePoolSize = 20; // số lượng particle được pool cho mỗi loại vật liệu

    [Header("Audio Settings")]
    [Range(0f, 1f)]
    public float soundVolume = 0.5f;
    [Range(0f, 1.2f)]
    public float pitchVariation = 0.1f; // Random pitch để tạo sự đa dạng

    [Header("References")]
    public Transform effectsParent;  // nơi chứa particle
    public Transform decalsParent;   // nơi chứa decal

    [Header("Debug")]
    public bool enableDebugLogs = false;

    // Database chuyển MaterialType -> HitEffectData
    private Dictionary<MaterialType, HitEffectData> effectDatabase;

    // Pool particle theo từng MaterialType
    private Dictionary<MaterialType, Queue<ParticleSystem>> particlePool;

    void Awake()
    {
        // Khởi tạo singleton
        if (Instance == null)
        {
            Instance = this;
            Initialize();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Initialize()
    {
        // Tạo dictionary chứa dữ liệu hiệu ứng theo từng loại surface
        effectDatabase = new Dictionary<MaterialType, HitEffectData>();
        foreach (var data in effectDataList)
        {
            if (data != null)
            {
                effectDatabase[data.materialType] = data;
            }
        }

        InitializePools();

        Debug.Log($"[HitEffectManager] Initialized with {effectDataList.Count} materials");
    }

    void InitializePools()
    {
        // Tạo pool cho từng loại material
        particlePool = new Dictionary<MaterialType, Queue<ParticleSystem>>();

        foreach (var kvp in effectDatabase)
        {
            // Nếu loại này không có particle thì bỏ qua
            if (kvp.Value.particleEffect == null) continue;

            particlePool[kvp.Key] = new Queue<ParticleSystem>();

            // Tạo sẵn particle và bỏ vào pool
            for (int i = 0; i < particlePoolSize; i++)
            {
                ParticleSystem ps = Instantiate(kvp.Value.particleEffect, effectsParent);
                ps.gameObject.SetActive(false);
                particlePool[kvp.Key].Enqueue(ps);
            }
        }
    }


    public void PlayHitEffect(Vector3 position, Vector3 normal, Collider collider, GameObject hitObject = null)
    {
        if (collider == null)
        {
            if (enableDebugLogs)
                Debug.LogWarning("[HitEffectManager] Collider is null");
            return;
        }

        // Tự phát hiện loại vật liệu (ground, metal, wood,...)
        MaterialType material = SurfaceDetector.DetectSurface(collider);

        // Kiểm tra xem có custom effect không (ưu tiên override)
        HitEffectData customEffect = SurfaceDetector.DetectCustomEffect(collider);

        if (enableDebugLogs)
            Debug.Log($"[HitEffectManager] Playing hit effect for {material} at {position}");

        // Gọi vào phương thức cũ để xử lý
        PlayHitEffect(position, normal, material, hitObject, customEffect);
    }



    public void PlayHitEffect(Vector3 position, Vector3 normal, MaterialType material,
        GameObject hitObject = null, HitEffectData customEffect = null)
    {
        // Nếu có custom effect → dùng custom
        HitEffectData data = customEffect != null ? customEffect :
                            (effectDatabase.ContainsKey(material) ? effectDatabase[material] : null);

        if (data == null)
        {
            if (enableDebugLogs)
                Debug.LogWarning($"[HitEffectManager] No effect data for {material}");
            return;
        }

        // ⚡ PLAY SOUND FIRST (most important for feedback)
        PlayHitSound(position, data);

        // Particle
        PlayParticleEffect(position, normal, material);

        // Spawn decal nếu có
        if (data.decalPrefab != null)
        {
            SpawnDecal(position, normal, data);
        }

        // Camera shake (nếu hệ thống camera hỗ trợ)
        if (data.cameraShakeIntensity > 0)
        {
            //CameraShake.Instance?.Shake(data.cameraShakeIntensity, 0.2f);
        }
    }

    void PlayParticleEffect(Vector3 position, Vector3 normal, MaterialType material)
    {
        // Nếu không có pool cho loại này hoặc pool hết particle
        if (!particlePool.ContainsKey(material) || particlePool[material].Count == 0)
        {
            if (enableDebugLogs)
                Debug.LogWarning($"[HitEffectManager] No particle in pool for {material}");
            return;
        }

        // Lấy effect từ pool
        ParticleSystem ps = particlePool[material].Dequeue();
        ps.transform.position = position;
        ps.transform.rotation = Quaternion.LookRotation(normal);
        ps.gameObject.SetActive(true);
        ps.Play();

        // Sau khi chạy xong đưa lại vào pool
        StartCoroutine(ReturnParticleToPool(ps, material));
    }

    IEnumerator ReturnParticleToPool(ParticleSystem ps, MaterialType material)
    {
        // Chờ particle chạy hết
        yield return new WaitForSeconds(ps.main.duration + ps.main.startLifetime.constantMax);

        ps.gameObject.SetActive(false);

        // Bỏ lại vào pool
        if (particlePool.ContainsKey(material))
        {
            particlePool[material].Enqueue(ps);
        }
    }

    void SpawnDecal(Vector3 position, Vector3 normal, HitEffectData data)
    {
        // Tạo decal tại vị trí va chạm
        GameObject decal = Instantiate(data.decalPrefab, position, Quaternion.LookRotation(normal), decalsParent);

        // Đẩy decal 1 chút để tránh Z-Fighting
        decal.transform.position += normal * 0.01f;

        // Xoá decal sau thời gian sống
        Destroy(decal, data.decalLifetime);
    }

    /// <summary>
    /// ⚡ FIXED - Now properly plays sounds with error checking
    /// </summary>
    void PlayHitSound(Vector3 position, HitEffectData data)
    {
        // Check if sound array exists and has clips
        if (data.hitSounds == null || data.hitSounds.Length == 0)
        {
            if (enableDebugLogs)
                Debug.LogWarning($"[HitEffectManager] No hit sounds configured for {data.materialType}");
            return;
        }

        // Pick random sound
        AudioClip clip = data.hitSounds[Random.Range(0, data.hitSounds.Length)];

        if (clip == null)
        {
            if (enableDebugLogs)
                Debug.LogWarning($"[HitEffectManager] Sound clip is null for {data.materialType}");
            return;
        }

        // ⚡ CRITICAL FIX: Create temporary GameObject with AudioSource
        GameObject tempAudio = new GameObject($"HitSound_{data.materialType}");
        tempAudio.transform.position = position;

        AudioSource source = tempAudio.AddComponent<AudioSource>();
        source.clip = clip;
        source.volume = soundVolume;
        source.pitch = 1f + Random.Range(-pitchVariation, pitchVariation); // Add pitch variation
        source.spatialBlend = 1f; // Full 3D sound
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 1f;
        source.maxDistance = 50f;
        source.playOnAwake = false;

        source.Play();

        if (enableDebugLogs)
            Debug.Log($"[HitEffectManager] ✅ Playing sound '{clip.name}' at {position} for {data.materialType}");

        // Destroy after clip finishes
        Destroy(tempAudio, clip.length + 0.1f);
    }
}