using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;
using System.Collections.Generic;
using System.Collections;
using System;
using Mirror;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DefaultExecutionOrder(300)] // Chạy trước hệ thống IK
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
    public class ActionAnimation
    {
        public string actionName;
        public AnimationClip animationClip;
        public float transitionDuration = 0.2f;
        public bool canInterrupt = false;
        [Range(0f, 1f)] public float layerWeight = 1f;
    }

    [Serializable]
    public class IKProtectedBone
    {
        public string boneName;
        public Transform bone;
        [Tooltip("Khi bật, xương này sẽ không bị ảnh hưởng bởi overlay")]
        public bool isProtected = true;
    }

    #endregion

    #region Inspector Fields

    [Header("Setup")]
    [SerializeField] private Animator animator;
    [SerializeField] private Transform animationRoot;

    [Header("Playables API - Action Animations")]
    [SerializeField] private List<ActionAnimation> actionAnimations = new List<ActionAnimation>();
    [SerializeField] private AnimationClip defaultIdleClip;
    [SerializeField, Range(0f, 1f)] private float actionLayerWeight = 1f;

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
    [Tooltip("Bật để loại trừ các xương cụ thể khỏi overlay (cho IK)")]
    [SerializeField] private bool enableIKProtection = true;
    [Tooltip("Các xương trong danh sách này sẽ không bị ảnh hưởng bởi overlay")]
    [SerializeField] private List<IKProtectedBone> ikProtectedBones = new List<IKProtectedBone>();

    [Header("Execution Settings")]
    [SerializeField, Range(0f, 1f)] private float masterWeight = 1f;
    [SerializeField] private bool previewInEditor = false;

    [Header("Network Settings")]
    [SerializeField] private float networkSendRate = 20f; // Tần suất gửi dữ liệu (Hz)
    [SerializeField] private bool enableNetworkSmoothing = true; // Làm mượt chuyển động trên client
    [SerializeField, Range(0f, 1f)] private float networkSmoothingSpeed = 0.15f; // Tốc độ làm mượt
    private float currentLookBlend = 0f;
    #endregion

    #region Network Synced Variables

    // SyncVar cho Look Offset - đồng bộ từ server xuống tất cả client
    [SyncVar(hook = nameof(OnLookVerticalChanged))]
    private float networkLookVertical = 0f;

    [SyncVar(hook = nameof(OnLookHorizontalChanged))]
    private float networkLookHorizontal = 0f;

    [SyncVar(hook = nameof(OnLookOffsetEnabledChanged))]
    private bool networkLookOffsetEnabled = true;

    // SyncVar cho Action Animation
    [SyncVar(hook = nameof(OnCurrentActionChanged))]
    private string networkCurrentAction = "";

    [SyncVar]
    private float networkActionTransitionTime = 0.2f;

    // Biến local để làm mượt giá trị nhận được từ network
    private float smoothedLookVertical = 0f;
    private float smoothedLookHorizontal = 0f;

    // Thời gian gửi dữ liệu network
    private float nextNetworkSendTime = 0f;

    #endregion

    #region Private Fields - Playables

    private PlayableGraph playableGraph;
    private AnimationMixerPlayable actionMixer;
    private Dictionary<string, int> actionIndexMap = new Dictionary<string, int>();
    private string currentAction = "";
    private bool isTransitioning = false;

    #endregion

    #region Private Fields

    private Dictionary<Transform, string> bonePathCache = new Dictionary<Transform, string>();
    private Dictionary<string, Coroutine> activeBlendCoroutines = new Dictionary<string, Coroutine>();
    private Dictionary<string, GameObject> previewObjects = new Dictionary<string, GameObject>();
    private Dictionary<Transform, Quaternion> initialBoneRotations = new Dictionary<Transform, Quaternion>();
    private Dictionary<Transform, Quaternion> baseAnimationRotations = new Dictionary<Transform, Quaternion>();
    private Dictionary<Transform, Quaternion> accumulatedOverlayRotations = new Dictionary<Transform, Quaternion>();
    private HashSet<Transform> ikProtectedTransforms = new HashSet<Transform>();

    // Dictionary để lưu trạng thái overlay và đồng bộ qua network
    private Dictionary<string, OverlayNetworkState> overlayNetworkStates = new Dictionary<string, OverlayNetworkState>();

    // Struct để lưu trạng thái overlay qua network
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

        InitializePlayableGraph();
    }

    void Start()
    {
        CacheBonePaths();
        InitializeOverlays();
        StoreInitialBoneRotations();
        CacheIKProtectedBones();

        // Khởi tạo giá trị làm mượt
        smoothedLookVertical = lookVerticalOffset;
        smoothedLookHorizontal = lookHorizontalOffset;

        Debug.Log($"[PlayableAnimationBlender] Initialized - Execution Order: 300 (chạy TRƯỚC IK trong LateUpdate)");
        Debug.Log($"[PlayableAnimationBlender] LookBones: {lookBones.Count}, Overlays: {overlayPoses.Count}, Actions: {actionAnimations.Count}, IK Protected: {ikProtectedTransforms.Count}");
        Debug.Log($"[PlayableAnimationBlender] Network Mode: {(isServer ? "Server" : isClient ? "Client" : "Offline")}");

        foreach (var bone in ikProtectedTransforms)
        {
            Debug.Log($"[PlayableAnimationBlender] ✓ IK Protected: {bone.name}");
        }
    }

    private void InitializePlayableGraph()
    {
        playableGraph = PlayableGraph.Create("AnimationBlenderGraph");
        playableGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

        actionMixer = AnimationMixerPlayable.Create(playableGraph, actionAnimations.Count + 1);

        if (defaultIdleClip != null)
        {
            var idleClip = AnimationClipPlayable.Create(playableGraph, defaultIdleClip);
            idleClip.SetApplyFootIK(false);
            playableGraph.Connect(idleClip, 0, actionMixer, 0);
            actionMixer.SetInputWeight(0, 1f);
        }

        for (int i = 0; i < actionAnimations.Count; i++)
        {
            var action = actionAnimations[i];
            if (action.animationClip != null)
            {
                var clipPlayable = AnimationClipPlayable.Create(playableGraph, action.animationClip);
                clipPlayable.SetApplyFootIK(false);
                playableGraph.Connect(clipPlayable, 0, actionMixer, i + 1);
                actionMixer.SetInputWeight(i + 1, 0f);

                actionIndexMap[action.actionName] = i + 1;
            }
        }

        var output = AnimationPlayableOutput.Create(playableGraph, "Animation", animator);
        output.SetSourcePlayable(actionMixer);

        playableGraph.Play();

        Debug.Log($"[PlayableAnimationBlender] Playable Graph đã khởi tạo với {actionAnimations.Count} actions");
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
            Debug.Log("[PlayableAnimationBlender] IK Protection đã TẮT");
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

        Debug.Log($"[PlayableAnimationBlender] IK Protection BẬT - {count} xương được bảo vệ");
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
        if (!Application.isPlaying) return;

        float deltaTime = Time.deltaTime;

        // === LOCAL PLAYER: Cập nhật và gửi dữ liệu lên server ===
        if (isLocalPlayer)
        {
            // Bước 1: Bắt trạng thái animation gốc (trước khi apply overlay)
            CaptureBaseAnimationState();

            // Bước 2: Cập nhật animation overlay
            UpdateAnimationOverlays(deltaTime);

            // Bước 3: Apply overlay (blend dần dần)
            ProcessOverlayPoses();

            // Bước 4: Apply look offset
            ProcessLookOffsets();

            // Bước 5: Gửi dữ liệu lên server theo tần suất
            if (Time.time >= nextNetworkSendTime)
            {
                SendLookDataToServer();
                SendOverlayDataToServer();
                nextNetworkSendTime = Time.time + (1f / networkSendRate);
            }
        }
        // === REMOTE PLAYER: Nhận và apply dữ liệu từ server ===
        else
        {
            // Làm mượt giá trị look offset nhận từ network
            if (enableNetworkSmoothing)
            {
                smoothedLookVertical = Mathf.Lerp(smoothedLookVertical, networkLookVertical, networkSmoothingSpeed);
                smoothedLookHorizontal = Mathf.Lerp(smoothedLookHorizontal, networkLookHorizontal, networkSmoothingSpeed);
            }
            else
            {
                smoothedLookVertical = networkLookVertical;
                smoothedLookHorizontal = networkLookHorizontal;
            }

            // Cập nhật overlay từ network state
            ApplyNetworkOverlayStates();

            // Bước 1-4: Giống local player
            CaptureBaseAnimationState();
            UpdateAnimationOverlays(deltaTime);
            ProcessOverlayPoses();
            ProcessLookOffsetsFromNetwork(); // Sử dụng giá trị từ network
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

    void OnDestroy()
    {
        if (playableGraph.IsValid())
        {
            playableGraph.Destroy();
        }
    }

    #endregion

    #region Network Synchronization

    /// <summary>
    /// GỬI dữ liệu Look Offset từ client lên server
    /// </summary>
    private void SendLookDataToServer()
    {
        if (!isLocalPlayer) return;

        // Nếu là server, cập nhật trực tiếp
        if (isServer)
        {
            networkLookVertical = lookVerticalOffset;
            networkLookHorizontal = lookHorizontalOffset;
            networkLookOffsetEnabled = enableLookOffset;
        }
        // Nếu là client, gửi Command lên server
        else if (isClient)
        {
            CmdUpdateLookOffset(lookVerticalOffset, lookHorizontalOffset, enableLookOffset);
        }
    }

    /// <summary>
    /// COMMAND: Client gửi Look Offset lên server
    /// </summary>
    [Command]
    private void CmdUpdateLookOffset(float vertical, float horizontal, bool enabled)
    {
        // Server nhận và cập nhật SyncVar (tự động đồng bộ xuống tất cả client)
        networkLookVertical = vertical;
        networkLookHorizontal = horizontal;
        networkLookOffsetEnabled = enabled;
    }

    /// <summary>
    /// GỬI dữ liệu Overlay lên server
    /// </summary>
    private void SendOverlayDataToServer()
    {
        if (!isLocalPlayer) return;

        foreach (var overlay in overlayPoses)
        {
            if (overlay == null) continue;

            float weight = overlay.blendWeight;
            float normalizedTime = overlay.normalizedTime;
            bool isPlaying = overlay.isPlaying;

            // Nếu là server, cập nhật trực tiếp
            if (isServer)
            {
                CmdUpdateOverlayState(overlay.overlayName, weight, normalizedTime, isPlaying);
            }
            // Nếu là client, gửi Command
            else if (isClient)
            {
                CmdUpdateOverlayState(overlay.overlayName, weight, normalizedTime, isPlaying);
            }
        }
    }

    /// <summary>
    /// COMMAND: Client gửi trạng thái Overlay lên server
    /// </summary>
    [Command]
    private void CmdUpdateOverlayState(string overlayName, float weight, float normalizedTime, bool isPlaying)
    {
        // Server lưu trữ và broadcast xuống tất cả client
        RpcUpdateOverlayState(overlayName, weight, normalizedTime, isPlaying);
    }

    /// <summary>
    /// RPC: Server broadcast trạng thái Overlay xuống tất cả client
    /// </summary>
    [ClientRpc]
    private void RpcUpdateOverlayState(string overlayName, float weight, float normalizedTime, bool isPlaying)
    {
        // Chỉ apply trên remote player (không phải local player)
        if (isLocalPlayer) return;

        overlayNetworkStates[overlayName] = new OverlayNetworkState
        {
            weight = weight,
            normalizedTime = normalizedTime,
            isPlaying = isPlaying
        };
    }

    /// <summary>
    /// APPLY trạng thái Overlay từ network vào local overlay
    /// </summary>
    private void ApplyNetworkOverlayStates()
    {
        foreach (var kvp in overlayNetworkStates)
        {
            var overlay = overlayPoses.Find(o => o.overlayName == kvp.Key);
            if (overlay == null) continue;

            var state = kvp.Value;

            // Làm mượt weight nếu cần
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

    /// <summary>
    /// HOOK: Khi networkLookVertical thay đổi (được gọi tự động bởi Mirror)
    /// </summary>
    private void OnLookVerticalChanged(float oldValue, float newValue)
    {
        if (!isLocalPlayer)
        {
            // Remote player nhận giá trị mới từ server
            smoothedLookVertical = newValue;
        }
    }

    /// <summary>
    /// HOOK: Khi networkLookHorizontal thay đổi
    /// </summary>
    private void OnLookHorizontalChanged(float oldValue, float newValue)
    {
        if (!isLocalPlayer)
        {
            smoothedLookHorizontal = newValue;
        }
    }

    /// <summary>
    /// HOOK: Khi networkLookOffsetEnabled thay đổi
    /// </summary>
    private void OnLookOffsetEnabledChanged(bool oldValue, bool newValue)
    {
        if (!isLocalPlayer)
        {
            enableLookOffset = newValue;
        }
    }

    /// <summary>
    /// HOOK: Khi networkCurrentAction thay đổi (Action Animation)
    /// </summary>
    private void OnCurrentActionChanged(string oldValue, string newValue)
    {
        if (!isLocalPlayer && !string.IsNullOrEmpty(newValue))
        {
            // Remote player nhận action mới và chạy transition
            if (string.IsNullOrEmpty(newValue))
            {
                ReturnToIdle(networkActionTransitionTime);
            }
            else
            {
                PlayAction(newValue, networkActionTransitionTime);
            }
        }
    }

    /// <summary>
    /// GỬI Action Animation lên server
    /// </summary>
    private void SendActionToServer(string actionName, float transitionTime)
    {
        if (!isLocalPlayer) return;

        if (isServer)
        {
            networkCurrentAction = actionName;
            networkActionTransitionTime = transitionTime;
        }
        else if (isClient)
        {
            CmdPlayAction(actionName, transitionTime);
        }
    }

    /// <summary>
    /// COMMAND: Client yêu cầu chạy Action Animation
    /// </summary>
    [Command]
    private void CmdPlayAction(string actionName, float transitionTime)
    {
        networkCurrentAction = actionName;
        networkActionTransitionTime = transitionTime;
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

    #region Playables API - Action Control

    /// <summary>
    /// Chạy Action Animation (đồng bộ qua network)
    /// </summary>
    public void PlayAction(string actionName, float transitionTime = -1f)
    {
        if (!actionIndexMap.ContainsKey(actionName))
        {
            Debug.LogWarning($"[PlayableAnimationBlender] Action '{actionName}' không tìm thấy!");
            return;
        }

        var action = actionAnimations.Find(a => a.actionName == actionName);
        if (action == null) return;

        if (transitionTime < 0) transitionTime = action.transitionDuration;

        if (isTransitioning && !string.IsNullOrEmpty(currentAction))
        {
            var currentActionData = actionAnimations.Find(a => a.actionName == currentAction);
            if (currentActionData != null && !currentActionData.canInterrupt)
            {
                Debug.LogWarning($"[PlayableAnimationBlender] Không thể ngắt action '{currentAction}'");
                return;
            }
        }

        // Gửi dữ liệu lên network nếu là local player
        if (isLocalPlayer)
        {
            SendActionToServer(actionName, transitionTime);
        }

        StopAllCoroutines();
        StartCoroutine(TransitionToAction(actionName, transitionTime));
    }

    public void ReturnToIdle(float transitionTime = 0.2f)
    {
        // Gửi dữ liệu lên network nếu là local player
        if (isLocalPlayer)
        {
            SendActionToServer("", transitionTime);
        }

        StopAllCoroutines();
        StartCoroutine(TransitionToAction("", transitionTime));
    }

    public bool IsPlayingAction(string actionName)
    {
        return currentAction == actionName;
    }

    public bool IsPlayingAnyAction()
    {
        return !string.IsNullOrEmpty(currentAction);
    }

    public float GetCurrentActionTime()
    {
        if (string.IsNullOrEmpty(currentAction) || !actionIndexMap.ContainsKey(currentAction))
            return 0f;

        int index = actionIndexMap[currentAction];
        var playable = (AnimationClipPlayable)actionMixer.GetInput(index);

        if (playable.IsValid() && playable.GetAnimationClip() != null)
        {
            return (float)(playable.GetTime() / playable.GetAnimationClip().length);
        }

        return 0f;
    }

    private IEnumerator TransitionToAction(string actionName, float duration)
    {
        isTransitioning = true;
        float elapsed = 0f;

        int targetIndex = string.IsNullOrEmpty(actionName) ? 0 : actionIndexMap[actionName];

        Dictionary<int, float> startWeights = new Dictionary<int, float>();
        for (int i = 0; i < actionMixer.GetInputCount(); i++)
        {
            startWeights[i] = actionMixer.GetInputWeight(i);
        }

        if (targetIndex > 0)
        {
            var targetPlayable = (AnimationClipPlayable)actionMixer.GetInput(targetIndex);
            if (targetPlayable.IsValid())
            {
                targetPlayable.SetTime(0);
                targetPlayable.Play();
            }
        }

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            for (int i = 0; i < actionMixer.GetInputCount(); i++)
            {
                float targetWeight = (i == targetIndex) ? actionLayerWeight : 0f;
                float newWeight = Mathf.Lerp(startWeights[i], targetWeight, t);
                actionMixer.SetInputWeight(i, newWeight);
            }

            yield return null;
        }

        for (int i = 0; i < actionMixer.GetInputCount(); i++)
        {
            actionMixer.SetInputWeight(i, (i == targetIndex) ? actionLayerWeight : 0f);
        }

        currentAction = actionName;
        isTransitioning = false;

        Debug.Log($"[PlayableAnimationBlender] Đã chuyển sang: {(string.IsNullOrEmpty(actionName) ? "Idle" : actionName)}");
    }

    public void SetActionSpeed(string actionName, float speed)
    {
        if (!actionIndexMap.ContainsKey(actionName)) return;

        int index = actionIndexMap[actionName];
        var playable = (AnimationClipPlayable)actionMixer.GetInput(index);

        if (playable.IsValid())
        {
            playable.SetSpeed(speed);
        }
    }

    public void SetActionLayerWeight(float weight)
    {
        actionLayerWeight = Mathf.Clamp01(weight);
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

    #region Look Offset Processing

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

    /// <summary>
    /// Xử lý Look Offset cho LOCAL PLAYER (sử dụng giá trị local)
    /// </summary>
    private void ProcessLookOffsets()
    {
        if (!enableLookOffset || lookBones == null || lookBones.Count == 0 || animationRoot == null)
            return;

        float vertical = lookVerticalOffset;
        float horizontal = lookHorizontalOffset;

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

            if (currentLookBlend <= 0f) return;

            Vector3 localDir = animationRoot.InverseTransformDirection(direction.normalized);

            float targetHorizontal = Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg;
            float targetVertical = Mathf.Asin(-localDir.y) * Mathf.Rad2Deg;

            targetHorizontal = Mathf.Clamp(targetHorizontal, -90f, 90f);
            targetVertical = Mathf.Clamp(targetVertical, -90f, 90f);

            vertical = Mathf.Lerp(lookVerticalOffset, targetVertical, lookAtBlendWeight * currentLookBlend);
            horizontal = Mathf.Lerp(lookHorizontalOffset, targetHorizontal, lookAtBlendWeight * currentLookBlend);
        }

        ApplyLookOffsetsToBonesInternal(vertical, horizontal);
    }

    /// <summary>
    /// Xử lý Look Offset cho REMOTE PLAYER (sử dụng giá trị từ network)
    /// </summary>
    private void ProcessLookOffsetsFromNetwork()
    {
        if (!networkLookOffsetEnabled || lookBones == null || lookBones.Count == 0 || animationRoot == null)
            return;

        ApplyLookOffsetsToBonesInternal(smoothedLookVertical, smoothedLookHorizontal);
    }

    /// <summary>
    /// Apply look offset lên các xương (dùng chung cho cả local và remote)
    /// </summary>
    private void ApplyLookOffsetsToBonesInternal(float vertical, float horizontal)
    {
        for (int i = 0; i < lookBones.Count; i++)
        {
            BoneOffset bo = lookBones[i];
            if (bo.bone == null) continue;

            // Bỏ qua xương được bảo vệ bởi IK
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

        // FIXED: Reset accumulated rotations về base animation state trước khi apply overlay
        foreach (var kvp in baseAnimationRotations)
        {
            if (accumulatedOverlayRotations.ContainsKey(kvp.Key))
            {
                accumulatedOverlayRotations[kvp.Key] = kvp.Value;
            }
        }

        // Apply từng overlay dần dần, blend với kết quả đã tích lũy
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

                    // Bỏ qua xương được bảo vệ bởi IK
                    if (enableIKProtection && ikProtectedTransforms.Contains(bone))
                        continue;

                    // FIXED: Blend từ kết quả overlay đã tích lũy (các overlay trước đã apply)
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

                    // Blend từ kết quả tích lũy đến target pose
                    Quaternion newRotation = Quaternion.Slerp(currentRotation, recordedGlobalRotation, effectiveBlend);

                    // Cập nhật rotation của xương
                    bone.rotation = newRotation;

                    // Lưu kết quả tích lũy cho overlay tiếp theo
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

    /// <summary>
    /// Bật/tắt IK Protection
    /// </summary>
    public void SetIKProtection(bool enabled)
    {
        enableIKProtection = enabled;
        Debug.Log($"[PlayableAnimationBlender] IK Protection: {(enabled ? "Bật" : "Tắt")}");
    }

    /// <summary>
    /// Thêm xương vào danh sách bảo vệ IK
    /// </summary>
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

            Debug.Log($"[PlayableAnimationBlender] ✓ Đã thêm IK protection: {ikBone.boneName}");
        }
    }

    /// <summary>
    /// Xóa xương khỏi danh sách bảo vệ IK
    /// </summary>
    public void RemoveIKProtectedBone(Transform bone)
    {
        if (bone == null) return;

        if (ikProtectedTransforms.Contains(bone))
        {
            ikProtectedTransforms.Remove(bone);
            ikProtectedBones.RemoveAll(b => b.bone == bone);

            Debug.Log($"[PlayableAnimationBlender] Đã xóa IK protection: {bone.name}");
        }
    }

    /// <summary>
    /// Xóa toàn bộ IK protection
    /// </summary>
    public void ClearIKProtection()
    {
        ikProtectedTransforms.Clear();
        ikProtectedBones.Clear();
        Debug.Log("[PlayableAnimationBlender] Đã xóa tất cả IK protection");
    }

    /// <summary>
    /// Bật/tắt protection cho một xương cụ thể
    /// </summary>
    public void SetBoneProtection(Transform bone, bool isProtected)
    {
        if (bone == null) return;

        var ikBone = ikProtectedBones.Find(b => b.bone == bone);
        if (ikBone != null)
        {
            ikBone.isProtected = isProtected;
            CacheIKProtectedBones();
            Debug.Log($"[PlayableAnimationBlender] {bone.name} protection: {isProtected}");
        }
    }

    /// <summary>
    /// Làm mới cache IK Protection
    /// </summary>
    [ContextMenu("Refresh IK Protection Cache")]
    public void RefreshIKProtectionCache()
    {
        CacheIKProtectedBones();
    }

    #endregion

    #region Public API - Overlay Control

    /// <summary>
    /// Chạy overlay pose (đồng bộ qua network nếu là local player)
    /// </summary>
    public void PlayOverlay(string overlayName, float transitionTime = -1f)
    {
        var overlay = overlayPoses.Find(o => o.overlayName == overlayName);
        if (overlay == null)
        {
            Debug.LogWarning($"Overlay '{overlayName}' không tìm thấy!");
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

    /// <summary>
    /// Dừng overlay pose
    /// </summary>
    public void StopOverlay(string overlayName, float transitionTime = -1f)
    {
        var overlay = overlayPoses.Find(o => o.overlayName == overlayName);
        if (overlay == null) return;

        if (transitionTime < 0) transitionTime = overlay.defaultTransitionTime;

        overlay.isPlaying = false;
        BlendOverlay(overlayName, 0f, transitionTime);
    }

    /// <summary>
    /// Blend overlay từ weight hiện tại đến target weight
    /// </summary>
    public void BlendOverlay(string overlayName, float targetWeight, float duration = 0.3f)
    {
        var overlay = overlayPoses.Find(o => o.overlayName == overlayName);
        if (overlay == null)
        {
            Debug.LogWarning($"Overlay '{overlayName}' không tìm thấy!");
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

    /// <summary>
    /// Set overlay weight trực tiếp (không có transition)
    /// </summary>
    public void SetOverlayWeight(string overlayName, float weight)
    {
        var overlay = overlayPoses.Find(o => o.overlayName == overlayName);
        if (overlay != null)
        {
            overlay.blendWeight = Mathf.Clamp01(weight);
            overlay.needsUpdate = true;
        }
    }

    /// <summary>
    /// Set thời gian playback của overlay animation
    /// </summary>
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

    /// <summary>
    /// Set Look Offset theo trục dọc (Vertical: -90 đến +90 độ)
    /// </summary>
    public void SetLookVerticalOffset(float offset)
    {
        lookVerticalOffset = Mathf.Clamp(offset, -90f, 90f);
    }

    /// <summary>
    /// Set Look Offset theo trục ngang (Horizontal: -90 đến +90 độ)
    /// </summary>
    public void SetLookHorizontalOffset(float offset)
    {
        lookHorizontalOffset = Mathf.Clamp(offset, -90f, 90f);
    }

    /// <summary>
    /// Set cả Vertical và Horizontal cùng lúc
    /// </summary>
    public void SetLookOffset(float vertical, float horizontal)
    {
        lookVerticalOffset = Mathf.Clamp(vertical, -90f, 90f);
        lookHorizontalOffset = Mathf.Clamp(horizontal, -90f, 90f);
    }

    /// <summary>
    /// Bật/tắt Look Offset
    /// </summary>
    public void SetLookOffsetEnabled(bool enabled)
    {
        enableLookOffset = enabled;
    }

    /// <summary>
    /// Bật/tắt Look-At Mode
    /// </summary>
    public void SetLookAtMode(bool enabled)
    {
        enableLookAtMode = enabled;
    }

    /// <summary>
    /// Set target cho Look-At Mode
    /// </summary>
    public void SetLookAtTarget(Transform target)
    {
        lookAtTarget = target;
    }

    /// <summary>
    /// Set blend weight cho Look-At Mode
    /// </summary>
    public void SetLookAtBlendWeight(float weight)
    {
        lookAtBlendWeight = Mathf.Clamp01(weight);
    }

    #endregion

    #region Public API - Pose Recording

    /// <summary>
    /// Ghi lại pose hiện tại thành AnimationPoseDataSO
    /// </summary>
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

        Debug.Log($"Đã ghi {poseData.boneTransforms.Count} xương vào pose '{poseName}'");
        return poseData;
    }

    #endregion

    #region Look Bone Initialization

    /// <summary>
    /// Khởi tạo phạm vi xoay cho Look Bones
    /// </summary>
    [ContextMenu("Initialize Look Bone Ranges")]
    public void InitializeBaseLookProfile()
    {
        if (lookBones == null || lookBones.Count == 0)
        {
            Debug.LogWarning("Không có look bones được cấu hình.");
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

        Debug.Log($"✓ Đã khởi tạo {lookBones.Count} look bones với phạm vi ±{perBoneRange:F1}° mỗi xương.");

#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
    }

    #endregion

    #region Public API - Network Settings

    /// <summary>
    /// Set tần suất gửi dữ liệu qua network (Hz)
    /// </summary>
    public void SetNetworkSendRate(float rate)
    {
        networkSendRate = Mathf.Max(1f, rate);
    }

    /// <summary>
    /// Bật/tắt network smoothing
    /// </summary>
    public void SetNetworkSmoothing(bool enabled)
    {
        enableNetworkSmoothing = enabled;
    }

    /// <summary>
    /// Set tốc độ smoothing (0-1)
    /// </summary>
    public void SetNetworkSmoothingSpeed(float speed)
    {
        networkSmoothingSpeed = Mathf.Clamp01(speed);
    }

    #endregion
}