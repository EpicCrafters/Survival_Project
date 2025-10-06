using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct SerializableVector3
{
    public float x, y, z;
    public SerializableVector3(Vector3 v) { x = v.x; y = v.y; z = v.z; }
    public Vector3 ToVector3() => new Vector3(x, y, z);
}

[Serializable]
public struct SerializableQuaternion
{
    public float x, y, z, w;
    public SerializableQuaternion(Quaternion q) { x = q.x; y = q.y; z = q.z; w = q.w; }
    public Quaternion ToQuaternion() => new Quaternion(x, y, z, w);
}

[Serializable]
public class BuildRecord
{
    public string guid;
    public string itemId;      // primary key để tìm ItemData
    public string prefabName;  // fallback
    public SerializableVector3 position;
    public SerializableQuaternion rotation;
    public SerializableVector3 scale;
    public string anchorGuid;  // guid của anchor nếu có
    public string sceneName;
}

[Serializable]
public class BuildRecordCollection
{
    public int version = 1;
    public List<BuildRecord> records = new List<BuildRecord>();
}
