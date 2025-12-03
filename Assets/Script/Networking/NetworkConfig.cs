using kcp2k;
using UnityEngine;

public class NetworkConfig : MonoBehaviour
{
    void Start()
    {
        // Increase KCP timeouts
        var kcpTransport = GetComponent<KcpTransport>();
        if (kcpTransport != null)
        {
            kcpTransport.Timeout = 30000; // 30 seconds instead of 10
            kcpTransport.Interval = 2000; // Ping every 2 seconds
        }
    }
}