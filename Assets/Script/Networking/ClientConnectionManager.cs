using System.Collections;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ClientConnectionManager : MonoBehaviour
{
    public static ClientConnectionManager Instance { get; private set; }

    [Header("Settings")]
    public float connectionTimeout = 30f;
    public float reconnectDelay = 5f;

    private bool isConnecting = false;
    private float connectionStartTime;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        NetworkClient.OnConnectedEvent += OnConnected;
        NetworkClient.OnDisconnectedEvent += OnDisconnected;

        // Start connection process
        StartCoroutine(ConnectionLifecycleCoroutine());
    }

    void OnDestroy()
    {
        NetworkClient.OnConnectedEvent -= OnConnected;
        NetworkClient.OnDisconnectedEvent -= OnDisconnected;
    }

    private IEnumerator ConnectionLifecycleCoroutine()
    {
        while (true)
        {
            if (!NetworkClient.isConnected && !isConnecting)
            {
                yield return StartCoroutine(ConnectToServerCoroutine());
            }

            // If connected, wait and maintain connection
            if (NetworkClient.isConnected)
            {
                yield return new WaitForSeconds(1f);

                // Send periodic keep-alive if needed
                if (Time.time - connectionStartTime > 10f)
                {
                    // You can send a simple ping message here
                    SendKeepAlive();
                }
            }
            else
            {
                yield return new WaitForSeconds(reconnectDelay);
            }
        }
    }

    private IEnumerator ConnectToServerCoroutine()
    {
        isConnecting = true;
        connectionStartTime = Time.time;

        Debug.Log("[ClientConnectionManager] Starting connection to server...");

        // Your connection logic here - adjust to your network setup
        // NetworkManager.singleton.StartClient();

        // Wait for connection with timeout
        while (Time.time - connectionStartTime < connectionTimeout &&
               !NetworkClient.isConnected && isConnecting)
        {
            yield return null;
        }

        if (!NetworkClient.isConnected)
        {
            Debug.LogWarning("[ClientConnectionManager] Connection timeout");
            isConnecting = false;
        }
    }

    private void OnConnected()
    {
        Debug.Log("[ClientConnectionManager] Successfully connected to server");
        isConnecting = false;
        connectionStartTime = Time.time;

        // Start scene loading process after connection
        StartCoroutine(LoadInitialScenesCoroutine());
    }

    private void OnDisconnected()
    {
        Debug.Log("[ClientConnectionManager] Disconnected from server");
        isConnecting = false;
    }

    private IEnumerator LoadInitialScenesCoroutine()
    {
        // Wait a frame to ensure connection is stable
        yield return null;

        // Request initial environment scenes from server
        // or load default scenes based on your game logic

        Debug.Log("[ClientConnectionManager] Starting scene initialization...");

        // Your scene loading logic here
        // This prevents snapshot requests before scenes are ready
    }

    private void SendKeepAlive()
    {
        // Send a simple keep-alive message to prevent timeout
        if (NetworkClient.isConnected)
        {
            // Create a simple ping message if needed
            // NetworkClient.Send(new PingMessage());
        }
    }
}