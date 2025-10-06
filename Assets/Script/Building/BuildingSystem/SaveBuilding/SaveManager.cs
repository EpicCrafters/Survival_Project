// SaveManager.cs
using System.Collections.Generic;
using UnityEngine;

public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    // registry nội bộ
    private readonly List<ISaveable> saveables = new List<ISaveable>();
    private readonly object locker = new object();

    public bool HasUnsavedChanges
    {
        get
        {
            lock (locker)
            {
                foreach (var s in saveables)
                    if (s != null && s.HasUnsavedChanges)
                        return true;
            }
            return false;
        }
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void Register(ISaveable s)
    {
        if (s == null) return;
        lock (locker)
        {
            if (!saveables.Contains(s))
            {
                saveables.Add(s);
                Debug.Log($"[SaveManager] Registered {s.SaveableName}");
            }
        }
    }

    public void Unregister(ISaveable s)
    {
        if (s == null) return;
        lock (locker)
        {
            saveables.Remove(s);
        }
    }

    /// <summary>
    /// Trả về một copy của danh sách hiện tại — an toàn để iterate.
    /// </summary>
    public List<ISaveable> GetSaveables()
    {
        lock (locker)
        {
            return new List<ISaveable>(saveables);
        }
    }

    /// <summary>
    /// Synchronous save for every registered ISaveable.
    /// (PersistenceSceneManager sẽ thường dùng GetSaveables() để orchestrate staggered saves.)
    /// </summary>
    public void SaveAll()
    {
        var list = GetSaveables();
        foreach (var s in list)
        {
            if (s == null) continue;
            try
            {
                Debug.Log($"[SaveManager] → Saving '{s.SaveableName}' in scene '{s.SceneName}'");
                s.SaveNow();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[SaveManager] Save failed for {s.SaveableName}: {ex}");
            }
        }
    }
}
