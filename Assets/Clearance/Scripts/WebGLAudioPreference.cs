using System.Runtime.InteropServices;
using UnityEngine;

namespace Clearance
{
    // The launch menu selects audio before Unity starts, including scene restarts.
    public static class WebGLAudioPreference
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern int ClearanceSoundMuted();
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Apply()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            AudioListener.volume = ClearanceSoundMuted() != 0 ? 0 : 1;
#endif
        }
    }
}
