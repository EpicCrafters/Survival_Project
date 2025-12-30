using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;

[System.Serializable]
public class GraphicsSettingsData
{
    public int qualityLevel = 2;
    public int resolutionIndex = 0;
    public bool fullscreen = true;
    public bool vsync = true;
    public int targetFrameRate = 60;
    public float brightness = 1.0f;
}