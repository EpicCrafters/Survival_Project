using UnityEngine;
using System.Collections.Generic;

namespace BSS.PoseBlender
{
    /// <summary>
    /// An advanced IK controller that can handle multiple IK constraints for character hands and other limbs.
    /// All processing happens in LateUpdate for consistent behavior.
    /// </summary>
    [ExecuteInEditMode]
    public class IKController : MonoBehaviour
    {
        [System.Serializable]
        public class IKConstraint
        {

            public string name = "New Constraint";
            public bool enabled = true;

            [Header("Limb Setup")]
            public TwoBoneIKSolver.HumanoidLimb limbType = TwoBoneIKSolver.HumanoidLimb.RightArm;

            [Header("Manual Bone Setup (Optional)")]
            [Tooltip("If set, will use these bones instead of the humanoid bones")]
            public bool useManualBones = false;
            public Transform rootBone;
            public Transform midBone;
            public Transform endBone;

            [Header("Targets")]
            public Transform targetPosition;
            [Tooltip("Position that controls the bend direction of the limb")]
            public Transform poleTarget;

            [Header("Weights")]
            [Range(0f, 1f)]
            public float positionWeight = 1.0f;
            [Range(0f, 1f)]
            public float rotationWeight = 1.0f;

            [Header("Options")]
            public bool maintainEndRotation = false;

            [HideInInspector]
            public Vector3 lastValidPosition;
            [HideInInspector]
            public Quaternion lastValidRotation;
        }

        [Header("Global Settings")]
        [Range(0f, 1f), Tooltip("Master weight that affects all constraints")]
        public float masterWeight = 1.0f;

        [Header("Animator Reference")]
        [Tooltip("Animator on the character (must be Humanoid for automatic bone setup)")]
        public Animator animator;

        [Header("IK Constraints")]
        [Tooltip("List of IK constraints to apply")]
        public List<IKConstraint> constraints = new List<IKConstraint>();


        // We'll store the current animation state to apply in LateUpdate for AnimatorIK
        private Dictionary<AvatarIKGoal, Vector3> storedIKPositions = new Dictionary<AvatarIKGoal, Vector3>();
        private Dictionary<AvatarIKGoal, Quaternion> storedIKRotations = new Dictionary<AvatarIKGoal, Quaternion>();
        private Dictionary<AvatarIKGoal, float> storedIKPositionWeights = new Dictionary<AvatarIKGoal, float>();
        private Dictionary<AvatarIKGoal, float> storedIKRotationWeights = new Dictionary<AvatarIKGoal, float>();
        private Dictionary<AvatarIKHint, Vector3> storedHintPositions = new Dictionary<AvatarIKHint, Vector3>();
        private Dictionary<AvatarIKHint, float> storedHintWeights = new Dictionary<AvatarIKHint, float>();

        private void Reset()
        {
            // Add default constraints when component is first added
            animator = GetComponent<Animator>();

            if (constraints.Count == 0)
            {
                // Add default left and right hand constraints
                constraints.Add(new IKConstraint
                {
                    name = "Left Hand",
                    limbType = TwoBoneIKSolver.HumanoidLimb.LeftArm
                });

                constraints.Add(new IKConstraint
                {
                    name = "Right Hand",
                    limbType = TwoBoneIKSolver.HumanoidLimb.RightArm
                });
            }
        }

        /// <summary>
        /// Sets the overall master IK weight, clamped between 0 and 1.
        /// </summary>
        /// <param name="weight">The weight value to set for the master IK weight. Expected to be in the range [0,1].</param>
        public void SetMasterWeight(float weight)
        {
            // Clamp the weight between 0 and 1 to ensure valid values.
            masterWeight = Mathf.Clamp01(weight);
        }

        /// <summary>
        /// Looks for an IK constraint with the specified name and sets both its position and rotation weights to the same blend value.
        /// </summary>
        /// <param name="layerName">The name of the IK constraint to update.</param>
        /// <param name="blendWeight">The blend weight to apply to both position and rotation weights. Expected to be in the range [0,1].</param>
        public void SetLayerWeightByName(string layerName, float blendWeight)
        {
            float clampedBlend = Mathf.Clamp01(blendWeight);
            bool found = false;

            // Loop through the constraints list
            foreach (var constraint in constraints)
            {
                // Compare the constraint name (ignoring case)
                if (constraint.name.Equals(layerName, System.StringComparison.OrdinalIgnoreCase))
                {
                    constraint.positionWeight = clampedBlend;
                    constraint.rotationWeight = clampedBlend;
                    found = true;
                }
            }

            if (!found)
            {
                Debug.LogWarning("No IK constraint found with the name: " + layerName);
            }
        }

        /// <summary>
        /// Looks for an IK constraint with the specified name and sets its position and rotation weights independently.
        /// </summary>
        /// <param name="layerName">The name of the IK constraint to update.</param>
        /// <param name="positionBlendWeight">The blend weight to apply to the position weight. Expected to be in the range [0,1].</param>
        /// <param name="rotationBlendWeight">The blend weight to apply to the rotation weight. Expected to be in the range [0,1].</param>
        public void SetLayerWeightByName(string layerName, float positionBlendWeight, float rotationBlendWeight)
        {
            float clampedPosBlend = Mathf.Clamp01(positionBlendWeight);
            float clampedRotBlend = Mathf.Clamp01(rotationBlendWeight);
            bool found = false;

            // Loop through the constraints list
            foreach (var constraint in constraints)
            {
                // Compare the constraint name (ignoring case)
                if (constraint.name.Equals(layerName, System.StringComparison.OrdinalIgnoreCase))
                {
                    constraint.positionWeight = clampedPosBlend;
                    constraint.rotationWeight = clampedRotBlend;
                    found = true;
                }
            }

            if (!found)
            {
                Debug.LogWarning("No IK constraint found with the name: " + layerName);
            }
        }


        private void Start()
        {
            // Initialize last valid positions/rotations
            foreach (var constraint in constraints)
            {
                if (constraint.targetPosition != null)
                {
                    constraint.lastValidPosition = constraint.targetPosition.position;
                    constraint.lastValidRotation = constraint.targetPosition.rotation;
                }
            }

            // Initialize dictionaries for all possible IK goals and hints
            foreach (AvatarIKGoal goal in System.Enum.GetValues(typeof(AvatarIKGoal)))
            {
                storedIKPositions[goal] = Vector3.zero;
                storedIKRotations[goal] = Quaternion.identity;
                storedIKPositionWeights[goal] = 0f;
                storedIKRotationWeights[goal] = 0f;
            }

            foreach (AvatarIKHint hint in System.Enum.GetValues(typeof(AvatarIKHint)))
            {
                storedHintPositions[hint] = Vector3.zero;
                storedHintWeights[hint] = 0f;
            }
        }

        private void LateUpdate()
        {
            ApplyTwoBoneIK();

        }

        private void ApplyTwoBoneIK()
        {
            if (animator == null) return;

            foreach (var constraint in constraints)
            {
                if (!constraint.enabled) continue;

                // Skip if no target is set
                if (constraint.targetPosition == null) continue;

                // Update last valid position/rotation
                constraint.lastValidPosition = constraint.targetPosition.position;
                constraint.lastValidRotation = constraint.targetPosition.rotation;

                // Calculate effective weight
                float effectiveWeight = masterWeight * constraint.positionWeight;
                if (effectiveWeight <= 0) continue;

                // Determine pole position
                Vector3 polePosition;
                if (constraint.poleTarget != null)
                {
                    polePosition = constraint.poleTarget.position;
                }
                else
                {
                    // Create a default pole position if none is specified
                    Transform rootBone = GetRootBone(constraint);
                    if (rootBone == null) continue;

                    // Default pole is perpendicular to the limb direction
                    Vector3 rootToTarget = constraint.targetPosition.position - rootBone.position;
                    Vector3 poleDir = Vector3.Cross(rootToTarget, Vector3.up).normalized;
                    if (poleDir.magnitude < 0.001f)
                        poleDir = Vector3.Cross(rootToTarget, Vector3.right).normalized;

                    polePosition = rootBone.position + poleDir * 0.5f;
                }

                // Apply IK solution
                if (constraint.useManualBones && constraint.rootBone != null &&
                    constraint.midBone != null && constraint.endBone != null)
                {
                    // Use manually specified bones
                    TwoBoneIKSolver.Solve(
                        constraint.rootBone,
                        constraint.midBone,
                        constraint.endBone,
                        constraint.targetPosition.position,
                        polePosition,
                        effectiveWeight,
                        constraint.maintainEndRotation
                    );
                }
                else
                {
                    // Use humanoid bones
                    TwoBoneIKSolver.SolveHumanoidLimb(
                        animator,
                        constraint.limbType,
                        constraint.targetPosition.position,
                        polePosition,
                        effectiveWeight,
                        constraint.maintainEndRotation
                    );
                }

                // Apply rotation if needed and not maintaining end rotation
                if (!constraint.maintainEndRotation && constraint.rotationWeight > 0)
                {
                    // Get the end bone
                    Transform endBone = GetEndBone(constraint);
                    if (endBone != null)
                    {
                        // Blend between current rotation and target rotation
                        float rotBlend = masterWeight * constraint.rotationWeight;
                        endBone.rotation = Quaternion.Slerp(
                            endBone.rotation,
                            constraint.targetPosition.rotation,
                            rotBlend
                        );
                    }
                }
            }
        }

        private void PrepareAnimatorIK()
        {
            if (animator == null) return;

            // Reset all stored weights to zero
            foreach (AvatarIKGoal goal in System.Enum.GetValues(typeof(AvatarIKGoal)))
            {
                storedIKPositionWeights[goal] = 0f;
                storedIKRotationWeights[goal] = 0f;
            }

            foreach (AvatarIKHint hint in System.Enum.GetValues(typeof(AvatarIKHint)))
            {
                storedHintWeights[hint] = 0f;
            }

            foreach (var constraint in constraints)
            {
                if (!constraint.enabled) continue;

                // Skip if no target is set
                if (constraint.targetPosition == null) continue;

                // Update last valid position/rotation
                constraint.lastValidPosition = constraint.targetPosition.position;
                constraint.lastValidRotation = constraint.targetPosition.rotation;

                // Determine which AvatarIKGoal to use based on limb type
                AvatarIKGoal ikGoal = AvatarIKGoal.LeftHand; // Default

                switch (constraint.limbType)
                {
                    case TwoBoneIKSolver.HumanoidLimb.LeftArm:
                        ikGoal = AvatarIKGoal.LeftHand;
                        break;
                    case TwoBoneIKSolver.HumanoidLimb.RightArm:
                        ikGoal = AvatarIKGoal.RightHand;
                        break;
                    case TwoBoneIKSolver.HumanoidLimb.LeftLeg:
                        ikGoal = AvatarIKGoal.LeftFoot;
                        break;
                    case TwoBoneIKSolver.HumanoidLimb.RightLeg:
                        ikGoal = AvatarIKGoal.RightFoot;
                        break;
                }

                // Store IK hint if pole target is available
                if (constraint.poleTarget != null)
                {
                    AvatarIKHint hint = GetCorrespondingHint(ikGoal);
                    storedHintPositions[hint] = constraint.poleTarget.position;
                    storedHintWeights[hint] = constraint.positionWeight * masterWeight;
                }

                // Store IK position and rotation values
                storedIKPositions[ikGoal] = constraint.targetPosition.position;
                storedIKRotations[ikGoal] = constraint.targetPosition.rotation;
                storedIKPositionWeights[ikGoal] = constraint.positionWeight * masterWeight;
                storedIKRotationWeights[ikGoal] = constraint.rotationWeight * masterWeight;
            }
        }

        // Helper to get the corresponding IK hint from goal
        private AvatarIKHint GetCorrespondingHint(AvatarIKGoal goal)
        {
            switch (goal)
            {
                case AvatarIKGoal.LeftHand:
                    return AvatarIKHint.LeftElbow;
                case AvatarIKGoal.RightHand:
                    return AvatarIKHint.RightElbow;
                case AvatarIKGoal.LeftFoot:
                    return AvatarIKHint.LeftKnee;
                case AvatarIKGoal.RightFoot:
                    return AvatarIKHint.RightKnee;
                default:
                    return AvatarIKHint.LeftElbow;
            }
        }

        // Helper to get root bone from constraint
        private Transform GetRootBone(IKConstraint constraint)
        {
            if (constraint.useManualBones && constraint.rootBone != null)
                return constraint.rootBone;

            if (animator == null || !animator.isHuman)
                return null;

            switch (constraint.limbType)
            {
                case TwoBoneIKSolver.HumanoidLimb.LeftArm:
                    return animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                case TwoBoneIKSolver.HumanoidLimb.RightArm:
                    return animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                case TwoBoneIKSolver.HumanoidLimb.LeftLeg:
                    return animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                case TwoBoneIKSolver.HumanoidLimb.RightLeg:
                    return animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
                default:
                    return null;
            }
        }

        // Helper to get end bone from constraint
        private Transform GetEndBone(IKConstraint constraint)
        {
            if (constraint.useManualBones && constraint.endBone != null)
                return constraint.endBone;

            if (animator == null || !animator.isHuman)
                return null;

            switch (constraint.limbType)
            {
                case TwoBoneIKSolver.HumanoidLimb.LeftArm:
                    return animator.GetBoneTransform(HumanBodyBones.LeftHand);
                case TwoBoneIKSolver.HumanoidLimb.RightArm:
                    return animator.GetBoneTransform(HumanBodyBones.RightHand);
                case TwoBoneIKSolver.HumanoidLimb.LeftLeg:
                    return animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                case TwoBoneIKSolver.HumanoidLimb.RightLeg:
                    return animator.GetBoneTransform(HumanBodyBones.RightFoot);
                default:
                    return null;
            }
        }
    }
}