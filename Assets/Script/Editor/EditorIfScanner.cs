// Assets/Editor/EditorIfScanner.cs
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

public class EditorIfScanner : EditorWindow
{
    Vector2 scroll;
    List<ScanResult> results = new List<ScanResult>();

    [MenuItem("Tools/Editor If Scanner")]
    public static void ShowWindow() => GetWindow<EditorIfScanner>("Editor If Scanner");

    void OnGUI()
    {
        if (GUILayout.Button("Scan project for #if UNITY_EDITOR risks"))
        {
            ScanProject();
        }

        GUILayout.Space(6);
        EditorGUILayout.HelpBox("Flags: [SerializeField], public fields, or class/MonoBehaviour defs found inside #if UNITY_EDITOR blocks.", MessageType.Info);
        GUILayout.Space(6);

        scroll = EditorGUILayout.BeginScrollView(scroll);
        foreach (var r in results)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField(r.filePath, EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Block lines: {r.startLine} - {r.endLine}");
            foreach (var issue in r.issues)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"L{issue.lineNumber}: {issue.snippet}", GUILayout.MaxWidth(position.width - 120));
                if (GUILayout.Button("Open", GUILayout.MaxWidth(60)))
                {
                    // open external at line
                    UnityEditorInternal.InternalEditorUtility.OpenFileAtLineExternal(r.filePath, issue.lineNumber);
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
            GUILayout.Space(4);
        }
        EditorGUILayout.EndScrollView();

        if (results.Count == 0) EditorGUILayout.LabelField("No risky blocks found (or scan not run yet).");
    }

    void ScanProject()
    {
        results.Clear();
        string[] files = Directory.GetFiles(Application.dataPath, "*.cs", SearchOption.AllDirectories);

        var ifPattern = new Regex(@"^\s*#\s*if\s+UNITY_EDITOR\b", RegexOptions.Compiled);
        var endifPattern = new Regex(@"^\s*#\s*endif\b", RegexOptions.Compiled);
        var serializePattern = new Regex(@"\[SerializeField\]", RegexOptions.Compiled);
        var publicFieldPattern = new Regex(@"public\s+[A-Za-z0-9_<>,\[\]\s]+\s+[a-zA-Z0-9_]+\s*;", RegexOptions.Compiled);
        var classPattern = new Regex(@"class\s+[A-Za-z0-9_]+\s*(:\s*[A-Za-z0-9_,\s<>]+)?", RegexOptions.Compiled);

        foreach (var f in files)
        {
            string[] lines = File.ReadAllLines(f);
            for (int i = 0; i < lines.Length; i++)
            {
                if (ifPattern.IsMatch(lines[i]))
                {
                    int start = i + 1;
                    int end = -1;
                    // find matching endif (simple linear)
                    for (int j = i + 1; j < lines.Length; j++)
                    {
                        if (endifPattern.IsMatch(lines[j]))
                        {
                            end = j + 1;
                            break;
                        }
                    }
                    if (end == -1) end = lines.Length;

                    var sr = new ScanResult { filePath = f, startLine = start, endLine = end };
                    for (int k = start - 1; k < end && k < lines.Length; k++)
                    {
                        string ln = lines[k];
                        if (serializePattern.IsMatch(ln) || publicFieldPattern.IsMatch(ln) || classPattern.IsMatch(ln))
                        {
                            sr.issues.Add(new Issue { lineNumber = k + 1, snippet = ln.Trim() });
                        }
                    }
                    if (sr.issues.Count > 0) results.Add(sr);
                    i = end - 1; // continue after endif
                }
            }
        }

        // Sort results by path
        results.Sort((a, b) => a.filePath.CompareTo(b.filePath));
        Debug.Log($"EditorIfScanner: found {results.Count} files with issues.");
    }

    class ScanResult
    {
        public string filePath;
        public int startLine;
        public int endLine;
        public List<Issue> issues = new List<Issue>();
    }

    class Issue
    {
        public int lineNumber;
        public string snippet;
    }
}
