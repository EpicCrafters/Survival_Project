using UnityEngine;

[System.Serializable]
public class AILoadData
{
    public string uniqueId;
    public string poolKey;
    public Vector3 position;
    public Quaternion rotation;
    public int health;
    public bool isDead;
    public float lastSavedTime;
}
