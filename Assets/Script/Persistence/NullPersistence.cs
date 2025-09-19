// NullPersistence.cs
/// <summary>Persistence that does nothing (clients should use this).</summary>
public class NullPersistence : IResourcePersistence
{
    public SpawnRecordCollection Load() => new SpawnRecordCollection();
    public void Save(SpawnRecordCollection container) { /* no-op */ }
}
