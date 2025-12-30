using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System;
using Mirror;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Hệ thống blend animation - OPTIMIZED VERSION
/// - Look offset system với network sync tối ưu (SmoothDamp + Client Prediction)
/// - Overlay poses/animations
/// - IK protection
/// </summary>
[DefaultExecutionOrder(300)]
public class PlayableAnimationBlender : NetworkBehaviour
{
    #region Nested Classes and Structs

    [Serializable]
    public enum OverlayType
    {
        StaticPose,
        AnimationClip
    }

    [Serializable]
    public struct BoneOffset
    {
        public string boneName;
        public Transform bone;
        public Vector2 xMinMax;
        public Vector2 yMinMax;
        public Vector2 zMinMax;
        [HideInInspector] public Vector3 currentRootSpaceRotation;
    }

    [Serializable]
    public class BoneRotationSettings
    {
        public string boneName;
        public Transform bone;
        [Range(0f, 1f)] public float blendWeight = 1f;
        public Vector3 rotationOffset = Vector3.zero;
    }

    [Serializable]
    public class BoneChain
    {
        public string chainName;
        [Range(0f, 1f)] public float blendWeight = 1f;
        public List<BoneRotationSettings> bones = new List<BoneRotationSettings>();
    }

    [Serializable]
    public class AnimationOverlay
    {
        public string overlayName;
        public OverlayType overlayType = OverlayType.StaticPose;
        [Range(0f, 1f)] public float blendWeight = 0f;

        [Header("Static Pose Settings")]
        public AnimationPoseDataSO poseData;

        [Header("Animation Clip Settings")]
        public AnimationClip animationClip;
        public float animationSpeed = 1f;
        public bool loopAnimation = false;
        [Range(0f, 1f)] public float normalizedTime = 0f;

        [Header("Bone Selection")]
        public List<BoneChain> boneChains = new List<BoneChain>();

        [Header("Advanced Settings")]
        public Vector3 globalRotationOffset = Vector3.zero;
        public float defaultTransitionTime = 0.3f;

        [HideInInspector] public bool isPlaying = false;
        [HideInInspector] public float playbackTime = 0f;
        [HideInInspector] public AnimationPoseDataSO cachedPoseAtTime;
        [HideInInspector] public bool needsUpdate = false;
    }

    [Serializable]
    public class IKProtectedBone
    {
        public string boneName;
        public Transform bone;
        public bool isProtected = true;
    }

    #endregion

    #region Inspector Fields

    [Header("Setup")]
    [SerializeField] private Animator animator;
    [SerializeField] private Transform animationRoot;
    [SerializeField] private PlayerCombat combat;

    [Header("Look Offset Settings")]
    [SerializeField] private List<BoneOffset> lookBones = new List<BoneOffset>();
    [SerializeField, Range(-90f, 90f)] private float lookVerticalOffset = 0f;
    [SerializeField, Range(-90f, 90f)] private float lookHorizontalOffset = 0f;
    [SerializeField] private bool enableLookOffset = true;

    [Header("Look-At Mode")]
    [SerializeField] private bool enableLookAtMode = false;
    [SerializeField] public Transform lookAtTarget;
    [SerializeField, Range(0f, 1f)] private float lookAtBlendWeight = 1f;

    [Header("Overlay Poses")]
    [SerializeField] private List<AnimationOverlay> overlayPoses = new List<AnimationOverlay>();

    [Header("IK Protection")]
    [SerializeField] private bool enableIKProtection = true;
    [SerializeField] private List<IKProtectedBone> ikProtectedBones = new List<IKProtectedBone>();

    [Header("Execution Settings")]
    [SerializeField, Range(0f, 1f)] private float masterWeight = 1f;
    [SerializeField] private bool previewInEditor = false;

    [Header("Network Settings - OPTIMIZED")]
    [SerializeField] private float networkSendRate = 30f;
    [SerializeField] private bool enableNetworkSmoothing = true;
    [SerializeField, Range(0.1f, 1f)] private float networkSmoothingSpeed = 0.3f;
    [SerializeField] private float positionChangedThreshold = 0.5f;
    [SerializeField] private bool useClientPrediction = true;

    private float currentLookBlend = 0f;

    #endregion

    #region Network Synced Variables - OPTIMIZED

    [SyncVar(hook = nameof(OnLookVerticalChanged))]
    private float networkLookVertical = 0f;

    [SyncVar(hook = nameof(OnLookHorizontalChanged))]
    private float networkLookHorizontal = 0f;

    [SyncVar(hook = nameof(OnLookOffsetEnabledChanged))]
    private bool networkLookOffsetEnabled = true;

    [SyncVar(hook = nameof(OnLookAtModeChanged))]
    private bool networkLookAtEnabled = false;

    [SyncVar]
    private Vector3 networkLookAtTargetPosition = Vector3.zero;

    // Velocity tracking cho client prediction
    private float lookVerticalVelocity = 0f;
    private float lookHorizontalVelocity = 0f;

    // Prediction & smoothing
    private float predictedLookVertical = 0f;
    private float predictedLookHorizontal = 0f;
    private float smoothedLookVertical = 0f;
    private float smoothedLookHorizontal = 0f;

    // Change detection
    private float lastSentVertical = 0f;
    private float lastSentHorizontal = 0f;
    private float nextNetworkSendTime = 0f;
    private float lastSendTime = 0f;

    #endregion

    #region Private Fields - Caching

    private Dictionary<Transform, string> bonePathCache = new Dictionary<Transform, string>();
    private Dictionary<string, Coroutine> activeBlendCoroutines = new Dictionary<string, Coroutine>();
    private Dictionary<Transform, Quaternion> initialBoneRotations = new Dictionary<Transform, Quaternion>();
    private Dictionary<Transform, Quaternion> baseAnimationRotations = new Dictionary<Transform, Quaternion>();
    private Dictionary<Transform, Quaternion> accumulatedOverlayRotations = new Dictionary<Transform, Quaternion>();
    private HashSet<Transform> ikProtectedTransforms = new HashSet<Transform>();

    private Dictionary<string, OverlayNetworkState> overlayNetworkStates = new Dictionary<string, OverlayNetworkState>();

    private struct OverlayNetworkState
    {
        public float weight;
        public float normalizedTime;
        public bool isPlaying;
    }

#if UNITY_EDITOR
    private float lastEditorTime = 0f;
#endif

    #endregion

    #region Unity Lifecycle

    void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (animationRoot == null) animationRoot = animator.transform;
    }

    void Start()
    {
        CacheBonePaths();
        InitializeOverlays();
        StoreInitialBoneRotations();
        CacheIKProtectedBones();

        smoothedLookVertical = lookVerticalOffset;
        smoothedLookHorizontal = lookHorizontalOffset;
        predictedLookVertical = lookVerticalOffset;
        predictedLookHorizontal = lookHorizontalOffset;

        Debug.Log($"[PlayableAnimationBlender] OPTIMIZED - Execution Order: 300");
        Debug.Log($"[PlayableAnimationBlender] LookBones: {lookBones.Count} | Overlays: {overlayPoses.Count} | IK Protected: {ikProtectedTransforms.Count}");
        Debug.Log($"[PlayableAnimationBlender] Network: {(isServer ? "SERVER" : isClient ? "CLIENT" : "OFFLINE")}");
        Debug.Log($"[PlayableAnimationBlender] ✓ OPTIMIZED Network Sync: {networkSendRate}Hz | Prediction: {useClientPrediction} | Smoothing: {enableNetworkSmoothing}");
    }

    private void StoreInitialBoneRotations()
    {
        foreach (var overlay in overlayPoses)
        {
            if (overlay.boneChains == null) continue;

            foreach (var chain in overlay.boneChains)
            {
                if (chain.bones == null) continue;

                foreach (var bone in chain.bones)
                {
                    if (bone.bone != null && !initialBoneRotations.ContainsKey(bone.bone))
                    {
                        initialBoneRotations[bone.bone] = bone.bone.rotation;
                        baseAnimationRotations[bone.bone] = bone.bone.rotation;
                        accumulatedOverlayRotations[bone.bone] = bone.bone.rotation;
                    }
                }
            }
        }
    }

    private void CacheIKProtectedBones()
    {
        ikProtectedTransforms.Clear();

        if (!enableIKProtection)
        {
            Debug.Log("[PlayableAnimationBlender] IK Protection: OFF");
            return;
        }

        int count = 0;
        foreach (var ikBone in ikProtectedBones)
        {
            if (ikBone.bone != null && ikBone.isProtected)
            {
                ikProtectedTransforms.Add(ikBone.bone);
                count++;
            }
        }

        Debug.Log($"[PlayableAnimationBlender] IK Protection: ON - {count} bones protected");
    }

#if UNITY_EDITOR
    void OnEnable()
    {
        if (!Application.isPlaying)
        {
            lastEditorTime = (float)EditorApplication.timeSinceStartup;
            EditorApplication.update += EditorUpdate;
        }
    }

    void OnDisable()
    {
        if (!Application.isPlaying)
        {
            EditorApplication.update -= EditorUpdate;
        }
    }

    void EditorUpdate()
    {
        if (!previewInEditor) return;

        float currentTime = (float)EditorApplication.timeSinceStartup;
        float dt = currentTime - lastEditorTime;
        lastEditorTime = currentTime;

        if (animator) animator.Update(dt);

        UpdateAnimationOverlays(dt);
        ProcessOverlayPoses();
        ProcessLookOffsets();

        SceneView.RepaintAll();
    }
#endif

    void LateUpdate()
    {

        if (combat.isAttacking == true) return;
        if (!Application.isPlaying) return;

        float deltaTime = Time.deltaTime;

        // ===== LOCAL PLAYER =====
        if (isLocalPlayer)
        {
            CaptureBaseAnimationState();
            UpdateAnimationOverlays(deltaTime);
            ProcessOverlayPoses();
            ProcessLookOffsets();

            // Gửi data với change detection
            if (Time.time >= nextNetworkSendTime)
            {
                SendLookDataToServer();
                SendOverlayDataToServer();
                nextNetworkSendTime = Time.time + (1f / networkSendRate);
            }
        }
        // ===== REMOTE PLAYER - OPTIMIZED SMOOTHING =====
        else
        {
            // SmoothDamp với velocity tracking
            if (enableNetworkSmoothing)
            {
                float smoothTime = 1f / networkSmoothingSpeed;

                smoothedLookVertical = Mathf.SmoothDamp(
                    smoothedLookVertical,
                    useClientPrediction ? predictedLookVertical : networkLookVertical,
                    ref lookVerticalVelocity,
                    smoothTime,
                    Mathf.Infinity,
                    deltaTime
                );

                smoothedLookHorizontal = Mathf.SmoothDamp(
                    smoothedLookHorizontal,
                    useClientPrediction ? predictedLookHorizontal : networkLookHorizontal,
                    ref lookHorizontalVelocity,
                    smoothTime,
                    Mathf.Infinity,
                    deltaTime
                );

                // Update prediction
                if (useClientPrediction)
                {
                    predictedLookVertical += lookVerticalVelocity * deltaTime;
                    predictedLookHorizontal += lookHorizontalVelocity * deltaTime;

                    predictedLookVertical = Mathf.Clamp(predictedLookVertical, -90f, 90f);
                    predictedLookHorizontal = Mathf.Clamp(predictedLookHorizontal, -90f, 90f);
                }
            }
            else
            {
                smoothedLookVertical = networkLookVertical;
                smoothedLookHorizontal = networkLookHorizontal;
            }

            ApplyNetworkOverlayStates();
            CaptureBaseAnimationState();
            UpdateAnimationOverlays(deltaTime);
            ProcessOverlayPoses();
            ProcessLookOffsetsFromNetwork();
        }
    }

    private void CaptureBaseAnimationState()
    {
        foreach (var overlay in overlayPoses)
        {
            if (overlay.boneChains == null) continue;

            foreach (var chain in overlay.boneChains)
            {
                if (chain.bones == null) continue;

                foreach (var bone in chain.bones)
                {
                    if (bone.bone != null)
                    {
                        baseAnimationRotations[bone.bone] = bone.bone.rotation;
                    }
                }
            }
        }
    }

    #endregion

    #region Network Synchronization - OPTIMIZED

    private void SendLookDataToServer()
    {
        if (!isLocalPlayer) return;

        // Change detection
        float verticalDelta = Mathf.Abs(lookVerticalOffset - lastSentVertical);
        float horizontalDelta = Mathf.Abs(lookHorizontalOffset - lastSentHorizontal);

        bool hasSignificantChange = verticalDelta > positionChangedThreshold ||
                                    horizontalDelta > positionChangedThreshold;
        bool forceSend = Time.time >= nextNetworkSendTime + 0.5f;

        if (!hasSignificantChange && !forceSend)
            return;

        // Calculate velocity
        float deltaTime = Time.time - lastSendTime;
        if (deltaTime > 0)
        {
            lookVerticalVelocity = (lookVerticalOffset - lastSentVertical) / deltaTime;
            lookHorizontalVelocity = (lookHorizontalOffset - lastSentHorizontal) / deltaTime;
        }

        lastSentVertical = lookVerticalOffset;
        lastSentHorizontal = lookHorizontalOffset;
        lastSendTime = Time.time;

        if (isServer)
        {
            networkLookVertical = lookVerticalOffset;
            networkLookHorizontal = lookHorizontalOffset;
            networkLookOffsetEnabled = enableLookOffset;
            networkLookAtEnabled = enableLookAtMode;

            if (lookAtTarget != null)
            {
                networkLookAtTargetPosition = lookAtTarget.position;
            }

            // Broadcast velocity
            RpcBroadcastLookVelocity(lookVerticalVelocity, lookHorizontalVelocity);
        }
        else if (isClient)
        {
            Vector3 targetPos = lookAtTarget != null ? lookAtTarget.position : Vector3.zero;

            CmdUpdateLookOffsetFast(
                lookVerticalOffset,
                lookHorizontalOffset,
                lookVerticalVelocity,
                lookHorizontalVelocity,
                enableLookOffset,
                enableLookAtMode,
                targetPos
            );
        }
    }

    [Command]
    private void CmdUpdateLookOffsetFast(
        float vertical,
        float horizontal,
        float verticalVel,
        float horizontalVel,
        bool enabled,
        bool lookAtEnabled,
        Vector3 lookAtPos)
    {
        networkLookVertical = vertical;
        networkLookHorizontal = horizontal;
        networkLookOffsetEnabled = enabled;
        networkLookAtEnabled = lookAtEnabled;
        networkLookAtTargetPosition = lookAtPos;

        RpcBroadcastLookVelocity(verticalVel, horizontalVel);
    }

    [ClientRpc]
    private void RpcBroadcastLookVelocity(float verticalVel, float horizontalVel)
    {
        if (isLocalPlayer) return;

        lookVerticalVelocity = verticalVel;
        lookHorizontalVelocity = horizontalVel;
    }

    private void OnLookVerticalChanged(float oldValue, float newValue)
    {
        if (isLocalPlayer) return;

        if (useClientPrediction && enableNetworkSmoothing)
        {
            predictedLookVertical = newValue + (lookVerticalVelocity * Time.deltaTime);
            smoothedLookVertical = newValue;
        }
        else
        {
            smoothedLookVertical = newValue;
            predictedLookVertical = newValue;
        }
    }

    private void OnLookHorizontalChanged(float oldValue, float newValue)
    {
        if (isLocalPlayer) return;

        if (useClientPrediction && enableNetworkSmoothing)
        {
            predictedLookHorizontal = newValue + (lookHorizontalVelocity * Time.deltaTime);
            smoothedLookHorizontal = newValue;
        }
        else
        {
            smoothedLookHorizontal = newValue;
            predictedLookHorizontal = newValue;
        }
    }

    private void OnLookOffsetEnabledChanged(bool oldValue, bool newValue)
    {
        if (!isLocalPlayer)
        {
            enableLookOffset = newValue;
        }
    }

    private void OnLookAtModeChanged(bool oldValue, bool newValue)
    {
        if (!isLocalPlayer)
        {
            enableLookAtMode = newValue;
        }
    }

    #endregion

    #region Network Synchronization - OVERLAY

    private void SendOverlayDataToServer()
    {
        if (!isLocalPlayer) return;

        foreach (var overlay in overlayPoses)
        {
            if (overlay == null) continue;

            if (isServer)
            {
                RpcUpdateOverlayState(overlay.overlayName, overlay.blendWeight, overlay.normalizedTime, overlay.isPlaying);
            }
            else if (isClient)
            {
                CmdUpdateOverlayState(overlay.overlayName, overlay.blendWeight, overlay.normalizedTime, overlay.isPlaying);
            }
        }
    }

    [Command]
    private void CmdUpdateOverlayState(string overlayName, float weight, float normalizedTime, bool isPlaying)
    {
        RpcUpdateOverlayState(overlayName, weight, normalizedTime, isPlaying);
    }

    [ClientRpc]
    private void RpcUpdateOverlayState(string overlayName, float weight, float normalizedTime, bool isPlaying)
    {
        if (isLocalPlayer) return;

        overlayNetworkStates[overlayName] = new OverlayNetworkState
        {
            weight = weight,
            normalizedTime = normalizedTime,
            isPlaying = isPlaying
        };
    }

    private void ApplyNetworkOverlayStates()
    {
        foreach (var kvp in overlayNetworkStates)
        {
            var overlay = overlayPoses.Find(o => o.overlayName == kvp.Key);
            if (overlay == null) continue;

            var state = kvp.Value;

            if (enableNetworkSmoothing)
            {
                overlay.blendWeight = Mathf.Lerp(overlay.blendWeight, state.weight, networkSmoothingSpeed);
            }
            else
            {
                overlay.blendWeight = state.weight;
            }

            overlay.normalizedTime = state.normalizedTime;
            overlay.isPlaying = state.isPlaying;
            overlay.needsUpdate = true;
        }
    }

    #endregion

    #region Initialization

    private void InitializeOverlays()
    {
        foreach (var overlay in overlayPoses)
        {
            if (overlay.overlayType == OverlayType.AnimationClip && overlay.animationClip != null)
            {
                overlay.needsUpdate = true;
            }
        }
    }

    #endregion

    #region Animation Overlay Processing

    private void UpdateAnimationOverlays(float deltaTime)
    {
        foreach (var overlay in overlayPoses)
        {
            if (overlay.overlayType == OverlayType.AnimationClip && overlay.animationClip != null)
            {
                if (overlay.blendWeight > 0f || overlay.needsUpdate)
                {
                    if (overlay.isPlaying)
                    {
                        overlay.playbackTime += deltaTime * overlay.animationSpeed;
                        float clipLength = overlay.animationClip.length;

                        if (overlay.loopAnimation)
                        {
                            overlay.playbackTime = overlay.playbackTime % clipLength;
                        }
                        else if (overlay.playbackTime >= clipLength)
                        {
                            overlay.playbackTime = clipLength;
                            overlay.isPlaying = false;
                        }

                        overlay.normalizedTime = overlay.playbackTime / clipLength;
                    }
                    else
                    {
                        overlay.playbackTime = overlay.normalizedTime * overlay.animationClip.length;
                    }

                    overlay.cachedPoseAtTime = SampleAnimationClipAtTime(overlay.animationClip, overlay.playbackTime, overlay);
                    overlay.needsUpdate = false;
                }
            }
        }
    }

    private AnimationPoseDataSO SampleAnimationClipAtTime(AnimationClip clip, float time, AnimationOverlay overlay)
    {
        if (clip == null) return null;

        GameObject tempGO = new GameObject("TempAnimSample");
        tempGO.transform.SetParent(transform);
        tempGO.transform.localPosition = Vector3.zero;
        tempGO.transform.localRotation = Quaternion.identity;

        Dictionary<Transform, Transform> boneMapping = new Dictionary<Transform, Transform>();
        CopyHierarchy(animationRoot, tempGO.transform, boneMapping);

        clip.SampleAnimation(tempGO, time);

        AnimationPoseDataSO poseData = ScriptableObject.CreateInstance<AnimationPoseDataSO>();
        Quaternion rootInverse = Quaternion.Inverse(animationRoot.rotation);

        foreach (var chain in overlay.boneChains)
        {
            if (chain.bones == null) continue;

            foreach (var bone in chain.bones)
            {
                if (bone.bone != null && boneMapping.ContainsKey(bone.bone))
                {
                    Transform sampledBone = boneMapping[bone.bone];

                    Quaternion localRotation = (bone.bone == animationRoot)
                        ? sampledBone.rotation
                        : rootInverse * sampledBone.rotation;

                    var boneData = new AnimationPoseDataSO.BoneTransformData
                    {
                        bonePath = GetBonePath(bone.bone),
                        localRotation = localRotation,
                        localPosition = sampledBone.localPosition
                    };
                    poseData.boneTransforms.Add(boneData);
                }
            }
        }

        DestroyImmediate(tempGO);
        return poseData;
    }

    private void CopyHierarchy(Transform source, Transform destination, Dictionary<Transform, Transform> mapping)
    {
        mapping[source] = destination;

        foreach (Transform child in source)
        {
            GameObject childCopy = new GameObject(child.name);
            childCopy.transform.SetParent(destination);
            childCopy.transform.localPosition = child.localPosition;
            childCopy.transform.localRotation = child.localRotation;
            childCopy.transform.localScale = child.localScale;

            CopyHierarchy(child, childCopy.transform, mapping);
        }
    }

    #endregion

    #region Look Offset Processing - OPTIMIZED

    private float MapInputToBoneRotation(float input, Vector2 limits)
    {
        if (input >= 0f)
        {
            float t = input / 90f;
            return Mathf.Lerp(0f, limits.y, t);
        }
        else
        {
            float t = (-input) / 90f;
            return Mathf.Lerp(0f, limits.x, t);
        }
    }

    private void ProcessLookOffsets()
    {
        if (!enableLookOffset || lookBones == null || lookBones.Count == 0 || animationRoot == null)
            return;

        float vertical = lookVerticalOffset;
        float horizontal = lookHorizontalOffset;

        // Look-At Mode
        if (enableLookAtMode && lookAtTarget != null)
        {
            Transform lookOrigin = (lookBones.Count > 0 && lookBones[lookBones.Count - 1].bone != null)
                ? lookBones[lookBones.Count - 1].bone
                : animationRoot;

            Vector3 direction = lookAtTarget.position - lookOrigin.position;
            float forwardDot = Vector3.Dot(animationRoot.forward, direction.normalized);

            float blendSpeed = 5f;
            float targetBlend = forwardDot > 0f ? 1f : 0f;
            currentLookBlend = Mathf.MoveTowards(currentLookBlend, targetBlend, blendSpeed * Time.deltaTime);

            if (currentLookBlend > 0f)
            {
                Vector3 localDir = animationRoot.InverseTransformDirection(direction.normalized);

                float targetHorizontal = Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg;
                float targetVertical = Mathf.Asin(-localDir.y) * Mathf.Rad2Deg;

                targetHorizontal = Mathf.Clamp(targetHorizontal, -90f, 90f);
                targetVertical = Mathf.Clamp(targetVertical, -90f, 90f);

                vertical = Mathf.Lerp(lookVerticalOffset, targetVertical, lookAtBlendWeight * currentLookBlend);
                horizontal = Mathf.Lerp(lookHorizontalOffset, targetHorizontal, lookAtBlendWeight * currentLookBlend);

                lookVerticalOffset = vertical;
                lookHorizontalOffset = horizontal;
            }
        }

        ApplyLookOffsetsToBonesInternal(vertical, horizontal);
    }

    private void ProcessLookOffsetsFromNetwork()
    {
        if (!networkLookOffsetEnabled || lookBones == null || lookBones.Count == 0 || animationRoot == null)
            return;

        ApplyLookOffsetsToBonesInternal(smoothedLookVertical, smoothedLookHorizontal);
    }

    private void ApplyLookOffsetsToBonesInternal(float vertical, float horizontal)
    {
        for (int i = 0; i < lookBones.Count; i++)
        {
            BoneOffset bo = lookBones[i];
            if (bo.bone == null) continue;

            if (enableIKProtection && ikProtectedTransforms.Contains(bo.bone))
                continue;

            float mappedX = MapInputToBoneRotation(vertical, bo.xMinMax);
            float mappedY = MapInputToBoneRotation(horizontal, bo.yMinMax);

            Vector3 targetEuler = new Vector3(mappedX, mappedY) * masterWeight;
            bo.currentRootSpaceRotation = targetEuler;

            Quaternion offsetRotation = animationRoot.rotation * Quaternion.Euler(targetEuler) * Quaternion.Inverse(animationRoot.rotation);
            bo.bone.rotation = offsetRotation * bo.bone.rotation;

            lookBones[i] = bo;
        }
    }

    #endregion

    #region Overlay Pose Processing

    private void ProcessOverlayPoses()
    {
        if (overlayPoses == null || overlayPoses.Count == 0 || animationRoot == null)
            return;

        Quaternion poseRootRotation = animationRoot.rotation;

        foreach (var kvp in baseAnimationRotations)
        {
            if (accumulatedOverlayRotations.ContainsKey(kvp.Key))
            {
                accumulatedOverlayRotations[kvp.Key] = kvp.Value;
            }
        }

        foreach (var overlay in overlayPoses)
        {
            if (overlay.blendWeight <= 0f) continue;

            AnimationPoseDataSO currentPoseData = null;

            if (overlay.overlayType == OverlayType.StaticPose)
            {
                currentPoseData = overlay.poseData;
            }
            else if (overlay.overlayType == OverlayType.AnimationClip)
            {
                currentPoseData = overlay.cachedPoseAtTime;
            }

            if (currentPoseData == null) continue;

            Quaternion globalOffset = Quaternion.Euler(overlay.globalRotationOffset);

            foreach (var chain in overlay.boneChains)
            {
                if (chain.bones == null || chain.blendWeight <= 0f) continue;

                foreach (var boneSetting in chain.bones)
                {
                    Transform bone = boneSetting.bone;
                    if (bone == null) continue;

                    if (enableIKProtection && ikProtectedTransforms.Contains(bone))
                        continue;

                    Quaternion currentRotation = accumulatedOverlayRotations.ContainsKey(bone)
                        ? accumulatedOverlayRotations[bone]
                        : bone.rotation;

                    string bonePath = GetBonePath(bone);
                    var boneData = currentPoseData.boneTransforms.Find(b => b.bonePath == bonePath);
                    if (boneData == null) continue;

                    Quaternion recordedLocalRotation = boneData.localRotation;

                    if (boneSetting.rotationOffset != Vector3.zero)
                        recordedLocalRotation = Quaternion.Euler(boneSetting.rotationOffset) * recordedLocalRotation;

                    recordedLocalRotation = globalOffset * recordedLocalRotation;
                    Quaternion recordedGlobalRotation = poseRootRotation * recordedLocalRotation;

                    float effectiveBlend = boneSetting.blendWeight * chain.blendWeight * overlay.blendWeight * masterWeight;

                    Quaternion newRotation = Quaternion.Slerp(currentRotation, recordedGlobalRotation, effectiveBlend);

                    bone.rotation = newRotation;

                    accumulatedOverlayRotations[bone] = newRotation;

                    if (boneData.localPosition != Vector3.zero)
                    {
                        bone.localPosition = Vector3.Lerp(bone.localPosition, boneData.localPosition, effectiveBlend);
                    }
                }
            }
        }
    }

    #endregion

    #region Utility Methods

    private string GetBonePath(Transform bone)
    {
        if (bonePathCache.TryGetValue(bone, out string cachedPath))
            return cachedPath;

        if (bone == animationRoot) return "";

        string path = bone.name;
        Transform current = bone.parent;

        while (current != null && current != animationRoot)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        bonePathCache[bone] = path;
        return path;
    }

    private void CacheBonePaths()
    {
        bonePathCache.Clear();

        foreach (var overlay in overlayPoses)
        {
            if (overlay.boneChains == null) continue;

            foreach (var chain in overlay.boneChains)
            {
                if (chain.bones == null) continue;

                foreach (var bone in chain.bones)
                {
                    if (bone.bone != null)
                        GetBonePath(bone.bone);
                }
            }
        }
    }

    #endregion

    #region Public API - IK Protection

    public void SetIKProtection(bool enabled)
    {
        enableIKProtection = enabled;
        Debug.Log($"[PlayableAnimationBlender] IK Protection: {(enabled ? "ON" : "OFF")}");
    }

    public void AddIKProtectedBone(Transform bone, string boneName = "")
    {
        if (bone == null) return;

        if (!ikProtectedTransforms.Contains(bone))
        {
            ikProtectedTransforms.Add(bone);

            var ikBone = new IKProtectedBone
            {
                boneName = string.IsNullOrEmpty(boneName) ? bone.name : boneName,
                bone = bone,
                isProtected = true
            };
            ikProtectedBones.Add(ikBone);

            Debug.Log($"[PlayableAnimationBlender] ✓ Added IK protection: {ikBone.boneName}");
        }
    }

    public void RemoveIKProtectedBone(Transform bone)
    {
        if (bone == null) return;

        if (ikProtectedTransforms.Contains(bone))
        {
            ikProtectedTransforms.Remove(bone);
            ikProtectedBones.RemoveAll(b => b.bone == bone);
            Debug.Log($"[PlayableAnimationBlender] Removed IK protection: {bone.name}");
        }
    }

    public void ClearIKProtection()
    {
        ikProtectedTransforms.Clear();
        ikProtectedBones.Clear();
        Debug.Log("[PlayableAnimationBlender] Cleared all IK protection");
    }

    public void SetBoneProtection(Transform bone, bool isProtected)
    {
        if (bone == null) return;

        var ikBone = ikProtectedBones.Find(b => b.bone == bone);
        if (ikBone != null)
        {
            ikBone.isProtected = isProtected;
            CacheIKProtectedBones();
        }
    }

    [ContextMenu("Refresh IK Protection Cache")]
    public void RefreshIKProtectionCache()
    {
        CacheIKProtectedBones();
    }

    #endregion

    #region Public API - Overlay Control

    public void PlayOverlay(string overlayName, float transitionTime = -1f)
    {
        var overlay = overlayPoses.Find(o => o.overlayName == overlayName);
        if (overlay == null)
        {
            Debug.LogWarning($"[PlayableAnimationBlender] Overlay '{overlayName}' not found!");
            return;
        }

        if (transitionTime < 0) transitionTime = overlay.defaultTransitionTime;

        if (overlay.overlayType == OverlayType.AnimationClip)
        {
            overlay.isPlaying = true;
            overlay.playbackTime = 0f;
            overlay.normalizedTime = 0f;
            overlay.needsUpdate = true;
        }

        BlendOverlay(overlayName, 1f, transitionTime);
    }

    public void StopOverlay(string overlayName, float transitionTime = -1f)
    {
        var overlay = overlayPoses.Find(o => o.overlayName == overlayName);
        if (overlay == null) return;

        if (transitionTime < 0) transitionTime = overlay.defaultTransitionTime;

        overlay.isPlaying = false;
        BlendOverlay(overlayName, 0f, transitionTime);
    }

    public void BlendOverlay(string overlayName, float targetWeight, float duration = 0.3f)
    {
        var overlay = overlayPoses.Find(o => o.overlayName == overlayName);
        if (overlay == null)
        {
            Debug.LogWarning($"[PlayableAnimationBlender] Overlay '{overlayName}' not found!");
            return;
        }

        if (activeBlendCoroutines.ContainsKey(overlayName))
        {
            StopCoroutine(activeBlendCoroutines[overlayName]);
            activeBlendCoroutines.Remove(overlayName);
        }

        var coroutine = StartCoroutine(BlendOverlayCoroutine(overlay, targetWeight, duration));
        activeBlendCoroutines[overlayName] = coroutine;
    }

    public void SetOverlayWeight(string overlayName, float weight)
    {
        var overlay = overlayPoses.Find(o => o.overlayName == overlayName);
        if (overlay != null)
        {
            overlay.blendWeight = Mathf.Clamp01(weight);
            overlay.needsUpdate = true;
        }
    }

    public void SetOverlayPlaybackTime(string overlayName, float normalizedTime)
    {
        var overlay = overlayPoses.Find(o => o.overlayName == overlayName);
        if (overlay != null && overlay.overlayType == OverlayType.AnimationClip && overlay.animationClip != null)
        {
            overlay.normalizedTime = Mathf.Clamp01(normalizedTime);
            overlay.playbackTime = overlay.normalizedTime * overlay.animationClip.length;
            overlay.needsUpdate = true;
        }
    }

    private IEnumerator BlendOverlayCoroutine(AnimationOverlay overlay, float targetWeight, float duration)
    {
        float startWeight = overlay.blendWeight;
        float elapsed = 0f;
        targetWeight = Mathf.Clamp01(targetWeight);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            overlay.blendWeight = Mathf.Lerp(startWeight, targetWeight, t);
            overlay.needsUpdate = true;
            yield return null;
        }

        overlay.blendWeight = targetWeight;
        activeBlendCoroutines.Remove(overlay.overlayName);
    }

    #endregion

    #region Public API - Look Offset Control

    public void SetLookVerticalOffset(float offset)
    {
        lookVerticalOffset = Mathf.Clamp(offset, -90f, 90f);
    }

    public void SetLookHorizontalOffset(float offset)
    {
        lookHorizontalOffset = Mathf.Clamp(offset, -90f, 90f);
    }

    public void SetLookOffset(float vertical, float horizontal)
    {
        lookVerticalOffset = Mathf.Clamp(vertical, -90f, 90f);
        lookHorizontalOffset = Mathf.Clamp(horizontal, -90f, 90f);
    }

    public void SetLookOffsetEnabled(bool enabled)
    {
        enableLookOffset = enabled;
    }

    public void SetLookAtMode(bool enabled)
    {
        enableLookAtMode = enabled;
    }

    public void SetLookAtTarget(Transform target)
    {
        lookAtTarget = target;
    }

    public void SetLookAtBlendWeight(float weight)
    {
        lookAtBlendWeight = Mathf.Clamp01(weight);
    }

    #endregion

    #region Public API - Pose Recording

    public AnimationPoseDataSO RecordCurrentPose(string poseName = "NewPose")
    {
        AnimationPoseDataSO poseData = ScriptableObject.CreateInstance<AnimationPoseDataSO>();
        poseData.name = poseName;

        HashSet<Transform> recordedBones = new HashSet<Transform>();
        Quaternion rootInverse = Quaternion.Inverse(animationRoot.rotation);

        foreach (var overlay in overlayPoses)
        {
            if (overlay.boneChains == null) continue;

            foreach (var chain in overlay.boneChains)
            {
                if (chain.bones == null) continue;

                foreach (var bone in chain.bones)
                {
                    if (bone.bone != null && !recordedBones.Contains(bone.bone))
                    {
                        Quaternion localRotation = (bone.bone == animationRoot)
                            ? bone.bone.rotation
                            : rootInverse * bone.bone.rotation;

                        var boneData = new AnimationPoseDataSO.BoneTransformData
                        {
                            bonePath = GetBonePath(bone.bone),
                            localRotation = localRotation,
                            localPosition = bone.bone.localPosition
                        };
                        poseData.boneTransforms.Add(boneData);
                        recordedBones.Add(bone.bone);
                    }
                }
            }
        }

        Debug.Log($"[PlayableAnimationBlender] Recorded {poseData.boneTransforms.Count} bones in pose '{poseName}'");
        return poseData;
    }

    #endregion

    #region Look Bone Initialization

    [ContextMenu("Initialize Look Bone Ranges")]
    public void InitializeBaseLookProfile()
    {
        if (lookBones == null || lookBones.Count == 0)
        {
            Debug.LogWarning("[PlayableAnimationBlender] No look bones configured.");
            return;
        }

        float perBoneRange = 90f / lookBones.Count;

        for (int i = 0; i < lookBones.Count; i++)
        {
            BoneOffset bo = lookBones[i];
            bo.xMinMax = new Vector2(-perBoneRange, perBoneRange);
            bo.yMinMax = new Vector2(-perBoneRange, perBoneRange);
            bo.zMinMax = new Vector2(-perBoneRange, perBoneRange);
            lookBones[i] = bo;
        }

        Debug.Log($"[PlayableAnimationBlender] ✓ Initialized {lookBones.Count} look bones with ±{perBoneRange:F1}° per bone");

#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
    }

    #endregion

    #region Public API - Network Tuning

    public void SetNetworkSendRate(float rate)
    {
        networkSendRate = Mathf.Clamp(rate, 10f, 60f);
        Debug.Log($"[PlayableAnimationBlender] Network send rate: {networkSendRate}Hz ({1000f / networkSendRate:F0}ms)");
    }

    public void SetClientPrediction(bool enabled)
    {
        useClientPrediction = enabled;
        Debug.Log($"[PlayableAnimationBlender] Client prediction: {(enabled ? "ON" : "OFF")}");
    }

    public void SetChangeThreshold(float degrees)
    {
        positionChangedThreshold = Mathf.Max(0.1f, degrees);
        Debug.Log($"[PlayableAnimationBlender] Change threshold: {positionChangedThreshold}°");
    }

    public void SetNetworkSmoothingSpeed(float speed)
    {
        networkSmoothingSpeed = Mathf.Clamp(speed, 0.1f, 1f);
        Debug.Log($"[PlayableAnimationBlender] Smoothing speed: {networkSmoothingSpeed}");
    }

    public void SetNetworkSmoothing(bool enabled)
    {
        enableNetworkSmoothing = enabled;
        Debug.Log($"[PlayableAnimationBlender] Network smoothing: {(enabled ? "ON" : "OFF")}");
    }

    [ContextMenu("Debug Network Stats")]
    public void DebugNetworkStats()
    {
        if (isLocalPlayer)
        {
            Debug.Log($"[LOCAL] Vertical: {lookVerticalOffset:F2}° | Horizontal: {lookHorizontalOffset:F2}°");
            Debug.Log($"[LOCAL] Send Rate: {networkSendRate}Hz | Next Send: {(nextNetworkSendTime - Time.time):F3}s");
            Debug.Log($"[LOCAL] Velocity: V={lookVerticalVelocity:F2}°/s H={lookHorizontalVelocity:F2}°/s");
        }
        else
        {
            Debug.Log($"[REMOTE] Network: V={networkLookVertical:F2}° H={networkLookHorizontal:F2}°");
            Debug.Log($"[REMOTE] Smoothed: V={smoothedLookVertical:F2}° H={smoothedLookHorizontal:F2}°");
            Debug.Log($"[REMOTE] Predicted: V={predictedLookVertical:F2}° H={predictedLookHorizontal:F2}°");
            Debug.Log($"[REMOTE] Velocity: V={lookVerticalVelocity:F2}°/s H={lookHorizontalVelocity:F2}°/s");
        }
    }

    #endregion
}