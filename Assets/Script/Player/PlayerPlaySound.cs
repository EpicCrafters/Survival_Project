using UnityEngine;

public class PlayerPlaySound : MonoBehaviour
{
    public void PlayFootStep()
    {
        AudioManager.Instance.PlaySFX(SoundType.PlayerFootstep);
    }
    public void PlayHit()
    {
        AudioManager.Instance.PlaySFX(SoundType.Hit);
    }
    public void PlayChop()
    {
        AudioManager.Instance.PlaySFX(SoundType.Chop);
    }
}
