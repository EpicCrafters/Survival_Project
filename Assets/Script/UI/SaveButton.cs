using UnityEngine;

public class SaveButton : MonoBehaviour
{
    public void OnSaveClicked()
    {
        var mgr = ResourceManager.Instance;
        if (mgr == null) { Debug.LogWarning("No ResourceManagerOffline instance."); return; }
        mgr.SaveNow();
    }
}
