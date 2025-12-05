#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

[CustomEditor(typeof(PlayableAnimationBlender))]
public class PoseRecorderEditor : Editor
{
    private string newPoseName = "NewPose";
    private AnimationPoseDataSO selectedPoseData;
    private bool showPoseRecording = true;
    private bool showOverlayTesting = true;
    private bool showUtilities = false;

    private SerializedProperty overlayPosesProperty;
    private Dictionary<string, bool> overlayFoldouts = new Dictionary<string, bool>();

    private void OnEnable()
    {
        overlayPosesProperty = serializedObject.FindProperty("overlayPoses");
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        PlayableAnimationBlender blender = (PlayableAnimationBlender)target;

        EditorGUILayout.Space(20);
        DrawOverlayManagement(blender);

        EditorGUILayout.Space(10);
        DrawPoseRecordingSection(blender);

        EditorGUILayout.Space(10);
        DrawOverlayTestingSection(blender);

        EditorGUILayout.Space(10);
        DrawUtilitiesSection(blender);
    }

    #region Overlay Management

    private void DrawOverlayManagement(PlayableAnimationBlender blender)
    {
        EditorGUILayout.LabelField("Overlay Management", EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "Manage overlay poses and animations here. Click 'Add Bone Chain' to select bones!",
            MessageType.Info
        );

        serializedObject.Update();

        if (overlayPosesProperty.arraySize == 0)
        {
            EditorGUILayout.HelpBox("No overlays configured. Add overlays in the 'Overlay Poses' section above.", MessageType.Warning);
            return;
        }

        for (int i = 0; i < overlayPosesProperty.arraySize; i++)
        {
            SerializedProperty overlayProp = overlayPosesProperty.GetArrayElementAtIndex(i);
            SerializedProperty overlayNameProp = overlayProp.FindPropertyRelative("overlayName");
            SerializedProperty overlayTypeProp = overlayProp.FindPropertyRelative("overlayType");
            SerializedProperty bonePresetProp = overlayProp.FindPropertyRelative("bonePreset");
            SerializedProperty boneChainsProperty = overlayProp.FindPropertyRelative("boneChains");

            string overlayName = overlayNameProp.stringValue;
            if (string.IsNullOrEmpty(overlayName))
                overlayName = $"Overlay {i}";

            if (!overlayFoldouts.ContainsKey(overlayName))
                overlayFoldouts[overlayName] = false;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Header
            EditorGUILayout.BeginHorizontal();
            overlayFoldouts[overlayName] = EditorGUILayout.Foldout(
                overlayFoldouts[overlayName],
                $"🎯 {overlayName}",
                true,
                EditorStyles.foldoutHeader
            );

            // Quick status
            var overlayType = (PlayableAnimationBlender.OverlayType)overlayTypeProp.enumValueIndex;
            string typeLabel = overlayType == PlayableAnimationBlender.OverlayType.StaticPose ? "📌" : "🎬";
            EditorGUILayout.LabelField(typeLabel, GUILayout.Width(20));

            EditorGUILayout.EndHorizontal();

            if (overlayFoldouts[overlayName])
            {
                EditorGUI.indentLevel++;

                // Type indicator
                EditorGUILayout.LabelField("Type", overlayType.ToString(), EditorStyles.miniLabel);

                // Add Bone Chain Button
                EditorGUILayout.Space(5);
                GUI.color = new Color(0.5f, 0.8f, 1f);
                if (GUILayout.Button("➕ Add Bone Chain", GUILayout.Height(35)))
                {
                    var overlay = GetOverlayAtIndex(blender, i);
                    if (overlay != null)
                    {
                        BonePickerWindow.ShowWindow(blender, overlay);
                    }
                }
                GUI.color = Color.white;

                // Bone Preset Selection (Optional Quick Setup)
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Quick Setup (Optional)", EditorStyles.boldLabel);

              

              

                // Bone Chains Summary
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Bone Chains", EditorStyles.boldLabel);

                if (boneChainsProperty.arraySize == 0)
                {
                    EditorGUILayout.HelpBox("No bone chains configured. Click 'Add Bone Chain' or select a preset.", MessageType.Warning);
                }
                else
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                    int totalBones = 0;
                    for (int j = 0; j < boneChainsProperty.arraySize; j++)
                    {
                        SerializedProperty chainProp = boneChainsProperty.GetArrayElementAtIndex(j);
                        SerializedProperty chainNameProp = chainProp.FindPropertyRelative("chainName");
                        SerializedProperty bonesProp = chainProp.FindPropertyRelative("bones");
                        SerializedProperty chainWeightProp = chainProp.FindPropertyRelative("blendWeight");

                        totalBones += bonesProp.arraySize;

                        EditorGUILayout.BeginHorizontal();

                        // Chain icon based on name
                        string icon = GetChainIcon(chainNameProp.stringValue);
                        EditorGUILayout.LabelField(icon, GUILayout.Width(20));

                        // Chain info
                        EditorGUILayout.LabelField(
                            $"{chainNameProp.stringValue} ({bonesProp.arraySize} bones)",
                            GUILayout.Width(150)
                        );

                        // Weight slider
                        float newWeight = EditorGUILayout.Slider(chainWeightProp.floatValue, 0f, 1f);
                        if (Mathf.Abs(newWeight - chainWeightProp.floatValue) > 0.001f)
                        {
                            chainWeightProp.floatValue = newWeight;
                        }

                        EditorGUILayout.EndHorizontal();
                    }

                    EditorGUILayout.Space(5);
                    EditorGUILayout.LabelField($"Total: {boneChainsProperty.arraySize} chains, {totalBones} bones",
                        EditorStyles.miniLabel);

                    EditorGUILayout.EndVertical();
                }

                // Quick Actions
                EditorGUILayout.Space(5);
                EditorGUILayout.BeginHorizontal();

                if (GUILayout.Button("📋 Expand Details", GUILayout.Height(25)))
                {
                    Selection.activeObject = blender;
                }

                if (overlayType == PlayableAnimationBlender.OverlayType.AnimationClip)
                {
                    if (GUILayout.Button("▶️ Play", GUILayout.Height(25)))
                    {
                        if (Application.isPlaying)
                            blender.PlayOverlay(overlayName);
                    }
                }

                EditorGUILayout.EndHorizontal();

                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }

        serializedObject.ApplyModifiedProperties();
    }

    private string GetChainIcon(string chainName)
    {
        if (chainName.Contains("Arm")) return "💪";
        if (chainName.Contains("Leg")) return "🦵";
        if (chainName.Contains("Head") || chainName.Contains("Neck")) return "🧠";
        if (chainName.Contains("Spine") || chainName.Contains("Chest")) return "🫀";
        if (chainName.Contains("Hand")) return "✋";
        return "🦴";
    }

    private PlayableAnimationBlender.AnimationOverlay GetOverlayAtIndex(PlayableAnimationBlender blender, int index)
    {
        var field = typeof(PlayableAnimationBlender).GetField("overlayPoses",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (field != null)
        {
            var overlays = field.GetValue(blender) as List<PlayableAnimationBlender.AnimationOverlay>;
            if (overlays != null && index >= 0 && index < overlays.Count)
                return overlays[index];
        }
        return null;
    }

    #endregion

    #region Pose Recording

    private void DrawPoseRecordingSection(PlayableAnimationBlender blender)
    {
        showPoseRecording = EditorGUILayout.Foldout(showPoseRecording, "📷 Pose Recording Tools", true, EditorStyles.foldoutHeader);

        if (!showPoseRecording) return;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.HelpBox(
            "Step 1: Set up your character in the desired pose\n" +
            "Step 2: Enter a name and click 'Record Current Pose'\n" +
            "Step 3: Assign the created PoseData to an overlay above",
            MessageType.Info
        );

        EditorGUILayout.Space(5);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Pose Name:", GUILayout.Width(80));
        newPoseName = EditorGUILayout.TextField(newPoseName);
        EditorGUILayout.EndHorizontal();

        GUI.color = Color.green;
        if (GUILayout.Button("📷 Record Current Pose", GUILayout.Height(40)))
        {
            RecordPose(blender);
        }
        GUI.color = Color.white;

        EditorGUILayout.EndVertical();
    }

    private void RecordPose(PlayableAnimationBlender blender)
    {
        if (string.IsNullOrEmpty(newPoseName))
        {
            EditorUtility.DisplayDialog("Error", "Please enter a pose name!", "OK");
            return;
        }

        var poseData = blender.RecordCurrentPose(newPoseName);

        if (poseData.boneTransforms.Count == 0)
        {
            EditorUtility.DisplayDialog(
                "Warning",
                "No bones were recorded!\n\nCheck:\n- Have you added Overlay Poses?\n- Have you set up Bone Chains?",
                "OK"
            );
            DestroyImmediate(poseData);
            return;
        }

        string folderPath = "Assets/AnimationPoses";
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            AssetDatabase.CreateFolder("Assets", "AnimationPoses");
        }

        string assetPath = $"{folderPath}/{newPoseName}.asset";
        assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);

        AssetDatabase.CreateAsset(poseData, assetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorGUIUtility.PingObject(poseData);
        Selection.activeObject = poseData;

        EditorUtility.DisplayDialog(
            "Success!",
            $"Recorded {poseData.boneTransforms.Count} bones\n\nFile: {assetPath}",
            "OK"
        );

        Debug.Log($"✓ Pose '{newPoseName}' saved at: {assetPath}");
    }

    #endregion

    #region Overlay Testing

    private void DrawOverlayTestingSection(PlayableAnimationBlender blender)
    {
        showOverlayTesting = EditorGUILayout.Foldout(showOverlayTesting, "🎮 Quick Test Controls", true, EditorStyles.foldoutHeader);

        if (!showOverlayTesting) return;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        if (Application.isPlaying)
        {
            var overlayPoses = GetOverlayPoses(blender);

            if (overlayPoses.Count == 0)
            {
                EditorGUILayout.HelpBox("No overlays configured.", MessageType.Info);
            }

            foreach (var overlay in overlayPoses)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                // Overlay name and type
                EditorGUILayout.BeginHorizontal();
                string typeIcon = overlay.overlayType == PlayableAnimationBlender.OverlayType.StaticPose ? "📌" : "🎬";
                EditorGUILayout.LabelField($"{typeIcon} {overlay.overlayName}", EditorStyles.boldLabel);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();

                // Control buttons
                if (overlay.overlayType == PlayableAnimationBlender.OverlayType.StaticPose)
                {
                    GUI.color = Color.green;
                    if (GUILayout.Button("ON", GUILayout.Width(50), GUILayout.Height(30)))
                    {
                        blender.BlendOverlay(overlay.overlayName, 1f, 0.3f);
                    }

                    GUI.color = Color.red;
                    if (GUILayout.Button("OFF", GUILayout.Width(50), GUILayout.Height(30)))
                    {
                        blender.BlendOverlay(overlay.overlayName, 0f, 0.3f);
                    }
                    GUI.color = Color.white;
                }
                else
                {
                    GUI.color = Color.green;
                    if (GUILayout.Button("▶️ Play", GUILayout.Width(70), GUILayout.Height(30)))
                    {
                        blender.PlayOverlay(overlay.overlayName);
                    }

                    GUI.color = Color.yellow;
                    if (GUILayout.Button("⏸️ Stop", GUILayout.Width(70), GUILayout.Height(30)))
                    {
                        blender.StopOverlay(overlay.overlayName);
                    }
                    GUI.color = Color.white;
                }

                // Weight slider
                EditorGUILayout.LabelField("Weight:", GUILayout.Width(50));
                float newWeight = EditorGUILayout.Slider(overlay.blendWeight, 0f, 1f);
                if (Mathf.Abs(newWeight - overlay.blendWeight) > 0.001f)
                {
                    blender.SetOverlayWeight(overlay.overlayName, newWeight);
                }

                EditorGUILayout.EndHorizontal();

                // Animation-specific controls
                if (overlay.overlayType == PlayableAnimationBlender.OverlayType.AnimationClip &&
                    overlay.animationClip != null)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("Time:", GUILayout.Width(50));
                    float newTime = EditorGUILayout.Slider(overlay.normalizedTime, 0f, 1f);
                    if (Mathf.Abs(newTime - overlay.normalizedTime) > 0.001f)
                    {
                        blender.SetOverlayPlaybackTime(overlay.overlayName, newTime);
                    }
                    EditorGUILayout.LabelField($"{overlay.playbackTime:F2}s", GUILayout.Width(50));
                    EditorGUILayout.EndHorizontal();

                    if (overlay.isPlaying)
                    {
                        EditorGUILayout.LabelField("▶️ Playing...", EditorStyles.miniLabel);
                    }
                }

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(5);
            }
        }
        else
        {
            EditorGUILayout.HelpBox("▶️ Enter Play Mode to test overlays", MessageType.Warning);
        }

        EditorGUILayout.EndVertical();
    }

    #endregion

    #region Utilities

    private void DrawUtilitiesSection(PlayableAnimationBlender blender)
    {
        showUtilities = EditorGUILayout.Foldout(showUtilities, "🔧 Utilities", true, EditorStyles.foldoutHeader);

        if (!showUtilities) return;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        selectedPoseData = (AnimationPoseDataSO)EditorGUILayout.ObjectField(
            "Preview Pose Data",
            selectedPoseData,
            typeof(AnimationPoseDataSO),
            false
        );

        if (selectedPoseData != null && GUILayout.Button("📊 Show Pose Info"))
        {
            ShowPoseInfo(selectedPoseData);
        }

        EditorGUILayout.Space(5);

        if (GUILayout.Button("🔄 Refresh Bone Cache"))
        {
            Debug.Log("Bone cache will be refreshed on next frame");
        }

        EditorGUILayout.EndVertical();
    }

    private void ShowPoseInfo(AnimationPoseDataSO poseData)
    {
        string info = $"Pose: {poseData.name}\n";
        info += $"Number of bones: {poseData.boneTransforms.Count}\n\n";
        info += "Bones:\n";

        foreach (var bone in poseData.boneTransforms)
        {
            info += $"  • {bone.bonePath}\n";
            info += $"    Rotation: {bone.localRotation.eulerAngles}\n";
        }

        Debug.Log(info);
        EditorUtility.DisplayDialog("Pose Information", info, "OK");
    }

    #endregion

    #region Helper Methods

    private List<PlayableAnimationBlender.AnimationOverlay> GetOverlayPoses(PlayableAnimationBlender blender)
    {
        var field = typeof(PlayableAnimationBlender).GetField("overlayPoses",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (field != null)
        {
            var value = field.GetValue(blender);
            return value as List<PlayableAnimationBlender.AnimationOverlay>;
        }

        return new List<PlayableAnimationBlender.AnimationOverlay>();
    }

    #endregion
}

// Bone Picker Window
public class BonePickerWindow : EditorWindow
{
    private PlayableAnimationBlender blender;
    private PlayableAnimationBlender.AnimationOverlay targetOverlay;
    private string newChainName = "New Chain";

    private Transform rootTransform;
    private List<BoneItem> allBones = new List<BoneItem>();
    private HashSet<Transform> selectedBones = new HashSet<Transform>();

    private Vector2 scrollPosition;
    private string searchFilter = "";
    private bool showOnlySelected = false;

    private class BoneItem
    {
        public Transform bone;
        public string displayName;
        public int depth;
        public bool hasChildren;
        public bool isExpanded;

        public BoneItem(Transform bone, string displayName, int depth, bool hasChildren)
        {
            this.bone = bone;
            this.displayName = displayName;
            this.depth = depth;
            this.hasChildren = hasChildren;
            this.isExpanded = true;
        }
    }

    public static void ShowWindow(PlayableAnimationBlender blender, PlayableAnimationBlender.AnimationOverlay overlay)
    {
        BonePickerWindow window = GetWindow<BonePickerWindow>("Bone Chain Picker");
        window.minSize = new Vector2(400, 500);
        window.blender = blender;
        window.targetOverlay = overlay;
        window.Initialize();
        window.Show();
    }

    private void Initialize()
    {
        if (blender == null) return;

        var animator = blender.GetComponent<Animator>();
        if (animator != null)
        {
            rootTransform = animator.transform;
        }
        else
        {
            rootTransform = blender.transform;
        }

        BuildBoneHierarchy();
    }

    private void BuildBoneHierarchy()
    {
        allBones.Clear();
        if (rootTransform == null) return;

        AddBoneRecursive(rootTransform, 0);
    }

    private void AddBoneRecursive(Transform bone, int depth)
    {
        bool hasChildren = bone.childCount > 0;
        allBones.Add(new BoneItem(bone, bone.name, depth, hasChildren));

        for (int i = 0; i < bone.childCount; i++)
        {
            AddBoneRecursive(bone.GetChild(i), depth + 1);
        }
    }

    private void OnGUI()
    {
        if (blender == null || targetOverlay == null)
        {
            EditorGUILayout.HelpBox("Invalid reference. Please close and reopen this window.", MessageType.Error);
            return;
        }

        DrawHeader();
        DrawToolbar();
        DrawBoneList();
        DrawFooter();
    }

    private void DrawHeader()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.LabelField("Create Bone Chain", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"For Overlay: {targetOverlay.overlayName}", EditorStyles.miniLabel);

        EditorGUILayout.Space(5);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Chain Name:", GUILayout.Width(80));
        newChainName = EditorGUILayout.TextField(newChainName);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        EditorGUILayout.LabelField($"Selected Bones: {selectedBones.Count}", EditorStyles.helpBox);

        EditorGUILayout.EndVertical();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        GUILayout.Label("🔍", GUILayout.Width(20));
        searchFilter = EditorGUILayout.TextField(searchFilter, EditorStyles.toolbarSearchField);

        if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(50)))
        {
            searchFilter = "";
        }

        GUILayout.FlexibleSpace();

        showOnlySelected = GUILayout.Toggle(showOnlySelected, "Selected Only", EditorStyles.toolbarButton);

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Select All", GUILayout.Height(25)))
        {
            SelectAll();
        }

        if (GUILayout.Button("Deselect All", GUILayout.Height(25)))
        {
            DeselectAll();
        }

        if (GUILayout.Button("Expand All", GUILayout.Height(25)))
        {
            ExpandAll(true);
        }

        if (GUILayout.Button("Collapse All", GUILayout.Height(25)))
        {
            ExpandAll(false);
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);
    }

    private void DrawBoneList()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Bone Hierarchy", EditorStyles.boldLabel);

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        foreach (var boneItem in allBones)
        {
            if (!string.IsNullOrEmpty(searchFilter) &&
                !boneItem.displayName.ToLower().Contains(searchFilter.ToLower()))
                continue;

            if (showOnlySelected && !selectedBones.Contains(boneItem.bone))
                continue;

            if (!IsVisible(boneItem))
                continue;

            DrawBoneItem(boneItem);
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private bool IsVisible(BoneItem item)
    {
        if (item.depth == 0) return true;

        int currentIndex = allBones.IndexOf(item);
        for (int i = currentIndex - 1; i >= 0; i--)
        {
            if (allBones[i].depth < item.depth)
            {
                if (!allBones[i].isExpanded)
                    return false;

                return IsVisible(allBones[i]);
            }
        }

        return true;
    }

    private void DrawBoneItem(BoneItem item)
    {
        EditorGUILayout.BeginHorizontal();

        GUILayout.Space(item.depth * 20);

        if (item.hasChildren)
        {
            string foldoutIcon = item.isExpanded ? "▼" : "▶";
            if (GUILayout.Button(foldoutIcon, EditorStyles.label, GUILayout.Width(15)))
            {
                item.isExpanded = !item.isExpanded;
            }
        }
        else
        {
            GUILayout.Space(15);
        }

        bool isSelected = selectedBones.Contains(item.bone);
        bool newSelected = EditorGUILayout.Toggle(isSelected, GUILayout.Width(20));

        if (newSelected != isSelected)
        {
            if (newSelected)
            {
                selectedBones.Add(item.bone);
            }
            else
            {
                selectedBones.Remove(item.bone);
            }
        }

        string icon = item.hasChildren ? "🦴" : "⚫";
        GUILayout.Label(icon, GUILayout.Width(20));

        if (GUILayout.Button(item.displayName, EditorStyles.label))
        {
            Selection.activeGameObject = item.bone.gameObject;
            EditorGUIUtility.PingObject(item.bone.gameObject);
        }

        var animator = blender.GetComponent<Animator>();
        if (animator != null && animator.isHuman)
        {
            for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
            {
                if (animator.GetBoneTransform((HumanBodyBones)i) == item.bone)
                {
                    GUILayout.Label($"[{((HumanBodyBones)i).ToString()}]", EditorStyles.miniLabel);
                    break;
                }
            }
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawFooter()
    {
        EditorGUILayout.Space(10);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        EditorGUILayout.BeginHorizontal();

        GUI.color = Color.green;
        if (GUILayout.Button("✓ Create Bone Chain", GUILayout.Height(40)))
        {
            CreateBoneChain();
        }
        GUI.color = Color.white;

        GUI.color = Color.red;
        if (GUILayout.Button("✖ Cancel", GUILayout.Height(40)))
        {
            Close();
        }
        GUI.color = Color.white;

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
    }

    private void SelectAll()
    {
        selectedBones.Clear();
        foreach (var item in allBones)
        {
            if (!string.IsNullOrEmpty(searchFilter) &&
                !item.displayName.ToLower().Contains(searchFilter.ToLower()))
                continue;

            selectedBones.Add(item.bone);
        }
    }

    private void DeselectAll()
    {
        selectedBones.Clear();
    }

    private void ExpandAll(bool expand)
    {
        foreach (var item in allBones)
        {
            item.isExpanded = expand;
        }
    }

    private void CreateBoneChain()
    {
        if (selectedBones.Count == 0)
        {
            EditorUtility.DisplayDialog("No Bones Selected",
                "Please select at least one bone to create a chain.", "OK");
            return;
        }

        if (string.IsNullOrEmpty(newChainName))
        {
            EditorUtility.DisplayDialog("Invalid Chain Name",
                "Please enter a valid chain name.", "OK");
            return;
        }

        var newChain = new PlayableAnimationBlender.BoneChain
        {
            chainName = newChainName,
            blendWeight = 1f,
            bones = new List<PlayableAnimationBlender.BoneRotationSettings>()
        };

        var orderedBones = allBones
            .Where(item => selectedBones.Contains(item.bone))
            .Select(item => item.bone)
            .ToList();

        foreach (var bone in orderedBones)
        {
            newChain.bones.Add(new PlayableAnimationBlender.BoneRotationSettings
            {
                boneName = bone.name,
                bone = bone,
                blendWeight = 1f,
                rotationOffset = Vector3.zero
            });
        }

        targetOverlay.boneChains.Add(newChain);

        EditorUtility.SetDirty(blender);

        EditorUtility.DisplayDialog("Success!",
            $"Created bone chain '{newChainName}' with {selectedBones.Count} bones.", "OK");

        Close();
    }
}
#endif