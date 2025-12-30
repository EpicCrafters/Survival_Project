using UnityEngine;

public class ButtonPlaySound : MonoBehaviour
{
    public void PlayClick()
    {
        AudioManager.Instance.PlayUISFX(SoundType.UI_Click);
    }
}
