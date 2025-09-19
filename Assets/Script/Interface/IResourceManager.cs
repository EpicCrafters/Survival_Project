using UnityEngine;

public interface IResourceManager
{
    GameObject MarkResourceDestroyedAndReplace(string uniqueId, GameObject currentInstance, GameObject replacementPrefab = null);
    void OnResourceStateChanged(string uniqueId, bool isChopped);
}
