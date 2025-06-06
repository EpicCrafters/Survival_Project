using UnityEngine;

public class GameSceneUI : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    [SerializeField] private Player player;
    [SerializeField] private GameObject mainInventory;
    void Start()
    {
       
        Hide();
    }

    // Update is called once per frame
    void Update()
    {
        ShowInventory();
    }

    private void ShowInventory()
    {
        if (player.IsShowInventory())
        {
            Show();
        }
        else
        {
            Hide();
        }
    }

    private void Show()
    {
       
        mainInventory.SetActive(true);
    }
    private void Hide()
    {
        
        mainInventory.SetActive(false);
    }
}
