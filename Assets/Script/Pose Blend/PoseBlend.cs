using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using UnityEngine.UI;


#if UNITY_EDITOR
using UnityEditor;
#endif

namespace BSS.PoseBlender
{
    [ExecuteInEditMode]
    [RequireComponent(typeof(IKController))]
    public class PoseBlender : MonoBehaviour
    {
        /// <summary>
        /// The set of Transforms (bones) that the user has chosen in the Setup step.
        /// </summary>
        [Header("Look-Offset Bone Selection (Base Profile)")]
        public List<Transform> availableLookBones = new List<Transform>();

        [SerializeField, Range(0, 1f)] public float masterWeight = 1f;

        /// <summary>
        /// A “look profile” holds a name, a blend weight, and one BoneOffset per bone in availableLookBones.
        /// </summary>
        [System.Serializable]
        public class LookConfig
        {
            public string configName = "New Profile";

            // boneOffsets[i] corresponds exactly to availableLookBones[i].
            public List<BoneOffset> boneOffsets = new List<BoneOffset>();
        }

        [System.Serializable]
        public struct BoneRotationSettings
        {
            public string boneName;
            // The bone to rotate.
            public Transform bone;
            // Blend weight (0 to 1) for how strongly to apply the recorded rotation.
            [Range(0f, 1f)]
            public float blendWeight;
            // Per-bone rotation offset (applied during recorded pose processing).
            public Vector3 rotationOffset;
        }

        [System.Serializable]
        public class BoneChain
        {
            public string chainName;
            // Chain blend weight (0 to 1) that controls the overall blending for all bones in this chain.
            [Range(0f, 1f)]
            public float blendWeight = 1f;

            public List<BoneRotationSettings> bones = new List<BoneRotationSettings>();
        }

        [System.Serializable]
        public class AnimationOverride
        {
            public string animationName;
            [Range(0f, 1f)]
            public float blendWeight = 1f; // Overall influence
            public AnimationPoseDataSO poseData;
            public List<BoneChain> boneChains = new List<BoneChain>();

        }

        [System.Serializable]
        public struct BoneOffset
        {
            public string boneName;
            public Transform bone;
            public Vector2 xMinMax; // Minimum and maximum rotation for the x-axis.
            public Vector2 yMinMax; // Minimum and maximum rotation for the y-axis.
            public Vector2 zMinMax; // Minimum and maximum rotation for the z-axis.

            // This field will store the computed rotation (for debugging or reference).
            public Vector3 currentRootSpaceRotation;
        }

        [SerializeField] public bool previewInEditor = false;

        // The root transform used when recording the pose.
        public Animator animator;
        public Transform animationRoot;

        public LookConfig lookConfig = new LookConfig();

        [Tooltip("How long (seconds) it takes to blend between two look profiles.")]
        public float profileBlendDuration = 0.5f;

        public bool enableLookAtMode = false;
        public Transform lookAtTarget;
        [Range(0f, 1f)] public float lookAtBlendWeight = 1f;

        // Input offsets (expected between -90 and 90) coming from your player controller.
        [Range(-90, 90)] public float lookVerticalOffset = 0;
        [Range(-90, 90)] public float lookHorizontalOffset = 0;
        [Range(-90, 90)] public float leaningOffset = 0;

        [SerializeField, SerializeReference] public AnimationOverride overlayPose1;
        [SerializeField, SerializeReference] public AnimationOverride overlayPose2;

        // Cached recorded data and bone hierarchy.
        private Dictionary<Transform, List<Transform>> boneChildren = new Dictionary<Transform, List<Transform>>();

        [SerializeField, HideInInspector] private LookConfig currentLookConfig; // Make it serialized

        [SerializeField, HideInInspector] public bool initialized = false;

        // Store the active blend coroutine
        private Coroutine activeBlendCoroutine = null;

        /// <summary>
        /// Sets the look offsets for vertical, horizontal, and leaning angles, clamping each value to the range -90 to 90.
        /// </summary>
        /// <param name="vertical">The vertical look offset.</param>
        /// <param name="horizontal">The horizontal look offset.</param>
        /// <param name="leaning">The leaning offset.</param>
        public void SetLookOffsets(float vertical, float horizontal, float leaning)
        {
            lookVerticalOffset = Mathf.Clamp(vertical, -90f, 90f);
            lookHorizontalOffset = Mathf.Clamp(horizontal, -90f, 90f);
            leaningOffset = Mathf.Clamp(leaning, -90f, 90f);
        }

#if UNITY_EDITOR
        private float lastEditorTime = 0f;

        void OnEnable()
        {
            // Only subscribe in edit mode
            if (!Application.isPlaying)
            {
                lastEditorTime = (float)EditorApplication.timeSinceStartup;
                EditorApplication.update += EditorUpdate;
            }

            RebuildCurrentLookConfig();
        }

        void OnDisable()
        {
            if (!Application.isPlaying)
            {
                EditorApplication.update -= EditorUpdate;
            }
        }
        /// <summary>
        /// Called on each editor update tick when previewing in the editor.
        /// It updates the animator based on elapsed time, processes overlay poses and root rotations,
        /// and repaints the Scene view for immediate visual feedback.
        /// </summary>
        void EditorUpdate()
        {
            if (!previewInEditor)
                return;

            float currentTime = (float)EditorApplication.timeSinceStartup;
            float dt = currentTime - lastEditorTime;
            lastEditorTime = currentTime;

            if (animator)
                animator.Update(dt);
            ProcessOverlayPoses();
            ProcessRootRotation();
            SceneView.RepaintAll();
        }
#endif

        void Start()
        {
            if (Application.isPlaying && !initialized)
            {
                RebuildCurrentLookConfig();
                initialized = true;
            }
        }

        // This LateUpdate will run during play mode (and even in the editor if playing)
        void LateUpdate()
        {
            if (Application.isPlaying)
            {
                if (previewInEditor) previewInEditor = false;

                ProcessOverlayPoses();
                ProcessRootRotation();
            }
        }

        private void ProcessOverlayPoses()
        {
            Quaternion poseRootRotation = animationRoot.rotation;

            // Loop through each overlay pose.
            foreach (var overlay in new[] { overlayPose1, overlayPose2 })
            {
                // Validate the overlay pose data.
                if (overlay == null || overlay.poseData == null)
                    continue;

                // Here you might add interpolation between frames if needed.
                // For simplicity, we'll use the first frame:
                var frame = overlay.poseData;

                // Process each bone chain in this overlay pose.
                foreach (var chain in overlay.boneChains)
                {
                    foreach (var boneSettings in chain.bones)
                    {
                        Transform bone = boneSettings.bone;
                        if (bone == null)
                            continue;

                        // Find the bone data using the relative path.
                        string relativePath = GetRelativePath(animationRoot, bone);
                        var boneData = frame.boneTransforms.Find(b => b.bonePath == relativePath);
                        if (boneData == null)
                            continue;

                        // Apply per-bone offset in local space first
                        Quaternion recordedLocalRotation = boneData.localRotation;
                        Quaternion perBoneOffset = Quaternion.Euler(boneSettings.rotationOffset);
                        recordedLocalRotation = perBoneOffset * recordedLocalRotation;

                        // Then convert to global space
                        Quaternion recordedGlobalRotation = poseRootRotation * recordedLocalRotation;

                        // Compute the effective blend weight.
                        float effectiveBlend = boneSettings.blendWeight * chain.blendWeight * overlay.blendWeight * masterWeight;

                        // Blend the current bone rotation with the overlay pose rotation.
                        bone.rotation = Quaternion.Slerp(bone.rotation, recordedGlobalRotation, effectiveBlend);
                    }
                }
            }
        }


        /// <summary>
        /// Maps an input value (expected in the range -90 to 90) to a target rotation value defined by the given min–max range.
        /// </summary>
        /// <param name="input">The input value (-90 to 90).</param>
        /// <param name="minMax">The target rotation range.</param>
        /// <returns>The mapped rotation value.</returns>
        float MapInputToBoneRotation(float input, Vector2 limits)
        {
            // 'limits.x' is assumed to be the negative limit (a negative value)
            // and 'limits.y' is the positive limit.
            if (input >= 0f)
            {
                float t = input / 90f; // Map [0, 90] to [0, 1]
                return Mathf.Lerp(0f, limits.y, t);
            }
            else
            {
                float t = (-input) / 90f; // Map [0, 90] to [0, 1] (input is negative)
                return Mathf.Lerp(0f, limits.x, t);
            }
        }

        /// <summary>
        /// Applies look-offset rotations to each bone based solely on currentLookConfig.
        /// </summary>
        private void ProcessRootRotation()
        {
            // If nothing to do, bail.
            if (currentLookConfig == null
                || currentLookConfig.boneOffsets == null
                || currentLookConfig.boneOffsets.Count == 0
                || animationRoot == null)
            {
                return;
            }

            // 1) Compute vertical & horizontal
            float vertical = lookVerticalOffset;
            float horizontal = lookHorizontalOffset;

            if (enableLookAtMode && lookAtTarget != null)
            {
                // 1) remember the original/manual offsets
                float origVertical = 0;
                float origHorizontal = 0;

                // 2) compute the raw look-at target angles
                Transform lookOrigin = (availableLookBones != null && availableLookBones.Count > 0)
                                       ? (availableLookBones[availableLookBones.Count - 1] ?? animationRoot)
                                       : animationRoot;
                Vector3 dir = lookAtTarget.position - lookOrigin.position;
                Vector3 localDir = animationRoot.InverseTransformDirection(dir.normalized);

                float targetHorizontal = Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg;
                float targetVertical = Mathf.Asin(-localDir.y) * Mathf.Rad2Deg;

                targetHorizontal = Mathf.Clamp(targetHorizontal, -90f, 90f);
                targetVertical = Mathf.Clamp(targetVertical, -90f, 90f);

                // 3) blend between manual and look-at by lookAtBlendWeight
                vertical = Mathf.Lerp(origVertical, targetVertical, lookAtBlendWeight);
                horizontal = Mathf.Lerp(origHorizontal, targetHorizontal, lookAtBlendWeight);

                // 4) store back into your offsets so the rest of ProcessRootRotation uses them
                lookVerticalOffset = vertical;
                lookHorizontalOffset = horizontal;
            }

            // 2) Loop through each BoneOffset in currentLookConfig
            var boneOffsets = currentLookConfig.boneOffsets;
            for (int i = 0; i < boneOffsets.Count; i++)
            {
                BoneOffset bo = boneOffsets[i];
                Transform bone = bo.bone;
                if (bone == null) continue;

                float mappedX = MapInputToBoneRotation(vertical, bo.xMinMax);
                float mappedY = MapInputToBoneRotation(horizontal, bo.yMinMax);
                float mappedZ = enableLookAtMode
                                ? 0f
                                : MapInputToBoneRotation(-leaningOffset, bo.zMinMax);

                Vector3 targetEuler = new Vector3(mappedX, mappedY, mappedZ) * masterWeight;
                bo.currentRootSpaceRotation = targetEuler;

                Quaternion offsetQ = animationRoot.rotation
                                     * Quaternion.Euler(targetEuler)
                                     * Quaternion.Inverse(animationRoot.rotation);

                Quaternion blendedQ = offsetQ * bone.rotation;

                bone.rotation = blendedQ;
                boneOffsets[i] = bo;
            }
        }


        /// <summary>
        /// Recursively processes a bone and its children to apply blended pose rotations.
        /// For bones found in the provided bone settings map, it computes a blended local rotation 
        /// by interpolating between the current and recorded rotations (with an applied global offset).
        /// </summary>
        /// <param name="bone">The current bone transform to process.</param>
        /// <param name="poseRootRotation">The root rotation of the recorded pose.</param>
        /// <param name="hipDeltaYaw">A yaw adjustment for the hip (unused in the provided snippet but may be relevant).</param>
        /// <param name="globalOffset">A global rotation offset applied to the recorded rotation.</param>
        /// <param name="boneSettingsMap">A mapping from bone transforms to their rotation settings.</param>
        /// <param name="chainBlendMap">A mapping from bone transforms to their chain blend weight values.</param>
        private void ProcessBoneAndChildren(
            Transform bone,
            Quaternion poseRootRotation,
            Quaternion hipDeltaYaw,
            Quaternion globalOffset,
            Dictionary<Transform, BoneRotationSettings> boneSettingsMap,
            Dictionary<Transform, float> chainBlendMap)
        {
            if (bone == null || !boneSettingsMap.ContainsKey(bone))
                return;

            BoneRotationSettings boneSettings = boneSettingsMap[bone];
            string relativePath = GetRelativePath(animationRoot, bone);

            // Use the first overlay pose (overlayPose1) if it’s assigned.
            var boneData = (overlayPose1 != null && overlayPose1.poseData != null)
                    ? overlayPose1.poseData.boneTransforms.Find(b => b.bonePath == relativePath)
                    : null;

            if (boneData != null)
            {
                Quaternion originalLocalRotation = bone.localRotation;
                Quaternion recordedLocalRotation = boneData.localRotation;
                Quaternion perBoneOffset = Quaternion.Euler(boneSettings.rotationOffset);
                recordedLocalRotation = perBoneOffset * recordedLocalRotation;

                recordedLocalRotation = globalOffset * recordedLocalRotation;
                float chainBlend = chainBlendMap.ContainsKey(bone) ? chainBlendMap[bone] : 1.0f;
                float effectiveBlend = boneSettings.blendWeight * chainBlend;
                Quaternion blendedLocalRotation = Quaternion.Slerp(originalLocalRotation, recordedLocalRotation, effectiveBlend);
                bone.localRotation = blendedLocalRotation;
            }

            if (boneChildren.TryGetValue(bone, out List<Transform> children))
            {
                foreach (Transform child in children)
                {
                    ProcessBoneAndChildren(child, poseRootRotation, hipDeltaYaw, globalOffset, boneSettingsMap, chainBlendMap);
                }
            }
        }



        /// <summary>
        /// Computes the relative path (hierarchical name) from the provided root transform to the target transform.
        /// Useful for matching recorded bone data with scene bones.
        /// </summary>
        /// <param name="root">The root transform from which the path should be computed.</param>
        /// <param name="target">The target transform whose relative path is desired.</param>
        /// <returns>The relative path string. Returns an empty string if the target is the root.</returns>
        private string GetRelativePath(Transform root, Transform target)
        {
            if (target == root)
                return "";
            string path = target.name;
            Transform current = target.parent;
            while (current != null && current != root)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }
            return path;
        }

        /// <summary>
        /// Starts a new blend coroutine, stopping any existing one.
        /// </summary>
        /// <param name="overlayName">The name of the overlay pose to blend.</param>
        /// <param name="targetBlend">The target blend weight (0 to 1).</param>
        /// <param name="duration">The duration of the blend in seconds.</param>
        public void BlendOverlay(string overlayName, float targetBlend, float duration)
        {
            // Stop any existing blend coroutine
            if (activeBlendCoroutine != null)
            {
                StopCoroutine(activeBlendCoroutine);
                activeBlendCoroutine = null;
            }

            // Start a new coroutine
            activeBlendCoroutine = StartCoroutine(BlendOverlayPoseCoroutine(overlayName, targetBlend, duration));
        }

        /// <summary>
        /// Starts a new blend coroutine for all overlays, stopping any existing one.
        /// </summary>
        /// <param name="targetBlend">The target blend weight (0 to 1).</param>
        /// <param name="duration">The duration of the blend in seconds.</param>
        public void BlendAllOverlays(float targetBlend, float duration)
        {
            // Stop any existing blend coroutine
            if (activeBlendCoroutine != null)
            {
                StopCoroutine(activeBlendCoroutine);
                activeBlendCoroutine = null;
            }

            // Start a new coroutine
            activeBlendCoroutine = StartCoroutine(BlendAllOverlaysCoroutine(targetBlend, duration));
        }

        /// <summary>
        /// Coroutine that smoothly blends an overlay pose to a target blend weight over time.
        /// </summary>
        /// <param name="overlayName">The name of the overlay pose to blend.</param>
        /// <param name="targetBlend">The target blend weight (0 to 1).</param>
        /// <param name="duration">The duration of the blend in seconds.</param>
        private IEnumerator BlendOverlayPoseCoroutine(string overlayName, float targetBlend, float duration)
        {
            // Find the overlay with the matching name among the two slots
            AnimationOverride targetOverlay = null;

            if (overlayPose1 != null && overlayPose1.animationName == overlayName)
                targetOverlay = overlayPose1;

            else if (overlayPose2 != null && overlayPose2.animationName == overlayName)
                targetOverlay = overlayPose2;

            // If no matching overlay was found, exit
            if (targetOverlay == null)
            {
                Debug.LogWarning($"Overlay pose '{overlayName}' not found.");
                activeBlendCoroutine = null;
                yield break;
            }

            // Store initial blend weight
            float initialBlend = targetOverlay.blendWeight;
            float currentTime = 0f;

            // Clamp target blend to valid range
            targetBlend = Mathf.Clamp01(targetBlend);

            // Blend over time
            while (currentTime < duration)
            {
                currentTime += Time.deltaTime;
                float t = Mathf.Clamp01(currentTime / duration);

                // Linear interpolation between initial and target blend weights
                targetOverlay.blendWeight = Mathf.Lerp(initialBlend, targetBlend, t);

                yield return null;
            }

            // Ensure we end exactly at the target blend
            targetOverlay.blendWeight = targetBlend;
            activeBlendCoroutine = null;
        }

        /// <summary>
        /// Coroutine that blends all overlay poses to a specified target weight over time.
        /// </summary>
        /// <param name="targetBlendWeight">The target blend weight for all overlays.</param>
        /// <param name="duration">The duration of the blend in seconds.</param>
        private System.Collections.IEnumerator BlendAllOverlaysCoroutine(float targetBlendWeight, float duration)
        {
            // Exit early if no overlays exist
            if (overlayPose1 == null && overlayPose2 == null)
            {
                activeBlendCoroutine = null;
                yield break;
            }

            // Store initial blend weights
            Dictionary<AnimationOverride, float> initialBlends = new Dictionary<AnimationOverride, float>();

            foreach (var overlay in new[] { overlayPose1, overlayPose2 })
            {
                initialBlends.Add(overlay, overlay.blendWeight);
            }

            float currentTime = 0f;

            // Clamp target blend to valid range
            targetBlendWeight = Mathf.Clamp01(targetBlendWeight);

            // Blend all overlays over time
            while (currentTime < duration)
            {
                currentTime += Time.deltaTime;
                float t = Mathf.Clamp01(currentTime / duration);

                foreach (var overlay in new[] { overlayPose1, overlayPose2 })
                {
                    overlay.blendWeight = Mathf.Lerp(initialBlends[overlay], targetBlendWeight, t);
                }

                yield return null;
            }

            // Ensure we end exactly at the target blend for all overlays
            foreach (var overlay in new[] { overlayPose1, overlayPose2 })
            {
                overlay.blendWeight = targetBlendWeight;
            }

            activeBlendCoroutine = null;
        }

        /// <summary>
        /// Sets the vertical look offset angle of the character.
        /// This controls the up/down orientation of the character's head and upper body.
        /// </summary>
        /// <param name="offset">The vertical angle in degrees. Positive values look up, negative values look down.</param>
        public void SetVerticalOffset(float offset)
        {
            lookVerticalOffset = offset;
        }

        /// <summary>
        /// Sets the horizontal look offset angle of the character.
        /// This controls the left/right orientation of the character's head and upper body.
        /// </summary>
        /// <param name="offset">The horizontal angle in degrees. Positive values look right, negative values look left.</param>
        public void SetHorizontalOffset(float offset)
        {
            lookHorizontalOffset = offset;
        }

        /// <summary>
        /// Sets the leaning offset angle of the character.
        /// This controls how much the character's body leans to the sides.
        /// </summary>
        /// <param name="offset">The leaning angle in degrees. Positive values lean right, negative values lean left.</param>
        public void SetLeaningOffset(float offset)
        {
            leaningOffset = offset;
        }

        /// <summary>
        /// Builds a single “Base” profile, splitting 90° equally among all selected bones.
        /// Also initializes `currentLookConfig` to match this “Base” exactly.
        /// </summary>
        public void InitializeBaseLookProfile()
        {
            if (availableLookBones == null || availableLookBones.Count == 0)
            {
                Debug.LogWarning("PoseBlenderLite: No bones selected. Cannot initialize base look profile.");
                return;
            }

            float perBoneRange = 90f / availableLookBones.Count;

            LookConfig baseCfg = new LookConfig
            {
                configName = "Profile 1",
                boneOffsets = new List<BoneOffset>()
            };

            foreach (var bone in availableLookBones)
            {
                BoneOffset bo = new BoneOffset
                {
                    boneName = bone.name,
                    bone = bone,
                    xMinMax = new Vector2(-perBoneRange, perBoneRange),
                    yMinMax = new Vector2(-perBoneRange, perBoneRange),
                    zMinMax = new Vector2(-perBoneRange, perBoneRange),
                    currentRootSpaceRotation = Vector3.zero
                };
                baseCfg.boneOffsets.Add(bo);
            }

            lookConfig = baseCfg;

            // ── NEW: clone baseCfg into currentLookConfig
            currentLookConfig = new LookConfig
            {
                configName = baseCfg.configName,
                boneOffsets = new List<BoneOffset>(baseCfg.boneOffsets)
            };
        }


        /// <summary>
        /// After Unity reloads this component, we need to recreate the in‐memory
        /// currentLookConfig so ProcessRootRotation() has something valid to read.
        /// </summary>
        public void RebuildCurrentLookConfig()
        {
            // Copy the single hard-coded lookConfig into currentLookConfig
            if (lookConfig != null && lookConfig.boneOffsets != null)
            {
                // Struct-wise clone of the BoneOffset list
                currentLookConfig = new LookConfig
                {
                    configName = lookConfig.configName,
                    boneOffsets = new List<BoneOffset>(lookConfig.boneOffsets)
                };
            }
            else
            {
                // No valid lookConfig → nothing to apply
                currentLookConfig = null;
            }
        }
    }
}