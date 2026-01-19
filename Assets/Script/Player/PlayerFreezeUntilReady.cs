using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Mirror;

[DisallowMultipleComponent]
public class PlayerFreezeUntilReady : NetworkBehaviour
{
    private PlayerMovement player;
    private CharacterController controller;
    private bool isReady = false;

    private void Awake()
    {
        player = GetComponent<PlayerMovement>();
        controller = GetComponent<CharacterController>();

        if (player == null)
            Debug.LogError("[PlayerFreezeUntilReady] No PlayerMovement component found on this GameObject!");
        if (controller == null)
            Debug.LogError("[PlayerFreezeUntilReady] No CharacterController found on this GameObject!");
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        StartCoroutine(FreezeUntilAllScenesLoaded());
    }

    private IEnumerator FreezeUntilAllScenesLoaded()
    {
        // Freeze player
        if (player != null) player.enabled = false;
        if (controller != null) controller.enabled = false;
        Debug.Log("[PlayerFreezeUntilReady] PlayerMovement frozen until all scenes loaded.");

        // Wait for MainMenuUI to exist
        MainMenuUI menuUI = null;
        while (menuUI == null)
        {
            menuUI = FindObjectOfType<MainMenuUI>(true);
            yield return null;
        }

        // Wait until all environment scenes are loaded
        while (!AreScenesLoaded(menuUI.environmentSceneNames))
        {
            yield return null;
        }

        // Small extra delay to ensure initialization
        yield return null;

        // Unfreeze player
        if (player != null) player.enabled = true;
        if (controller != null) controller.enabled = true;
        Debug.Log("[PlayerFreezeUntilReady] PlayerMovement unfrozen. Ready to move!");

        isReady = true;
    }

    private bool AreScenesLoaded(List<string> sceneNames)
    {
        if (sceneNames == null || sceneNames.Count == 0) return true;

        foreach (var name in sceneNames)
        {
            if (!SceneManager.GetSceneByName(name).isLoaded)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Optional manual unfreeze (debug/testing)
    /// </summary>
    public void ForceActivate()
    {
        if (isReady) return;

        if (player != null) player.enabled = true;
        if (controller != null) controller.enabled = true;

        isReady = true;
        Debug.Log("[PlayerFreezeUntilReady] ForceActivate called: PlayerMovement unfrozen.");
    }
}
