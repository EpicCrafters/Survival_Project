using Mirror;

// server -> client: tells client which additive scenes to load
public struct LoadEnvMessage : NetworkMessage
{
    public string[] sceneNames;
}

// client -> server: client tells server it finished loading env scenes
public struct EnvLoadedMessage : NetworkMessage { }
