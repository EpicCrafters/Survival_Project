using UnityEngine;


[System.Serializable]

public class GraphicsSettingsData
{
    public int qualityLevel = 2;
    public int resolutionIndex = 0;
    public FullScreenMode screenMode = FullScreenMode.ExclusiveFullScreen;  // Fullscreen, Window, Borderless
    public bool vsync = true;
    public int targetFrameRate = 60;  // 60, 90, 144, -1 (unlimited)
    public float brightness = 1.0f;
}