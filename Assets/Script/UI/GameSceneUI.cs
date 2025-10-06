using UnityEngine;

public class GameSceneUI : MonoBehaviour
{
    private Player player; // no longer [SerializeField]
    [SerializeField] private GameObject mainInventory;

    void Start()
    {
        Hide();
    }

    void Update()
    {
        if (player == null) return; // wait until player is assigned
        ShowInventory();
    }

    public void SetPlayer(Player newPlayer)
    {
        player = newPlayer;
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
            CraftingManager.Instance.ReturnItemsToInventory();
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
