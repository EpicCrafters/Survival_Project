using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.IO;

public class CharacterCustomizeUI : MonoBehaviour
{
    [Header("Buttons")] // Các nút điều khiển
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button nextHairButton;
    [SerializeField] private Button previousHairButton;

    [Header("Skin Customization")] // Tuỳ chỉnh màu da
    [SerializeField] private Slider skinColorSlider;
    [SerializeField] private Renderer skinRenderer;
    [SerializeField] private Material whiteMaterial;
    [SerializeField] private Material brownMaterial;

    [Header("Clothing Customization")] // Tuỳ chỉnh quần áo
    [SerializeField] private ClothingPart shirt;
    [SerializeField] private ClothingPart pants;
    [SerializeField] private ClothingPart shoes;

    [Header("Hair Customization")] // Tuỳ chỉnh tóc
    [SerializeField] private SkinnedMeshRenderer hairRenderer;
    [SerializeField] private HairStyle[] hairStyles;
    [SerializeField] private TextMeshProUGUI hairNameText;

    private Material runtimeSkinMaterial;
    private int currentHairIndex = 0;

    private void Start()
    {
        // Khởi tạo dữ liệu
        SetupSkinMaterial();
        SetupSlider();
        SetupHairButtons();
        SetupClothing();

        // Màu quần áo mặc định khi bắt đầu
        shirt.ApplyColor(Color.red);
        pants.ApplyColor(Color.gray);
        shoes.ApplyColor(Color.black);

        // Gán chức năng cho nút xác nhận
        confirmButton?.onClick.AddListener(SaveCustomizationToJson);
        UpdateHairDisplay();
    }

    #region Setup Methods

    // Tạo bản sao của material da để có thể thay đổi khi chơi
    private void SetupSkinMaterial()
    {
        if (skinRenderer != null)
        {
            runtimeSkinMaterial = new Material(skinRenderer.material);
            skinRenderer.material = runtimeSkinMaterial;
        }
    }

    // Thiết lập thanh trượt màu da
    private void SetupSlider()
    {
        if (skinColorSlider != null)
            skinColorSlider.onValueChanged.AddListener(UpdateSkinTone);
    }

    // Gán sự kiện cho nút đổi kiểu tóc
    private void SetupHairButtons()
    {
        nextHairButton?.onClick.AddListener(NextHair);
        previousHairButton?.onClick.AddListener(PreviousHair);
    }

    // Thiết lập quần áo và gán sự kiện chọn màu
    private void SetupClothing()
    {
        shirt.Setup();
        pants.Setup();
        shoes.Setup();
    }

    #endregion

    #region Hair Control

    // Cập nhật kiểu tóc đang chọn
    private void UpdateHairDisplay()
    {
        if (hairRenderer == null) return;

        if (hairStyles == null || hairStyles.Length == 0)
        {
            hairRenderer.sharedMesh = null;
            hairRenderer.material = null;
            hairNameText.text = "None";
            return;
        }

        HairStyle style = hairStyles[currentHairIndex];
        hairRenderer.sharedMesh = style.mesh;
        hairRenderer.material = style.material;

        hairNameText.text = style.mesh != null ? style.mesh.name : "None";
    }

    // Chuyển sang kiểu tóc tiếp theo
    private void NextHair()
    {
        if (hairStyles == null || hairStyles.Length == 0) return;

        currentHairIndex = (currentHairIndex + 1) % hairStyles.Length;
        UpdateHairDisplay();
    }

    // Quay lại kiểu tóc trước đó
    private void PreviousHair()
    {
        if (hairStyles == null || hairStyles.Length == 0) return;

        currentHairIndex = (currentHairIndex - 1 + hairStyles.Length) % hairStyles.Length;
        UpdateHairDisplay();
    }

    #endregion

    #region Skin Tone

    // Cập nhật màu da theo giá trị của thanh trượt
    private void UpdateSkinTone(float value)
    {
        if (runtimeSkinMaterial == null || whiteMaterial == null || brownMaterial == null) return;

        Color blended = Color.Lerp(whiteMaterial.color, brownMaterial.color, value);
        runtimeSkinMaterial.color = blended;
    }

    #endregion

    #region Save

    // Lưu dữ liệu tuỳ chỉnh nhân vật vào file JSON
    private void SaveCustomizationToJson()
    {
        CharacterData data = new CharacterData
        {
            skinToneValue = skinColorSlider.value,
            hairIndex = currentHairIndex,
            shirtColor = ColorUtility.ToHtmlStringRGBA(shirt.partRenderer.material.color),
            pantsColor = ColorUtility.ToHtmlStringRGBA(pants.partRenderer.material.color),
            shoesColor = ColorUtility.ToHtmlStringRGBA(shoes.partRenderer.material.color)
        };

        string json = JsonUtility.ToJson(data, true);
        string path = Application.persistentDataPath + "/character.json";

        try
        {
            File.WriteAllText(path, json);
            Debug.Log($"✔️ Lưu tuỳ chỉnh nhân vật thành công tại: {path}");

            string hairName = hairStyles != null && hairStyles.Length > currentHairIndex && hairStyles[currentHairIndex].mesh != null
                ? hairStyles[currentHairIndex].mesh.name
                : "Unknown";

            Debug.Log($"🔹 Tóc đã chọn: {hairName} (Chỉ số: {currentHairIndex})");
            Debug.Log($"🔹 Màu áo: #{data.shirtColor}");
            Debug.Log($"🔹 Màu quần: #{data.pantsColor}");
            Debug.Log($"🔹 Màu giày: #{data.shoesColor}");
            Debug.Log($"🔹 Giá trị màu da: {data.skinToneValue:F2}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"❌ Không thể lưu dữ liệu nhân vật: {ex.Message}");
        }

        // Chuyển sang màn chơi chính
        SceneManager.LoadScene("GameScene");
    }

    #endregion
}

[System.Serializable]
public class HairStyle
{
    public Mesh mesh;        // Lưới 3D cho kiểu tóc
    public Material material; // Chất liệu tóc
}

[System.Serializable]
public class ClothingPart
{
    public string partName;
    public Renderer partRenderer;

    // Các nút chọn màu
    public Button redButton;
    public Button greenButton;
    public Button blueButton;
    public Button yellowButton;
    public Button purpleButton;
    public Button blackButton;
    public Button grayButton;

    // Thiết lập renderer và gán sự kiện cho các nút
    public void Setup()
    {
        if (partRenderer == null) return;

        partRenderer.material = new Material(partRenderer.material);

        redButton?.onClick.AddListener(() => ApplyColor(Color.red));
        greenButton?.onClick.AddListener(() => ApplyColor(Color.green));
        blueButton?.onClick.AddListener(() => ApplyColor(Color.blue));
        yellowButton?.onClick.AddListener(() => ApplyColor(Color.yellow));
        purpleButton?.onClick.AddListener(() => ApplyColor(new Color(0.5f, 0f, 0.5f))); // tím
        blackButton?.onClick.AddListener(() => ApplyColor(Color.black));
        grayButton?.onClick.AddListener(() => ApplyColor(Color.gray));
    }

    // Áp dụng màu được chọn
    public void ApplyColor(Color color)
    {
        if (partRenderer != null)
            partRenderer.material.color = color;
    }
}
