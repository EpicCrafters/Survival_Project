using UnityEngine;
using UnityEngine.UI;

public class PlayerSkinCustom : MonoBehaviour
{
    public Slider colorSlider;
    public Renderer playerRenderer;

    public Material whiteMaterial;  // Drag your white skin material here
    public Material brownMaterial;  // Drag your brown skin material here

    private Material runtimeMaterial;

    private void Start()
    {
        // Create a copy of the current material so we don’t overwrite the original
        runtimeMaterial = new Material(playerRenderer.material);
        playerRenderer.material = runtimeMaterial;

        colorSlider.onValueChanged.AddListener(UpdateSkinMaterial);
    }

    private void UpdateSkinMaterial(float value)
    {
        // Lerp the color between the two materials
        Color whiteColor = whiteMaterial.color;
        Color brownColor = brownMaterial.color;
        Color blendedColor = Color.Lerp(whiteColor, brownColor, value);

        runtimeMaterial.color = blendedColor;

        // If you want to blend other properties like smoothness or metallic:
        // runtimeMaterial.SetFloat("_Glossiness", Mathf.Lerp(
        //     whiteMaterial.GetFloat("_Glossiness"), 
        //     brownMaterial.GetFloat("_Glossiness"), 
        //     value));
    }
}
