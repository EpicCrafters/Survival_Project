using UnityEngine;

[System.Serializable]
public class KeybindData
{
    public string actionName;      // Tên action (Move, Jump, Attack,...)
    public string displayName;     // Tên hiển thị trên UI
    public string bindingPath;     // Đường dẫn binding hiện tại
    public int bindingIndex;       // Index của binding trong action
}