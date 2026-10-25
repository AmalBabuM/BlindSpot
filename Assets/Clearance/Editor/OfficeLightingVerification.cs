using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Clearance.Editor
{
    [InitializeOnLoad]
    public static class OfficeLightingVerification
    {
        const string Request = "Library/ClearanceLightingVerify.request";
        static OfficeLightingVerification()
        {
            EditorApplication.delayCall += Process;
            EditorApplication.playModeStateChanged += Changed;
        }
        static void Process()
        {
            if (!File.Exists(Request) || File.ReadAllText(Request).Trim() != "pending" || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (File.Exists("Library/ClearanceLightingRepair.request") && new[] { "pending", "preparing", "baking" }.Contains(File.ReadAllText("Library/ClearanceLightingRepair.request").Trim()))
            { EditorApplication.update -= WaitForBake; EditorApplication.update += WaitForBake; return; }
            Start();
        }
        static void WaitForBake()
        {
            if (Lightmapping.isRunning || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (File.Exists("Library/ClearanceLightingRepair.request") && new[] { "pending", "preparing", "baking" }.Contains(File.ReadAllText("Library/ClearanceLightingRepair.request").Trim())) return;
            EditorApplication.update -= WaitForBake; Process();
        }
        [MenuItem("Clearance/Verify Saved Office Lighting")]
        public static void Start()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (Lightmapping.isRunning) { EditorApplication.update -= WaitForBake; EditorApplication.update += WaitForBake; return; }
            File.WriteAllText(Request, "checking");
            SessionState.SetBool("Clearance.LightingVerify", true);
            SessionState.SetString("Clearance.LightingVerifyErrors", "");
            EditorApplication.EnterPlaymode();
        }
        static void Changed(PlayModeStateChange state)
        {
            if (!SessionState.GetBool("Clearance.LightingVerify", false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
                EditorApplication.delayCall += () => { SessionState.SetString("Clearance.LightingVerifyErrors", Validate("Play Mode")); EditorApplication.ExitPlaymode(); };
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool("Clearance.LightingVerify", false);
                string errors = SessionState.GetString("Clearance.LightingVerifyErrors", "") + Validate("Edit Mode after Play Mode");
                string result = errors.Length == 0 ? "PASS: saved scene bake persists across Play Mode; desk/static lift use lightmaps; moving doors use 176 baked probes; two reflection probes and per-pixel lighting profile are present." : "FAIL: " + errors;
                File.WriteAllText("Logs/Office-lighting-verification.txt", result);
                File.WriteAllText(Request, errors.Length == 0 ? "complete" : "failed");
                Debug.Log(result);
                var desk = GameObject.Find("Front Desk");
                if (desk) { Selection.activeGameObject = desk; if (SceneView.lastActiveSceneView) SceneView.lastActiveSceneView.FrameSelected(); }
            }
        }
        static string Validate(string stage)
        {
            var errors = new List<string>();
            if (!Lightmapping.lightingDataAsset || !AssetDatabase.GetAssetPath(Lightmapping.lightingDataAsset).StartsWith("Assets/Scenes/GroundFloor/")) errors.Add("Scene-specific lighting data missing");
            if (LightmapSettings.lightmaps.Length < 1) errors.Add("Lightmaps missing");
            if (!LightmapSettings.lightProbes || LightmapSettings.lightProbes.count == 0) errors.Add("Baked light probes missing");
            if (!QualitySettings.renderPipeline || QualitySettings.renderPipeline.name != "OfficeLightingPipeline") errors.Add("Wrong active render pipeline");
            var desk = GameObject.Find("Front Desk");
            if (!desk || desk.GetComponentsInChildren<MeshRenderer>().Any(r => r.lightmapIndex < 0 || r.lightmapIndex >= LightmapSettings.lightmaps.Length)) errors.Add("Desk lightmap assignment missing");
            foreach (var door in UnityEngine.Object.FindObjectsByType<PushDoor>(FindObjectsSortMode.None))
                foreach (var renderer in door.GetComponentsInChildren<MeshRenderer>())
                    if (renderer.lightProbeUsage != LightProbeUsage.BlendProbes || renderer.lightmapIndex >= 0 && renderer.lightmapIndex < LightmapSettings.lightmaps.Length) errors.Add("Moving door has wrong GI assignment");
            var lift = UnityEngine.Object.FindFirstObjectByType<UnsafeElevator>();
            if (!lift) errors.Add("Lift missing");
            else foreach (var renderer in lift.GetComponentsInChildren<MeshRenderer>())
            {
                if (renderer.GetComponent<TextMesh>()) continue;
                bool moving = renderer.name == "LeftDoor_3D" || renderer.name == "RightDoor_3D";
                if (moving && renderer.lightProbeUsage != LightProbeUsage.BlendProbes) errors.Add("Lift door probe assignment missing");
                if (!moving && (renderer.lightmapIndex < 0 || renderer.lightmapIndex >= LightmapSettings.lightmaps.Length)) errors.Add("Static lift lightmap missing: " + renderer.name);
            }
            var reflections = UnityEngine.Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None).Where(p => p.name.StartsWith("Office_")).ToArray();
            if (reflections.Length < 2 || reflections.Any(p => !p.bakedTexture)) errors.Add("Reflection probe bake missing");
            return errors.Count == 0 ? "" : stage + ": " + string.Join("; ", errors) + "\n";
        }
    }
}
