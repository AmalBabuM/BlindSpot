using UnityEngine;

namespace Clearance
{
    // Shares the in-player menu preference; no dependency on a host page global.
    public static class WebGLAudioPreference
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Apply()
        {
            AudioListener.volume = PlayerPrefs.GetInt("Clearance.SoundMuted", 0) != 0 ? 0 : 1;
        }
    }
}
