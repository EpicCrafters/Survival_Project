// Put this into your existing MainMenuUI script — replace the old file with this version.
// Changes: debug wrapper for EnvLoadedMessage sending (actual send is commented out by default).
// ... rest of header unchanged ...

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Mirror;

public class MainMenuUI : MonoBehaviour
{
    [Header("UI (assign in inspector)")]
    public GameObject MenuUI;
    public GameObject GameplayUI;
    public Button HostButton;
    public Button ClientButton;
    public Button QuitButton;
    public InputField ServerAddressInput;

    [Header("Environment scenes to load (additive)")]
    [Tooltip("Add one or more environment/ground scenes here (they must be in Build Settings).")]
    public List<string> environmentSceneNames = new List<string>();

    [Header("Networking")]
    [Tooltip("Seconds to wait after StartHost() for the local server+client to come up")]
    public float hostStartupWait = 1.0f;
    [Tooltip("Client connect timeout (seconds)")]
    public float clientConnectTimeout = 10f;

    [Header("Client behaviour")]
    [Tooltip("If true, a client that successfully connects will locally load the environment scenes (same\nbehaviour as Host). If false, client will wait for\nserver's LoadEnvMessage.")]
    public bool clientLoadEnvironmentOnConnect = true;

    private Coroutine clientConnectCoroutine;

    private void Awake()
    {
        if (MenuUI == null) MenuUI = gameObject;
        if (GameplayUI != null) GameplayUI.SetActive(false);

        if (HostButton != null) { HostButton.onClick.RemoveAllListeners(); HostButton.onClick.AddListener(OnHostClicked); }
        if (ClientButton != null) { ClientButton.onClick.RemoveAllListeners(); ClientButton.onClick.AddListener(OnClientClicked); }
        if (QuitButton != null) { QuitButton.onClick.RemoveAllListeners(); QuitButton.onClick.AddListener(OnQuitClicked); }

        // register handler for server->client LoadEnvMessage
        NetworkClient.RegisterHandler<LoadEnvMessage>(OnLoadEnvMessage, false);
    }

    private void OnDestroy()
    {
        if (HostButton != null) HostButton.onClick.RemoveListener(OnHostClicked);
        if (ClientButton != null) ClientButton.onClick.RemoveListener(OnClientClicked);
        if (QuitButton != null) QuitButton.onClick.RemoveListener(OnQuitClicked);
        if (clientConnectCoroutine != null) StopCoroutine(clientConnectCoroutine);

        if (NetworkClient.active)
        {
            NetworkClient.UnregisterHandler<LoadEnvMessage>();
        }
    }

    // ----------------- Button handlers -----------------
    private void OnHostClicked()
    {
        var nm = NetworkManager.singleton;
        if (nm == null)
        {
            Debug.LogError("[Menu] NetworkManager.singleton is null. Put NetworkManager in bootstrap scene.");
            return;
        }

        if (nm.isNetworkActive)
        {
            Debug.LogWarning("[Menu] NetworkManager already active. Entering gameplay UI.");
            EnterGameplayUI();
            var p = FindObjectOfType<PlayerFreezeUntilReady>();
            if (p != null) p.ForceActivate();
            return;
        }

        Debug.Log("[Menu] Starting Host (server + local client)...");
        nm.StartHost();

        StartCoroutine(WaitForHostThenEnter());
    }

    private IEnumerator WaitForHostThenEnter()
    {
        float timer = 0f;
        while (!NetworkServer.active && timer < hostStartupWait)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        if (!NetworkServer.active)
            Debug.LogWarning("[Menu] NetworkServer.active still false after wait - proceeding anyway.");

        timer = 0f;
        while (!NetworkClient.isConnected && timer < hostStartupWait)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        if (!NetworkClient.isConnected)
            Debug.LogWarning("[Menu] Local client not connected quickly after StartHost. But continuing to enter gameplay UI.");

        // Host still loads environment scenes locally so the host's visuals exist immediately.
        yield return StartCoroutine(LoadMultipleScenesAdditive(environmentSceneNames));

        EnterGameplayUI();
        var p = FindObjectOfType<PlayerFreezeUntilReady>();
        if (p != null) p.ForceActivate();
    }

    private void OnClientClicked()
    {
        var nm = NetworkManager.singleton;
        if (nm == null)
        {
            Debug.LogError("[Menu] NetworkManager.singleton is null. Put NetworkManager in bootstrap scene.");
            return;
        }

        if (NetworkServer.active)
        {
            Debug.LogWarning("[Menu] This instance is running as server/host. Use StartHost instead of StartClient.");
            EnterGameplayUI();
            var p = FindObjectOfType<PlayerFreezeUntilReady>();
            if (p != null) p.ForceActivate();
            return;
        }

        string addr = "localhost";
        if (ServerAddressInput != null && !string.IsNullOrWhiteSpace(ServerAddressInput.text))
            addr = ServerAddressInput.text.Trim();

        nm.networkAddress = addr;
        Debug.Log($"[Menu] Client connecting to {nm.networkAddress}");

        if (clientConnectCoroutine != null) StopCoroutine(clientConnectCoroutine);
        clientConnectCoroutine = StartCoroutine(ClientConnectRoutine(nm));
    }

    private IEnumerator ClientConnectRoutine(NetworkManager nm)
    {
        if (nm.transport == null)
        {
            Debug.LogError("[Menu] NetworkManager.transport is null! Assign a transport (KcpTransport/Telepathy) in the NetworkManager inspector.");
            clientConnectCoroutine = null;
            yield break;
        }

        if (NetworkClient.isConnecting)
        {
            Debug.LogWarning("[Menu] NetworkClient already connecting.");
            clientConnectCoroutine = null;
            yield break;
        }

        try
        {
            nm.StartClient();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Menu] StartClient threw exception: {ex}");
            clientConnectCoroutine = null;
            yield break;
        }

        float timer = 0f;
        while (!NetworkClient.isConnected && timer < clientConnectTimeout)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        if (NetworkClient.isConnected)
        {
            Debug.Log("[Menu] Client successfully connected to server.");

            // NEW: Optionally load environment scenes locally for client (same as Host)
            if (clientLoadEnvironmentOnConnect)
            {
                Debug.Log("[Menu] Client will load environment scenes locally (clientLoadEnvironmentOnConnect = true).");
                yield return StartCoroutine(LoadMultipleScenesAdditive(environmentSceneNames));

                // After local load, notify server that we're ready if you expect server handshakes.
                if (NetworkClient.isConnected)
                {
                    // DEBUG: replaced send with debug wrapper (actual send is commented out)
                    DebugSendEnvLoadedMessage();
                    // NetworkClient.Send(new EnvLoadedMessage()); // <-- original (COMMENTED)
                    Debug.Log("[ClientDebug] (send commented) EnvLoadedMessage debug wrapper executed (clientConnect).");
                }

                EnterGameplayUI();
                var p = FindObjectOfType<PlayerFreezeUntilReady>();
                if (p != null) p.ForceActivate();
            }
            else
            {
                EnterGameplayUI();
                var p = FindObjectOfType<PlayerFreezeUntilReady>();
                if (p != null) p.ForceActivate();
            }
        }
        else
        {
            Debug.LogError("[Menu] Client failed to connect within timeout. Stopping client and showing error.");
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
            // DEBUG: replaced send with debug wrapper (actual send is commented out)
            DebugSendEnvLoadedMessage();
            // NetworkClient.Send(new EnvLoadedMessage()); // <-- original (COMMENTED)
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
            // DEBUG: replaced send with debug wrapper (actual send is commented out)
            DebugSendEnvLoadedMessage();
            // NetworkClient.Send(new EnvLoadedMessage()); // <-- original (COMMENTED)
            Debug.Log("[ClientDebug] (send commented) EnvLoadedMessage debug wrapper executed (ClientLoadScenesAndNotifyServer).");
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

    // ----------------- Scene loading helper (modified) -----------------
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

    // search the named scene for ResourceManager components and, when found, try switch to client role
    // SAFE: only change ResourceManager role on pure client processes
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

    // ----------------- Helpers ----------------    -----------------
    private void EnterGameplayUI()
    {
        if (MenuUI != null) MenuUI.SetActive(false);
        if (GameplayUI != null) GameplayUI.SetActive(true);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // ----------------- Debug send wrapper ----------------
    // This prints the Type.FullName, Assembly.FullName and AssemblyQualifiedName for EnvLoadedMessage.
    // The actual NetworkClient.Send call is intentionally commented out so you can inspect assemblies first.
    private void DebugSendEnvLoadedMessage()
    {
        try
        {
            var envMsg = new EnvLoadedMessage(); // keep minimal
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
