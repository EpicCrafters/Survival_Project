using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewPoseData", menuName = "Animation/Pose Data")]
public class AnimationPoseDataSO : ScriptableObject
{
    [System.Serializable]
    public class BoneTransformData
    {
        public string bonePath;           // Đường dẫn tương đối: "Spine/Chest/RightArm"
        public Quaternion localRotation;  // Rotation local của xương
        public Vector3 localPosition;     // Position local (tùy chọn)
    }

    public List<BoneTransformData> boneTransforms = new List<BoneTransformData>();
}