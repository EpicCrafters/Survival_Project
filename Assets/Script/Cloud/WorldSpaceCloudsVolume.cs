using UnityEngine;
using UnityEngine.Rendering;

[System.Serializable]
[VolumeComponentMenu("Custom/World Space Clouds")]
public class WorldSpaceCloudsVolume : VolumeComponent
{
    [Header("3D Textures")]
    [Tooltip("First 3D noise texture for cloud base shapes")]
    public Texture3DParameter cloudTexture1 = new Texture3DParameter(null);

    [Tooltip("Second 3D noise texture for cloud variation")]
    public Texture3DParameter cloudTexture2 = new Texture3DParameter(null);

    [Tooltip("Third 3D noise texture for cloud details")]
    public Texture3DParameter cloudTexture3 = new Texture3DParameter(null);

    [Header("Cloud Appearance")]
    public ColorParameter cloudColor = new ColorParameter(Color.white);
    public ColorParameter cloudShadowColor = new ColorParameter(new Color(0.6f, 0.65f, 0.75f));
    public ColorParameter cloudHighlightColor = new ColorParameter(new Color(1f, 0.95f, 0.9f));

    [Header("Cloud Shape")]
    [Tooltip("Overall scale of clouds. Lower = bigger clouds")]
    public ClampedFloatParameter cloudScale = new ClampedFloatParameter(1f, 0.1f, 10f);

    [Tooltip("Cloud movement speed (X, Y, Z)")]
    public Vector3Parameter cloudSpeed = new Vector3Parameter(new Vector3(0.02f, 0f, 0.01f));

    [Tooltip("Height of cloud layer center")]
    public FloatParameter cloudHeight = new FloatParameter(100f);

    [Tooltip("Vertical thickness of cloud layer")]
    public FloatParameter cloudThickness = new FloatParameter(50f);

    [Header("Cloud Density")]
    [Tooltip("Overall cloud density multiplier")]
    public ClampedFloatParameter densityMultiplier = new ClampedFloatParameter(1f, 0f, 5f);

    [Tooltip("Number of ray marching steps. Higher = better quality but slower")]
    public ClampedIntParameter rayMarchSteps = new ClampedIntParameter(64, 8, 128);

    [Tooltip("How much light is absorbed by clouds")]
    public ClampedFloatParameter lightAbsorption = new ClampedFloatParameter(1.5f, 0f, 5f);

    [Header("Detail")]
    [Tooltip("Scale of detail features. Lower = bigger details")]
    public ClampedFloatParameter detailScale = new ClampedFloatParameter(3f, 0.5f, 10f);

    [Tooltip("Strength of detail erosion")]
    public ClampedFloatParameter detailStrength = new ClampedFloatParameter(0.3f, 0f, 1f);

    [Header("Lighting")]
    [Tooltip("Direction of sun/main light")]
    public Vector3Parameter sunDirection = new Vector3Parameter(new Vector3(0.5f, 0.5f, 0.5f));

    [Tooltip("Ambient light amount")]
    public ClampedFloatParameter ambientLight = new ClampedFloatParameter(0.3f, 0f, 1f);

    [Header("Ghibli Style")]
    [Tooltip("Softness of cloud edges. Higher = softer")]
    public ClampedFloatParameter edgeSoftness = new ClampedFloatParameter(0.3f, 0f, 1f);

    [Tooltip("Puffiness of cloud tops. Higher = rounder, fluffier")]
    public ClampedFloatParameter puffiness = new ClampedFloatParameter(1f, 0f, 2f);

    // Helper method to check if all required textures are assigned
    public bool IsValid()
    {
        return cloudTexture1.value != null &&
               cloudTexture2.value != null &&
               cloudTexture3.value != null;
    }
}