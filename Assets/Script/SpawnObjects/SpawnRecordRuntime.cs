using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SpawnRecord
{
    public string uniqueId;
    public string prefabGuid;
    public string prefabPath;
    public string sceneName;   // NEW: which scene (or chunk) this record belongs to
    public string groundId;     // optional: sub-scene chunking
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 scale = Vector3.one;
    public bool isChopped;
}


[Serializable]
public class SpawnRecordCollection
{
    public List<SpawnRecord> records = new List<SpawnRecord>();
}
