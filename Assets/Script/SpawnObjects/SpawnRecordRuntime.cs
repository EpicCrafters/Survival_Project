using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SpawnRecord
{
    public string uniqueId;
    public string prefabGuid;
    public string prefabPath;
    public string sceneName;
    public string groundId;
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 scale = Vector3.one;
    public bool isChopped;
    public int curHealth;

    // NEW: Track replacement when object is destroyed
    public bool hasReplacement = false;
    public string replacementPrefabPath;      // Path of replacement prefab (stump)
    public int replacementHealth = 0;         // Health of replacement
}


[Serializable]
public class SpawnRecordCollection
{
    public long snapshotSeq;
    public long timestampTicks;
    public List<SpawnRecord> records = new List<SpawnRecord>();
}
