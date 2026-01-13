using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Mirror;
using TMPro;

public class MainMenuUI : MonoBehaviour
{
    [Header("UI (assign in inspector)")]
    public GameObject MenuUI;
    public GameObject GameplayUI;
    public Button HostButton;
    public Button ClientButton;
    public Button QuitButton;
    public TMP_InputField ServerAddressInput;
    public TMP_InputField PortInput; // Added for port configuration

    [Header("Loading Screen")]
    public GameObject LoadingScreen;  // Assign a panel in inspector
    public TMP_Text LoadingStatusText;  // Text for status updates
    public GameObject LoadingSpinner;  // Spinner with pulsing animation

    [Header("Optional Status Display")]
    public TMP_Text NetworkStatusText; // Optional - create if you want status display

    [Header("Environment scenes to load (additive)")]
    [Tooltip("Add one or more environment/ground scenes here (they must be in Build Settings).")]
    public List<string> environmentSceneNames = new List<string>();

    [Header("Networking")]
    [Tooltip("Seconds to wait after StartHost() for the local server+client to come up")]
    public float hostStartupWait = 1.0f;
    [Tooltip("Client connect timeout (seconds)")]
    public float clientConnectTimeout = 10f;

    [Header("Client behaviour")]
    [Tooltip("If true, a client that successfully connects will locally load the environment scenes (same behaviour as Host). If false, client will wait for server's LoadEnvMessage.")]
    public bool clientLoadEnvironmentOnConnect = true;

    private NetworkManager manager;
    private Coroutine clientConnectCoroutine;
    private bool isConnecting = false;
    private Coroutine spinnerPulseCoroutine;

    private void Start()
    {
        manager = NetworkManager.singleton;
        if (manager == null)
        {
            Debug.LogError("[Menu] NetworkManager.singleton is null. Put NetworkManager in bootstrap scene.");
            return;
        }

        if (MenuUI == null) MenuUI = gameObject;
        if (GameplayUI != null) GameplayUI.SetActive(false);
        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.ShowCursor(CursorManager.CursorPriority.MainMenu);
            Debug.Log("[MainMenuUI] Showing cursor via CursorManager");
        }
        else
        {
            // Fallback nếu không có CursorManager
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        //Play Music
        AudioManager.Instance.PlayMenuMusicWithFade();

        // Initialize loading screen as hidden
        if (LoadingScreen != null)
        {
            LoadingScreen.SetActive(false);
        }

        // Setup button listeners
        if (HostButton != null) { HostButton.onClick.RemoveAllListeners(); HostButton.onClick.AddListener(OnHostClicked); }
        if (ClientButton != null) { ClientButton.onClick.RemoveAllListeners(); ClientButton.onClick.AddListener(OnClientClicked); }
        if (QuitButton != null) { QuitButton.onClick.RemoveAllListeners(); QuitButton.onClick.AddListener(OnQuitClicked); }

        // Initialize port input with current port
        if (PortInput != null && Transport.active is PortTransport portTransport)
        {
            PortInput.text = portTransport.Port.ToString();
            PortInput.onEndEdit.AddListener(OnPortChanged);
        }

        // register handler for server->client LoadEnvMessage
        NetworkClient.RegisterHandler<LoadEnvMessage>(OnLoadEnvMessage, false);
    }

    private void OnDestroy()
    {
        if (HostButton != null) HostButton.onClick.RemoveListener(OnHostClicked);
        if (ClientButton != null) ClientButton.onClick.RemoveListener(OnClientClicked);
        if (QuitButton != null) QuitButton.onClick.RemoveListener(OnQuitClicked);

        if (PortInput != null) PortInput.onEndEdit.RemoveListener(OnPortChanged);

        if (clientConnectCoroutine != null) StopCoroutine(clientConnectCoroutine);
        if (spinnerPulseCoroutine != null) StopCoroutine(spinnerPulseCoroutine);

        if (NetworkClient.active)
        {
            NetworkClient.UnregisterHandler<LoadEnvMessage>();
        }
    }

    private void Update()
    {
        // Update network status display if you have one
        UpdateNetworkStatus();

        if (MenuUI != null && MenuUI.activeSelf)
        {
            if (CursorManager.Instance != null)
            {
                // Force refresh để đảm bảo MainMenu có priority cao nhất
                // Force refresh to ensure MainMenu has highest priority
                CursorManager.Instance.ShowCursor(CursorManager.CursorPriority.MainMenu);
            }
            else if (!Cursor.visible || Cursor.lockState != CursorLockMode.None)
            {
                Debug.LogWarning("[MainMenuUI] Cursor was hidden! Restoring...");
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
    }

    private void UpdateNetworkStatus()
    {
        if (NetworkStatusText == null) return;

        if (NetworkServer.active && NetworkClient.isConnected)
        {
            NetworkStatusText.text = $"<b>Host</b>: running via {Transport.active}";
        }
        else if (NetworkServer.active)
        {
            NetworkStatusText.text = $"<b>Server</b>: running via {Transport.active}";
        }
        else if (NetworkClient.isConnected)
        {
            NetworkStatusText.text = $"<b>Client</b>: connected to {manager.networkAddress} via {Transport.active}";
        }
        else if (isConnecting)
        {
            NetworkStatusText.text = $"Connecting to {manager.networkAddress}...";
        }
        else
        {
            NetworkStatusText.text = "Disconnected";
        }
    }

    // ----------------- Button handlers -----------------
    private void OnHostClicked()
    {
        if (manager == null) return;
        
        // Fade out music when entering game scene
        AudioManager.Instance.StopMusic(1.5f);
        // Play ambient sound when game scene loads
        AudioManager.Instance.PlayGameAmbientWithFade();

        // Show loading screen
        ShowLoadingScreen("Starting Host...");

        // Update network address from input field
        if (ServerAddressInput != null && !string.IsNullOrWhiteSpace(ServerAddressInput.text))
            manager.networkAddress = ServerAddressInput.text.Trim();

        if (manager.isNetworkActive)
        {
            Debug.LogWarning("[Menu] NetworkManager already active. Entering gameplay UI.");
            HideLoadingScreen();
            EnterGameplayUI();
            var p = FindObjectOfType<PlayerFreezeUntilReady>();
            if (p != null) p.ForceActivate();
            return;
        }

#if UNITY_WEBGL
        Debug.Log("[Menu] Starting Single Player (WebGL build)...");
        NetworkServer.listen = false;
        manager.StartHost();
#else
        Debug.Log("[Menu] Starting Host (server + local client)...");
        manager.StartHost();
#endif

        StartCoroutine(WaitForHostThenEnter());
    }

    private void OnClientClicked()
    {
        if (manager == null) return;

        // Fade out music when entering game scene
        AudioManager.Instance.StopMusic(1.5f);
        // Play ambient sound when game scene loads
        AudioManager.Instance.PlayGameAmbientWithFade();

        string addr = "localhost";
        if (ServerAddressInput != null && !string.IsNullOrWhiteSpace(ServerAddressInput.text))
            addr = ServerAddressInput.text.Trim();

        manager.networkAddress = addr;

        Debug.Log($"[Menu] Client connecting to {manager.networkAddress}");

        // Show loading screen
        ShowLoadingScreen($"Connecting to {addr}...");

        if (clientConnectCoroutine != null) StopCoroutine(clientConnectCoroutine);
        clientConnectCoroutine = StartCoroutine(ClientConnectRoutine(manager));
    }

    private void OnPortChanged(string newPort)
    {
        if (PortInput != null && Transport.active is PortTransport portTransport)
        {
            if (ushort.TryParse(newPort, out ushort port))
            {
                portTransport.Port = port;
                Debug.Log($"[Menu] Port changed to: {port}");
            }
        }
    }

    private void OnQuitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ----------------- Loading Screen Methods -----------------
    private void ShowLoadingScreen(string statusMessage = "Loading...")
    {
        if (LoadingScreen != null)
        {
            LoadingScreen.SetActive(true);
        }

        if (LoadingStatusText != null)
        {
            LoadingStatusText.text = statusMessage;
        }

        if (LoadingSpinner != null)
        {
            LoadingSpinner.SetActive(true);
            // Start the pulsing animation
            spinnerPulseCoroutine = StartCoroutine(PulseSpinner());
        }

        // Disable menu buttons during loading
        SetMenuButtonsInteractable(false);
    }

    private void HideLoadingScreen()
    {
        if (LoadingScreen != null)
        {
            LoadingScreen.SetActive(false);
        }

        if (LoadingSpinner != null)
        {
            LoadingSpinner.SetActive(false);
            // Stop the pulsing animation
            if (spinnerPulseCoroutine != null)
            {
                StopCoroutine(spinnerPulseCoroutine);
                spinnerPulseCoroutine = null;
            }
        }

        // Re-enable menu buttons if needed
        SetMenuButtonsInteractable(true);
    }

    private IEnumerator PulseSpinner()
    {
        // Ensure the spinner has an Image component
        Image spinnerImage = LoadingSpinner.GetComponent<Image>();
        if (spinnerImage == null) yield break;

        float pulseSpeed = 2f; // Speed of the pulse effect
        float minAlpha = 0.3f; // Minimum alpha value (faded)
        float maxAlpha = 1.0f; // Maximum alpha value (fully lit)
        float currentTime = 0f;

        while (true)
        {
            // Calculate alpha using sine wave for smooth pulsing
            float alpha = minAlpha + (maxAlpha - minAlpha) * (Mathf.Sin(currentTime * pulseSpeed) + 1f) / 2f;

            // Apply the alpha to the spinner image
            Color color = spinnerImage.color;
            color.a = alpha;
            spinnerImage.color = color;

            currentTime += Time.deltaTime;
            yield return null;
        }
    }

    private void SetMenuButtonsInteractable(bool interactable)
    {
        if (HostButton != null) HostButton.interactable = interactable;
        if (ClientButton != null) ClientButton.interactable = interactable;
        if (QuitButton != null) QuitButton.interactable = interactable;
        if (ServerAddressInput != null) ServerAddressInput.interactable = interactable;
        if (PortInput != null) PortInput.interactable = interactable;
    }

    private void UpdateLoadingStatus(string message)
    {
        if (LoadingStatusText != null)
        {
            LoadingStatusText.text = message;
        }
    }

    // ----------------- Network Operations -----------------
    private IEnumerator WaitForHostThenEnter()
    {
        UpdateLoadingStatus("Starting server...");

        float timer = 0f;
        while (!NetworkServer.active && timer < hostStartupWait)
        {
            timer += Time.deltaTime;
            UpdateLoadingStatus($"Starting server... {Mathf.FloorToInt(timer / hostStartupWait * 100)}%");
            yield return null;
        }

        if (!NetworkServer.active)
        {
            UpdateLoadingStatus("Server taking longer than expected...");
            Debug.LogWarning("[Menu] NetworkServer.active still false after wait - proceeding anyway.");
        }

        UpdateLoadingStatus("Connecting local client...");
        timer = 0f;
        while (!NetworkClient.isConnected && timer < hostStartupWait)
        {
            timer += Time.deltaTime;
            UpdateLoadingStatus($"Connecting local client... {Mathf.FloorToInt(timer / hostStartupWait * 100)}%");
            yield return null;
        }

        if (!NetworkClient.isConnected)
        {
            UpdateLoadingStatus("Local client connection delayed...");
            Debug.LogWarning("[Menu] Local client not connected quickly after StartHost.");
        }

        // Host loads environment scenes
        UpdateLoadingStatus("Loading environment...");
        yield return StartCoroutine(LoadMultipleScenesAdditiveWithProgress(environmentSceneNames));

        HideLoadingScreen(); // Hide loading screen before entering gameplay
        EnterGameplayUI();
        var p = FindObjectOfType<PlayerFreezeUntilReady>();
        if (p != null) p.ForceActivate();
    }

    private IEnumerator ClientConnectRoutine(NetworkManager nm)
    {
        isConnecting = true;

        if (nm.transport == null)
        {
            Debug.LogError("[Menu] NetworkManager.transport is null! Assign a transport (KcpTransport/Telepathy) in the NetworkManager inspector.");
            UpdateLoadingStatus("Error: No transport configured!");
            yield return new WaitForSeconds(2f);
            HideLoadingScreen();
            isConnecting = false;
            clientConnectCoroutine = null;
            yield break;
        }

        if (NetworkClient.isConnecting)
        {
            Debug.LogWarning("[Menu] NetworkClient already connecting.");
            UpdateLoadingStatus("Already connecting...");
            yield return new WaitForSeconds(1f);
            HideLoadingScreen();
            isConnecting = false;
            clientConnectCoroutine = null;
            yield break;
        }

        bool exceptionOccurred = false;
        try
        {
            nm.StartClient();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Menu] StartClient threw exception: {ex}");
            UpdateLoadingStatus($"Error: {ex.Message}");
            exceptionOccurred = true;
        }

        if (exceptionOccurred)
        {
            yield return new WaitForSeconds(2f);
            HideLoadingScreen();
            isConnecting = false;
            clientConnectCoroutine = null;
            yield break;
        }

        float timer = 0f;
        while (!NetworkClient.isConnected && timer < clientConnectTimeout && isConnecting)
        {
            timer += Time.deltaTime;
            float progress = Mathf.Clamp01(timer / clientConnectTimeout);
            UpdateLoadingStatus($"Connecting... {Mathf.FloorToInt(progress * 100)}%");
            yield return null;
        }

        isConnecting = false;

        if (NetworkClient.isConnected)
        {
            Debug.Log("[Menu] Client successfully connected to server.");

            // ⭐ CRITICAL FIX: Mark client as ready immediately after connection
            bool readySuccess = false;
            if (!NetworkClient.ready)
            {
                try
                {
                    NetworkClient.Ready();
                    Debug.Log("[Menu] ✓ Client marked as READY");
                    UpdateLoadingStatus("Connected! Marking as ready...");
                    readySuccess = true;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Menu] Failed to mark client as ready: {ex}");
                    UpdateLoadingStatus("Error: Failed to mark ready!");
                    readySuccess = false;
                }
            }
            else
            {
                readySuccess = true;
            }

            // Check if marking ready failed
            if (!readySuccess)
            {
                yield return new WaitForSeconds(2f);
                HideLoadingScreen();

                if (NetworkManager.singleton != null)
                {
                    NetworkManager.singleton.StopClient();
                }
                clientConnectCoroutine = null;
                yield break;
            }

            // Small delay to ensure Ready message is processed
            yield return new WaitForSeconds(0.2f);

            UpdateLoadingStatus("Ready! Loading environment...");

            if (clientLoadEnvironmentOnConnect)
            {
                // Load environment scenes
                yield return StartCoroutine(LoadMultipleScenesAdditiveWithProgress(environmentSceneNames));

                // ⭐ Send EnvLoadedMessage AFTER scenes are loaded AND we're ready
                if (NetworkClient.isConnected && NetworkClient.ready)
                {
                    bool sendSuccess = false;
                    try
                    {
                        var envMsg = new EnvLoadedMessage();
                        NetworkClient.Send(envMsg);
                        Debug.Log("[Menu] ✓ Sent EnvLoadedMessage to server");
                        UpdateLoadingStatus("Environment loaded! Notifying server...");
                        sendSuccess = true;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[Menu] Failed to send EnvLoadedMessage: {ex}");
                        sendSuccess = false;
                    }

                    if (sendSuccess)
                    {
                        // Brief delay to let message send
                        yield return new WaitForSeconds(0.1f);
                    }
                }
                else
                {
                    Debug.LogWarning($"[Menu] Cannot send EnvLoadedMessage - Connected: {NetworkClient.isConnected}, Ready: {NetworkClient.ready}");
                }

                HideLoadingScreen();
                EnterGameplayUI();

                var p = FindObjectOfType<PlayerFreezeUntilReady>();
                if (p != null)
                {
                    p.ForceActivate();
                    Debug.Log("[Menu] PlayerFreezeUntilReady activated");
                }
            }
            else
            {
                // Client doesn't load environment - just wait for server
                UpdateLoadingStatus("Waiting for server data...");

                HideLoadingScreen();
                EnterGameplayUI();

                var p = FindObjectOfType<PlayerFreezeUntilReady>();
                if (p != null)
                {
                    p.ForceActivate();
                    Debug.Log("[Menu] PlayerFreezeUntilReady activated");
                }
            }
        }
        else
        {
            UpdateLoadingStatus("Connection failed!");
            Debug.LogError("[Menu] Client failed to connect within timeout.");

            // Show error for a moment before hiding
            yield return new WaitForSeconds(2f);

            HideLoadingScreen();

            if (NetworkManager.singleton != null)
            {
                NetworkManager.singleton.StopClient();
            }
            else
            {
                NetworkClient.Disconnect();
            }
        }

        clientConnectCoroutine = null;
    }

    // ----------------- Message handler -----------------
    private void OnLoadEnvMessage(LoadEnvMessage msg)
    {
        Debug.Log("[Client] Received LoadEnvMessage from server. Loading scenes...");

        bool allLoaded = true;
        foreach (var s in msg.sceneNames)
        {
            if (!IsSceneLoaded(s)) { allLoaded = false; break; }
        }

        if (allLoaded)
        {
            Debug.Log("[Client] All requested scenes already loaded locally. Would send EnvLoadedMessage (but sending is commented).");
            DebugSendEnvLoadedMessage();
            Debug.Log("[ClientDebug] (send commented) EnvLoadedMessage debug wrapper executed (OnLoadEnvMessage).");
            return;
        }

        StartCoroutine(ClientLoadScenesAndNotifyServer(msg.sceneNames));
    }

    private IEnumerator ClientLoadScenesAndNotifyServer(string[] scenes)
    {
        var list = new List<string>(scenes);
        yield return StartCoroutine(LoadMultipleScenesAdditive(list));

        // when done, notify server that client finished loading
        if (NetworkClient.isConnected)
        {
            DebugSendEnvLoadedMessage();
            Debug.Log("[ClientDebug] (send commented) EnvLoadedMessage debug wrapper executed (ClientLoadScenesAndNotifyServer).");
        }
    }

    // ----------------- Scene loading helpers -----------------
    private IEnumerator LoadMultipleScenesAdditive(List<string> scenes)
    {
        if (scenes == null || scenes.Count == 0)
        {
            Debug.Log("[Menu] No environment scenes specified to load.");
            yield break;
        }

        foreach (var scene in scenes)
        {
            if (string.IsNullOrEmpty(scene)) continue;

            if (!Application.CanStreamedLevelBeLoaded(scene))
            {
                Debug.LogError($"[Menu] Scene '{scene}' cannot be loaded — make sure it's added to Build Settings. Skipping.");
                continue;
            }
            if (IsSceneLoaded(scene))
            {
                Debug.Log($"[Menu] Scene '{scene}' already loaded. Skipping.");
                if (NetworkClient.isConnected && !NetworkServer.active)
                    TrySwitchResourceManagersInLoadedScene(scene);
                else
                    Debug.Log($"[Menu] Skipping ResourceManager role switch for scene '{scene}' (host or offline).");
                continue;
            }

            Debug.Log($"[Menu] Loading scene additive: {scene}");
            var op = SceneManager.LoadSceneAsync(scene, LoadSceneMode.Additive);
            if (op == null)
            {
                Debug.LogError($"[Menu] Failed to start async load for '{scene}'.");
                continue;
            }
            while (!op.isDone) yield return null;
            Debug.Log($"[Menu] Scene '{scene}' loaded.");

            if (NetworkClient.isConnected && !NetworkServer.active)
                TrySwitchResourceManagersInLoadedScene(scene);
            else
                Debug.Log($"[Menu] Skipping ResourceManager role switch for scene '{scene}' (host or offline).");

            yield return null;
        }
    }

    // New coroutine with progress tracking (without progress bar)
    private IEnumerator LoadMultipleScenesAdditiveWithProgress(List<string> scenes)
    {
        if (scenes == null || scenes.Count == 0)
        {
            Debug.Log("[Menu] No environment scenes specified to load.");
            UpdateLoadingStatus("No scenes to load.");
            yield break;
        }

        for (int i = 0; i < scenes.Count; i++)
        {
            var scene = scenes[i];
            if (string.IsNullOrEmpty(scene)) continue;

            UpdateLoadingStatus($"Loading {scene} ({i + 1}/{scenes.Count})...");

            if (!Application.CanStreamedLevelBeLoaded(scene))
            {
                Debug.LogError($"[Menu] Scene '{scene}' cannot be loaded.");
                UpdateLoadingStatus($"Error: Scene '{scene}' not in build settings!");
                continue;
            }

            if (IsSceneLoaded(scene))
            {
                UpdateLoadingStatus($"{scene} already loaded.");
                continue;
            }

            var op = SceneManager.LoadSceneAsync(scene, LoadSceneMode.Additive);
            if (op == null)
            {
                Debug.LogError($"[Menu] Failed to start async load for '{scene}'.");
                UpdateLoadingStatus($"Failed to load {scene}");
                continue;
            }

            // Track loading progress
            while (!op.isDone)
            {
                float overallProgress = (i + op.progress) / scenes.Count;
                UpdateLoadingStatus($"Loading {scene}... {Mathf.FloorToInt(overallProgress * 100)}%");
                yield return null;
            }

            Debug.Log($"[Menu] Scene '{scene}' loaded.");
            UpdateLoadingStatus($"{scene} loaded successfully!");

            // Scene-specific setup
            if (NetworkClient.isConnected && !NetworkServer.active)
                TrySwitchResourceManagersInLoadedScene(scene);

            yield return null;
        }

        UpdateLoadingStatus("Environment loaded!");
        yield return new WaitForSeconds(0.5f); // Brief pause to show completion
    }

    // ----------------- UI State Management -----------------
    private void EnterGameplayUI()
    {
        if (MenuUI != null) MenuUI.SetActive(false);
        if (GameplayUI != null) GameplayUI.SetActive(true);

        // SỬ DỤNG CursorManager thay vì điều khiển cursor trực tiếp
        // Use CursorManager instead of direct cursor control
        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.HideCursor(CursorManager.CursorPriority.MainMenu);
        }
    }

    // ----------------- Resource Manager Handling -----------------
    private void TrySwitchResourceManagersInLoadedScene(string sceneName)
    {
        try
        {
            var scene = SceneManager.GetSceneByName(sceneName);
            if (!scene.IsValid() || !scene.isLoaded) return;

            bool isHostProcess = NetworkServer.active && NetworkClient.isConnected;
            bool isPureClient = NetworkClient.isConnected && !NetworkServer.active;
            if (isHostProcess)
            {
                Debug.Log($"[Menu] Not switching ResourceManagers in scene '{sceneName}' because this is a host process.");
                return;
            }
            if (!isPureClient)
            {
                Debug.Log($"[Menu] Not connected as client; skipping ResourceManager role switch for scene '{sceneName}'.");
                return;
            }

            var roots = scene.GetRootGameObjects();
            foreach (var root in roots)
            {
                var rms = root.GetComponentsInChildren<Component>(true)
                             .Where(c => c != null && c.GetType().Name == "ResourceManager")
                             .ToList();

                foreach (var comp in rms)
                {
                    if (comp == null) continue;

                    var rmType = comp.GetType();

                    try
                    {
                        var roleField = rmType.GetField("role", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                        if (roleField != null)
                        {
                            var enumType = roleField.FieldType;
                            var clientEnumVal = Enum.Parse(enumType, "Client");
                            roleField.SetValue(comp, clientEnumVal);
                            Debug.Log($"[Menu] Set ResourceManager.role = Client on '{root.name}' in scene '{sceneName}'.");
                        }
                        else
                        {
                            var roleProp = rmType.GetProperty("role", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                            if (roleProp != null && roleProp.CanWrite)
                            {
                                var clientEnumVal = Enum.Parse(roleProp.PropertyType, "Client");
                                roleProp.SetValue(comp, clientEnumVal);
                                Debug.Log($"[Menu] Set ResourceManager.role (property) = Client on '{root.name}' in scene '{sceneName}'.");
                            }
                            else
                            {
                                Debug.LogWarning($"[Menu] ResourceManager in '{root.name}' has no accessible 'role' field/property.");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[Menu] Failed to set ResourceManager.role on '{root.name}': {ex}");
                    }

                    try
                    {
                        var persistenceField = rmType.GetField("persistence", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                        if (persistenceField != null)
                        {
                            Type nullPersistenceType = null;
                            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                            {
                                Type[] types = null;
                                try { types = a.GetTypes(); } catch { continue; }
                                foreach (var t in types)
                                {
                                    if (t.Name == "NullPersistence")
                                    {
                                        nullPersistenceType = t;
                                        break;
                                    }
                                }
                                if (nullPersistenceType != null) break;
                            }

                            if (nullPersistenceType != null)
                            {
                                var nullPersistInstance = Activator.CreateInstance(nullPersistenceType);
                                persistenceField.SetValue(comp, nullPersistInstance);
                                Debug.Log("[Menu] Set ResourceManager.persistence to NullPersistence via reflection.");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[Menu] Couldn't set NullPersistence on ResourceManager in '{sceneName}': {ex}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Menu] TrySwitchResourceManagersInLoadedScene failed for '{sceneName}': {ex}");
        }
    }

    private bool IsSceneLoaded(string sceneName)
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).name == sceneName) return true;
        return false;
    }

    // ----------------- Debug send wrapper -----------------
    private void DebugSendEnvLoadedMessage()
    {
        try
        {
            var envMsg = new EnvLoadedMessage();
            var t = typeof(EnvLoadedMessage);
            Debug.Log($"[ClientDebug] (wrapper) Would send EnvLoadedMessage. Type.FullName={t.FullName} Assembly={t.Assembly.FullName}");
            Debug.Log($"[ClientDebug] (wrapper) AssemblyQualifiedName={t.AssemblyQualifiedName}");
            Debug.Log($"[ClientDebug] (wrapper) NetworkClient.isConnected={NetworkClient.isConnected} NetworkClient.localPlayer={(NetworkClient.localPlayer != null)}");

            // Uncomment the following line to actually send once you're satisfied types match:
            // NetworkClient.Send(envMsg);

            Debug.Log("[ClientDebug] (wrapper) DebugSendEnvLoadedMessage complete (actual send is commented).");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ClientDebug] DebugSendEnvLoadedMessage threw: {ex}");
        }
    }
}