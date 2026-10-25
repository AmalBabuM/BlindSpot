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
#endif
        }
    }
}
