// ISaveable.cs
using UnityEngine;

public interface ISaveable
{
    bool HasUnsavedChanges { get; }
    void SaveNow();
    string SaveableName { get; }
    string SceneName { get; }
}
