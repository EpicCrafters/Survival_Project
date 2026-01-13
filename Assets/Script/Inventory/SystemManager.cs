using UnityEngine;

public class SystemManager : MonoBehaviour
{
    public static SystemManager Instance;

    private void Awake()
    {
        Instance = this;
    }
}
