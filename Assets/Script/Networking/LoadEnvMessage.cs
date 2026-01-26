using Mirror;

// server -> client: tells client which additive scenes to load
public struct LoadEnvMessage : NetworkMessage
{
    public string[] sceneNames;
}

