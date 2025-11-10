using System;
using Mirror;
using UnityEngine;

/// <summary>
/// Register a simple debug handler for EnvLoadedMessage on the server.
/// </summary>
public class DebugServerMessageHooks : MonoBehaviour
{
    void Awake()
    {
        // optional: you can call TryRegister here too, but Start is often safer when
        // NetworkServer may not yet be active in Awake.
    }

    void Start()
    {
        TryRegister();
    }

    private void TryRegister()
    {
        try
        {
            // Only attempt to register on server/host processes
            if (!NetworkServer.active)
            {
                Debug.Log("[ServerDebug] NetworkServer is not active - skipping EnvLoadedMessage registration.");
                return;
            }

            // ensure a single registration
            NetworkServer.UnregisterHandler<EnvLoadedMessage>();
            NetworkServer.RegisterHandler<EnvLoadedMessage>(WrappedEnvLoadedHandler, false);

            var t = typeof(EnvLoadedMessage);
            Debug.Log($"[ServerDebug] Registered EnvLoadedMessage handler. Type.FullName={t.FullName} Assembly={t.Assembly.FullName}");
            Debug.Log($"[ServerDebug] Registered EnvLoadedMessage. AssemblyQualifiedName={t.AssemblyQualifiedName}");
        }
        catch (Exception ex)
        {
            Debug.LogError("[ServerDebug] Register failed: " + ex);
        }
    }

    // NOTE: Use NetworkConnectionToClient for server-side handlers.
    private void WrappedEnvLoadedHandler(NetworkConnectionToClient conn, EnvLoadedMessage msg)
    {
        Debug.Log($"[ServerDebug] WrappedEnvLoadedHandler invoked. connId={(conn != null ? conn.connectionId : -1)}");
        // Keep it minimal — don't call router here while debugging.
    }
}
