using System.Collections.Generic;
using UnityEngine;

public interface IResourceDropHandler
{
    /// <summary>
    /// Called by manager to spawn non-persistent/persistent drops for a resource that's been chopped.
    /// Should return a list of spawned GameObjects (empty list if none). The handler may call manager APIs
    /// (e.g. SpawnPersistentDrop) which abstract away local vs network spawning.
    /// </summary>
    List<GameObject> SpawnDrops(ResourceManager manager, GameObject currentInstance, SpawnRecord record);
}
