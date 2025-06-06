using UnityEngine;

public class CharacterLoadFromJSON : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created


    [Header("Character Appearance")]
    [SerializeField] private SkinnedMeshRenderer hairRenderer;
    [SerializeField] private HairStyle[] hairStyles;

    [SerializeField] private Renderer skinRenderer;
    [SerializeField] private Material whiteMaterial;
    [SerializeField] private Material brownMaterial;

    [SerializeField] private Renderer shirtRenderer;
    [SerializeField] private Renderer pantsRenderer;
    [SerializeField] private Renderer shoesRenderer;
    void Start()
    {

        LoadCharacterCustomization();
    }

    private void LoadCharacterCustomization()
    {
        string path = Application.persistentDataPath + "/character.json";
        if (!System.IO.File.Exists(path))
        {
            Debug.LogWarning("No character customization data found.");
            return;
        }

        string json = System.IO.File.ReadAllText(path);
        CharacterData data = JsonUtility.FromJson<CharacterData>(json);

        // Apply skin tone
        Material skinMat = new Material(skinRenderer.material);
        skinMat.color = Color.Lerp(whiteMaterial.color, brownMaterial.color, data.skinToneValue);
        skinRenderer.material = skinMat;

        // Apply hair style
        if (hairStyles != null && hairStyles.Length > 0)
        {
            int hairIndex = Mathf.Clamp(data.hairIndex, 0, hairStyles.Length - 1);
            hairRenderer.sharedMesh = hairStyles[hairIndex].mesh;
            hairRenderer.material = hairStyles[hairIndex].material;
            Debug.Log($"Loaded hair: {hairRenderer.sharedMesh.name}");
        }

        // Apply clothing colors
        Color shirtColor = ParseColor(data.shirtColor);
        Color pantsColor = ParseColor(data.pantsColor);
        Color shoesColor = ParseColor(data.shoesColor);

        shirtRenderer.material.color = shirtColor;
        pantsRenderer.material.color = pantsColor;
        shoesRenderer.material.color = shoesColor;

        Debug.Log("Character customization loaded successfully.");
    }

    private Color ParseColor(string html)
    {
        return ColorUtility.TryParseHtmlString("#" + html, out Color color) ? color : Color.white;
    }
}
