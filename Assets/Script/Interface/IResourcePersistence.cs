// IResourcePersistence.cs
public interface IResourcePersistence
{
    /// <summary>Load saved snapshot (may return empty container).</summary>
    SpawnRecordCollection Load();

    /// <summary>Save snapshot to storage (atomic recommended).</summary>
    void Save(SpawnRecordCollection container);
}
