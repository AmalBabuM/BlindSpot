using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Clearance.Editor
{
    [InitializeOnLoad]
    public static class WebGLReleaseBuild
    {
        const string Request = "Library/ClearanceWebGLBuild.request";
        static WebGLReleaseBuild()
        {
            EditorApplication.delayCall += Process;
            EditorApplication.playModeStateChanged += state => { if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += Process; };
        }
        [MenuItem("Clearance/Build WebGL Release")]
        public static void Start()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning) return;
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            File.WriteAllText(Request, "pending"); Process();
        }
        static void Process()
        {
            if (!File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning) return;
            string state = File.ReadAllText(Request).Trim();
            if (state != "pending" && state != "switching") return;
            try
            {
                if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
                {
                    File.WriteAllText(Request, "switching");
                    if (!EditorUserBuildSettings.SwitchActiveBuildTargetAsync(BuildTargetGroup.WebGL, BuildTarget.WebGL))
                        throw new InvalidOperationException("Unity could not switch to WebGL; check Web Build Support installation.");
                    return;
                }
                // This template owns Start Game and the pause/report return-to-menu destination.
                PlayerSettings.WebGL.template = "PROJECT:ClearanceIntro";
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/GroundFloor.unity", true) };
                File.WriteAllText(Request, "building");
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                    scenes = new[] { "Assets/Scenes/GroundFloor.unity" }, target = BuildTarget.WebGL,
                    locationPathName = "Builds/WebGL", options = BuildOptions.None
                });
                var summary = report.summary;
                string result = summary.result + ": errors=" + summary.totalErrors + ", warnings=" + summary.totalWarnings +
                    ", size=" + summary.totalSize + " bytes, duration=" + summary.totalTime + ", output=" + summary.outputPath;
                Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/WebGL-release-build.txt", result);
                File.WriteAllText(Request, summary.result == BuildResult.Succeeded ? "complete" : "failed");
                Debug.Log(result);
            }
            catch (Exception e) { File.WriteAllText(Request, "failed: " + e); Debug.LogException(e); }
        }
    }
}
