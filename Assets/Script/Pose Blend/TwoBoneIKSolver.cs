using UnityEngine;

namespace BSS.PoseBlender
{
    public static class TwoBoneIKSolver
    {
        /// <summary>
        /// Solves a two-bone IK using an iterative FABRIK approach.
        /// Updates rotations (and computes positions internally) for the root, mid, and end bones.
        /// </summary>
        /// <param name="rootBone">The root bone (e.g., upper arm or thigh).</param>
        /// <param name="midBone">The middle bone (e.g., lower arm or calf).</param>
        /// <param name="endBone">The end bone (e.g., hand or foot).</param>
        /// <param name="targetPosition">The target position for the end bone.</param>
        /// <param name="polePosition">A position defining the pole (bend) direction. (Use Vector3.zero if unused.)</param>
        /// <param name="weight">Blending weight of the IK effect (0–1).</param>
        /// <param name="maintainEndRotation">If true, leaves the end bone’s rotation unchanged.</param>
        public static void Solve(
             Transform rootBone,
             Transform midBone,
             Transform endBone,
             Vector3 targetPosition,
             Vector3 polePosition,
             float weight = 1.0f,
             bool maintainEndRotation = false)
        {
            if (rootBone == null || midBone == null || endBone == null)
                return;

            // Save original rotations and positions
            Quaternion origRootRot = rootBone.rotation;
            Quaternion origMidRot = midBone.rotation;
            Quaternion origEndRot = endBone.rotation;

            Vector3 origRootPos = rootBone.position;
            Vector3 origMidPos = midBone.position;
            Vector3 origEndPos = endBone.position;

            // Compute bone lengths and total chain length
            float boneLength0 = Vector3.Distance(origRootPos, origMidPos);
            float boneLength1 = Vector3.Distance(origMidPos, origEndPos);
            float chainLength = boneLength0 + boneLength1;

            // Store the original directions (from parent to child) for computing rotations later.
            Vector3 jointDir0 = (origMidPos - origRootPos).normalized;
            Vector3 jointDir1 = (origEndPos - origMidPos).normalized;

            // Create an array for the joint positions (we have 3 joints)
            Vector3[] jointPositions = new Vector3[3];
            jointPositions[0] = origRootPos;
            jointPositions[1] = origMidPos;
            jointPositions[2] = origEndPos;

            // Settings for the iterative solver
            int iterations = 50;
            float tolerance = 0.001f;
            float distToTarget = Vector3.Distance(origRootPos, targetPosition);

            // If the target is unreachable, simply stretch the chain in its direction.
            if (distToTarget > chainLength)
            {
                Vector3 direction = (targetPosition - origRootPos).normalized;
                jointPositions[1] = origRootPos + direction * boneLength0;
                jointPositions[2] = jointPositions[1] + direction * boneLength1;
            }
            else
            {
                // Otherwise, perform iterative FABRIK to reposition the joints.
                for (int iter = 0; iter < iterations; iter++)
                {
                    // Backward pass: set the end joint to the target.
                    jointPositions[2] = targetPosition;
                    // Move joints from the end toward the root.
                    for (int i = 1; i >= 0; i--)
                    {
                        float lambda = i == 1 ? boneLength1 : boneLength0;
                        Vector3 dir = (jointPositions[i] - jointPositions[i + 1]).normalized;
                        jointPositions[i] = jointPositions[i + 1] + dir * lambda;
                    }

                    // Forward pass: fix the root and reposition joints from the root toward the end.
                    jointPositions[0] = origRootPos;
                    for (int i = 0; i < 2; i++)
                    {
                        float lambda = i == 0 ? boneLength0 : boneLength1;
                        Vector3 dir = (jointPositions[i + 1] - jointPositions[i]).normalized;
                        jointPositions[i + 1] = jointPositions[i] + dir * lambda;
                    }

                    if (Vector3.Distance(jointPositions[2], targetPosition) < tolerance)
                        break;
                }
            }

            // Apply a pole constraint if a valid pole position is provided.
            if (polePosition != Vector3.zero)
            {
                Vector3 limbAxis = (jointPositions[2] - jointPositions[0]).normalized;
                Vector3 poleDir = (polePosition - jointPositions[0]).normalized;
                Vector3 boneDir = (jointPositions[1] - jointPositions[0]).normalized;
                // Orthonormalize to adjust the bend direction.
                Vector3.OrthoNormalize(ref limbAxis, ref poleDir);
                Vector3.OrthoNormalize(ref limbAxis, ref boneDir);
                Quaternion rot = Quaternion.FromToRotation(boneDir, poleDir);
                jointPositions[1] = rot * (jointPositions[1] - jointPositions[0]) + jointPositions[0];
            }

            // Compute new rotations based on the updated joint positions.
            // For the root bone: rotate its original direction (jointDir0) to match the new direction.
            Vector3 newDir0 = (jointPositions[1] - jointPositions[0]).normalized;
            Quaternion computedRootRot = Quaternion.FromToRotation(jointDir0, newDir0) * origRootRot;
            rootBone.rotation = Quaternion.Slerp(origRootRot, computedRootRot, weight);

            // For the mid bone: rotate its original direction (jointDir1) to the new direction from mid to end.
            Vector3 newDir1 = (jointPositions[2] - jointPositions[1]).normalized;
            Quaternion computedMidRot = Quaternion.FromToRotation(jointDir1, newDir1) * origMidRot;
            midBone.rotation = Quaternion.Slerp(origMidRot, computedMidRot, weight);

            // For the end bone, if we're not maintaining its original rotation,
            // we set it to look toward the target. We use the pole direction as an up hint if available.
            if (!maintainEndRotation)
            {
                Vector3 endDir = (targetPosition - jointPositions[1]).normalized;
                Vector3 upDir = polePosition != Vector3.zero ? (polePosition - jointPositions[1]).normalized : Vector3.up;
                Quaternion computedEndRot = Quaternion.LookRotation(endDir, upDir);
                endBone.rotation = Quaternion.Slerp(origEndRot, computedEndRot, weight);
            }
        }

        /// <summary>
        /// Solves IK for a humanoid limb (arm or leg) using the FABRIK-based two-bone IK.
        /// </summary>
        /// <param name="animator">The humanoid animator.</param>
        /// <param name="limbType">The type of limb (LeftArm, RightArm, LeftLeg, RightLeg).</param>
        /// <param name="targetPosition">The target position.</param>
        /// <param name="polePosition">A position for the pole (bend direction).</param>
        /// <param name="weight">Blending weight of the IK effect (0–1).</param>
        /// <param name="maintainEndRotation">If true, keeps the end bone’s original rotation.</param>
        public static void SolveHumanoidLimb(
                Animator animator,
                HumanoidLimb limbType,
                Vector3 targetPosition,
                Vector3 polePosition,
                float weight = 1.0f,
                bool maintainEndRotation = false)
        {
            if (animator == null || !animator.isHuman)
                return;

            Transform rootBone = null;
            Transform midBone = null;
            Transform endBone = null;

            // Retrieve the proper bone transforms based on the limb type.
            switch (limbType)
            {
                case HumanoidLimb.LeftArm:
                    rootBone = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                    midBone = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                    endBone = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                    break;
                case HumanoidLimb.RightArm:
                    rootBone = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                    midBone = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
                    endBone = animator.GetBoneTransform(HumanBodyBones.RightHand);
                    break;
                case HumanoidLimb.LeftLeg:
                    rootBone = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                    midBone = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
                    endBone = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                    break;
                case HumanoidLimb.RightLeg:
                    rootBone = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
                    midBone = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
                    endBone = animator.GetBoneTransform(HumanBodyBones.RightFoot);
                    break;
            }

            if (rootBone == null || midBone == null || endBone == null)
                return;

            Solve(rootBone, midBone, endBone, targetPosition, polePosition, weight, maintainEndRotation);
        }

        /// <summary>
        /// Enum representing different humanoid limbs.
        /// </summary>
        public enum HumanoidLimb
        {
            LeftArm,
            RightArm,
            LeftLeg,
            RightLeg
        }
    }
}