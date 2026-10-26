using UnityEngine;

namespace Clearance
{
    public static class WebGLRuntimeSettings
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            // Let the browser synchronize frames instead of adding a software frame limiter.
            QualitySettings.vSyncCount = 1;
            Application.targetFrameRate = -1;
            // Bound physics catch-up after a long browser frame (five 50 Hz steps).
            Time.maximumDeltaTime = .1f;
#endif
        }
    }
}
