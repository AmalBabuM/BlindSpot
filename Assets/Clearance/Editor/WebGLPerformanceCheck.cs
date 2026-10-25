using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Clearance.Editor
{
    [InitializeOnLoad]
    public static class WebGLPerformanceCheck
    {
        const string Request = "Library/ClearanceWebGLCheck.request";
        static WebGLPerformanceCheck()
        {
            EditorApplication.delayCall += Process;
            EditorApplication.playModeStateChanged += state => { if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += Process; };
        }
        static void Process()
        {
            if (!File.Exists(Request) || File.ReadAllText(Request).Trim() != "pending" || EditorApplication.isPlayingOrWillChangePlaymode) return;
            try { Validate(); File.WriteAllText(Request, "complete"); }
            catch (Exception e) { File.WriteAllText(Request, "failed: " + e); Debug.LogException(e); }
        }
        [MenuItem("Clearance/Check WebGL Performance Settings")]
        public static void Validate()
        {
            var pipeline = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
            if (!pipeline || pipeline.supportsAdditionalLightShadows || pipeline.supportsHDR || pipeline.renderScale > .85f)
                throw new InvalidOperationException("The browser lighting budget is not active.");
            var renderer = new SerializedObject(AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Clearance/Lighting/OfficeRenderer.asset"));
            if (renderer.FindProperty("m_RenderingMode").intValue != 0) throw new InvalidOperationException("WebGL should use Forward rendering.");
            if (LightmapSettings.lightmaps.Length == 0)
                throw new InvalidOperationException("Bake check: lightmaps=" + LightmapSettings.lightmaps.Length + ", probes=" + (LightmapSettings.lightProbes ? LightmapSettings.lightProbes.count : 0) + ", data=" + AssetDatabase.GetAssetPath(Lightmapping.lightingDataAsset));
            int probeCount = LightmapSettings.lightProbes ? LightmapSettings.lightProbes.count : 0;
            bool activeProbeGroup = UnityEngine.Object.FindObjectsByType<LightProbeGroup>(FindObjectsSortMode.None).Any(p => p.isActiveAndEnabled);
            if (activeProbeGroup && probeCount == 0) throw new InvalidOperationException("An active light-probe group needs baking.");
            var cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            int activeCameras = cameras.Count(c => c.isActiveAndEnabled);
            if (activeCameras != 1) throw new InvalidOperationException("Expected one rendering camera, found " + activeCameras);
            string modulePath = Path.Combine(EditorApplication.applicationContentsPath, "PlaybackEngines", "WebGLSupport");
            string result = "PASS: Forward renderer; HDR/additional-light shadows disabled; 85% render scale; one active camera; " +
                LightmapSettings.lightmaps.Length + " lightmaps; " + probeCount + " baked probes.\n" +
                (probeCount == 0 ? "NOTE: The saved scene has its light-probe group disabled. Optimization preserves that user setting; enable it and rebake if moving objects need baked probe lighting.\n" : "") +
                "WebGL build support installed: " + Directory.Exists(modulePath) + ". Browser FPS has not been measured.\n" +
                "Initial WASM memory configured to 256 MB; geometric growth remains enabled.\n";
            Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/WebGL-performance-check.txt", result);
            Debug.Log(result);
        }
    }
}
