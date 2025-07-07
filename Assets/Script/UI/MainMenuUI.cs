using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class MainMenuUI : MonoBehaviour
{



    [SerializeField] private Button ContinueButton;
    [SerializeField] private Button StartButton;
    [SerializeField] private Button QuitButton;



    private void Awake()
    {
        ContinueButton.onClick.AddListener(() =>
        {
            SceneManager.LoadScene("GameScene");
        });
        StartButton.onClick.AddListener(() =>
        {
            SceneManager.LoadScene("CharacterCustomizeScene");
        });
        QuitButton.onClick.AddListener(() =>
        {
            Application.Quit();
        });
    }
   
}
