using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Mirror;

/// <summary>
/// Client helper: loads/unloads scenes additively when instructed by server (via TargetRpc).
/// After scene load it waits for that scene's ResourceManager to register and then requests a snapshot.
/// </summary>
/*public class ClientSceneLoader : MonoBehaviour
{
    [Tooltip("Seconds to wait for a ResourceManager to appear after scene load")]
    public float managerWaitTimeout = 5f;

    /// <summary>
    /// Called by ResourceManagerRouter.TargetInstructLoadScenes on the client.
    /// </summary>
    public void LoadScenesAdditively(string[] sceneNames)
    {
        if (sceneNames == null || sceneNames.Length == 0) return;
        StartCoroutine(LoadScenesCoroutine(sceneNames));
    }

    /// <summary>
    /// Called by ResourceManagerRouter.TargetInstructUnloadScenes on the client.
    /// </summary>
    public void UnloadScenes(string[] sceneNames)
    {
        if (sceneNames == null || sceneNames.Length == 0) return;
        StartCoroutine(UnloadScenesCoroutine(sceneNames));
    }

    private IEnumerator LoadScenesCoroutine(string[] sceneNames)
    {
        foreach (var scene in sceneNames)
        {
            if (string.IsNullOrEmpty(scene)) continue;
            if (IsSceneLoaded(scene))
            {
                Debug.Log($"[ClientSceneLoader] Scene '{scene}' already loaded.");
                continue;
            }

            var op = SceneManager.LoadSceneAsync(scene, LoadSceneMode.Additive);
            if (op == null)
            {
                Debug.LogError($"[ClientSceneLoader] Scene '{scene}' not found in build settings.");
                continue;
            }

            while (!op.isDone) yield return null;
            Debug.Log($"[ClientSceneLoader] Scene '{scene}' loaded. Waiting for ResourceManager registration...");

            // wait for the ResourceManager to register in the registry
            float t = 0f;
            ResourceManager rm = null;
            while (t < managerWaitTimeout)
            {
                rm = ResourceManagerRegistry.GetManagerForScene(scene);
                if (rm != null) break;
                t += Time.deltaTime;
                yield return null;
            }

            if (rm != null)
            {
                Debug.Log($"[ClientSceneLoader] Found ResourceManager for scene '{scene}'. Requesting snapshot...");
                // find adapter on that manager and request snapshot
                var adapter = rm.GetComponents<MonoBehaviour>().OfType<INetworkAdapter>().FirstOrDefault();
                if (adapter != null && adapter.IsClient)
                {
                    adapter.RequestSnapshot();
                }
                else
                {
                    // fallback: send a snapshot request message for that scene
                    if (NetworkClient.isConnected)
                        NetworkClient.Send(new SnapshotRequestMessage { sceneName = scene });
                }
            }
            else
            {
                Debug.LogWarning($"[ClientSceneLoader] No ResourceManager for scene '{scene}' after waiting.");
            }
        }
    }

    private IEnumerator UnloadScenesCoroutine(string[] sceneNames)
    {
        foreach (var scene in sceneNames)
        {
            if (string.IsNullOrEmpty(scene)) continue;
            if (!IsSceneLoaded(scene)) continue;

            var op = SceneManager.UnloadSceneAsync(scene);
            if (op == null) continue;
            while (!op.isDone) yield return null;
            Debug.Log($"[ClientSceneLoader] Unloaded scene '{scene}'.");
        }
    }

    private bool IsSceneLoaded(string sceneName)
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).name == sceneName) return true;
        return false;
    }
}
*/