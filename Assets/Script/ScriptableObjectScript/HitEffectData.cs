using UnityEngine;

public enum MaterialType
{
    Stone,      // Đá
    Wood,       // Gỗ
    Metal,      // Kim loại
    Flesh,      // Thịt (máu)
    Water,      // Nước
    Dirt        // Đất
}




[CreateAssetMenu(fileName = "HitEffectData", menuName = "Effects/Hit Effect Data")]
public class HitEffectData : ScriptableObject
{
    [Header("Thông Tin Vật Liệu")]
    public MaterialType materialType;

    [Header("Hiệu Ứng Hình Ảnh")]
    [Tooltip("Kéo particle system prefab vào đây")]
    public ParticleSystem particleEffect;

    [Tooltip("Kéo decal prefab vào đây (vết đạn, vết máu)")]
    public GameObject decalPrefab;

    [Header("Âm Thanh")]
    [Tooltip("Thêm nhiều âm thanh để tạo sự đa dạng")]
    public AudioClip[] hitSounds;

    [Header("Phản Hồi")]
    [Range(0f, 1f)]
    [Tooltip("Độ mạnh rung camera")]
    public float cameraShakeIntensity = 0.1f;

    [Tooltip("Thời gian decal tồn tại (giây)")]
    public float decalLifetime = 10f;
}