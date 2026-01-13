using UnityEngine;
using UnityEngine.UI;
using TMPro;


public class DeathUIManager : MonoBehaviour
{
    public static DeathUIManager Instance { get; private set; }

    [Header("Death UI References")]
    [SerializeField] private GameObject deathScreen;
    [SerializeField] private TextMeshProUGUI countdownText;
    [SerializeField] private Button respawnButton;
    [SerializeField] private TextMeshProUGUI buttonText;

    [Header("Respawn Settings")]
    [SerializeField] private float respawnCooldown = 5f; // Seconds before respawn is available

    private float currentCountdown;
    private bool isCountingDown = false;
    private PlayerStatManager currentPlayer;

    private void Awake()
    {
        // Singleton setup
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Hide death screen initially
        if (deathScreen != null)
            deathScreen.SetActive(false);

        // Setup respawn button
        if (respawnButton != null)
        {
            respawnButton.onClick.AddListener(OnRespawnButtonClicked);
            respawnButton.interactable = false; // Start disabled
        }
    }

    private void Update()
    {
        if (isCountingDown)
        {
            currentCountdown -= Time.deltaTime;

            // Update countdown text
            if (countdownText != null)
            {
                if (currentCountdown > 0)
                {
                    countdownText.text = $"Respawn available in: {Mathf.CeilToInt(currentCountdown)}s";
                }
                else
                {
                    countdownText.text = "You can respawn now!";
                }
            }

            // Enable button when countdown reaches 0
            if (currentCountdown <= 0 && respawnButton != null && !respawnButton.interactable)
            {
                respawnButton.interactable = true;
                if (buttonText != null)
                    buttonText.text = "RESPAWN";
            }
        }
    }

  
    public void ShowDeathScreen(PlayerStatManager player)
    {
        if (!player.isLocalPlayer) return;
        currentPlayer = player;

        if (deathScreen != null)
            deathScreen.SetActive(true);

        currentCountdown = respawnCooldown;
        isCountingDown = true;

        if (respawnButton != null)
        {
            respawnButton.interactable = false;
            if (buttonText != null)
                buttonText.text = "WAIT...";
        }

        // Sử dụng CursorManager với ưu tiên cao nhất
        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.ShowCursor(CursorManager.CursorPriority.DeathScreen);
        }
    }


    public void HideDeathScreen()
    {

        if (deathScreen != null)
            deathScreen.SetActive(false);

        isCountingDown = false;
        currentPlayer = null;

        // Giải phóng ưu tiên death screen
        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.HideCursor(CursorManager.CursorPriority.DeathScreen);
        }
    }


    private void OnRespawnButtonClicked()
    {
        if (currentPlayer != null && respawnButton.interactable)
        {
            currentPlayer.RequestRespawn();
            HideDeathScreen();
        }
    }

    public Vector3 GetSpawnPoint()
    {
        MyNetworkManager networkManager = FindObjectOfType<MyNetworkManager>();
        if (networkManager != null && networkManager.spawnPoint != null)
        {
            return networkManager.spawnPoint.position;
        }
        return Vector3.zero;
    }

    public bool IsDeathScreenActive()
    {
        return deathScreen != null && deathScreen.activeSelf;
    }
}
