using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Clearance.Editor
{
    [InitializeOnLoad]
    public static class OfficeOptimization
    {
        const string Request = "Library/ClearanceOptimize.request";
        static OfficeOptimization() => EditorApplication.delayCall += Process;
        static void Process()
        {
            if (!File.Exists(Request) || File.ReadAllText(Request).Trim() != "pending" || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.WriteAllText(Request, "running");
            try { Rebuild(); File.WriteAllText(Request, "complete"); EditorMenuCheck.Start(); }
            catch (Exception error) { File.WriteAllText(Request, "failed: " + error); Debug.LogException(error); }
        }
        [MenuItem("Clearance/Rebake Navigation And Check Performance")]
        public static void Rebuild()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            const string navPath = "Assets/Clearance/OfficeNavigation.asset";
            string backup = "NavigationBackups/" + DateTime.Now.ToString("yyyy-MM-dd-HHmmss");
            Directory.CreateDirectory(backup); if (File.Exists(navPath)) File.Copy(navPath, backup + "/OfficeNavigation.asset");
            int mapsBefore = LightmapSettings.lightmaps.Length;
            OfficeInstaller.RebuildNavigation();
            AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            var office = UnityEngine.Object.FindAnyObjectByType<OfficeEncounter>();
            var data = AssetDatabase.LoadAssetAtPath<NavMeshData>(navPath);
            var instance = NavMesh.AddNavMeshData(data);
            string routes;
            try
            {
                CheckRoute(office.Player.transform.position, office.CustodyPoint.position, "Entrance to evacuation point");
                CheckRoute(office.Player.transform.position, office.NextFloor.position, "Entrance to stair suspect");
                routes = "PASS: entrance/evacuation and entrance/stairs have complete navigation paths.";
            }
            finally { instance.Remove(); }
            if (LightmapSettings.lightmaps.Length != mapsBefore) throw new InvalidOperationException("Navigation rebake changed lighting bindings.");
            WebGLPerformanceCheck.Validate();
            string result = routes + "\nNavmesh saved to " + navPath + "; previous asset in " + backup + ".\nLighting and scene layout preserved; splash setting left as supplied.";
            File.WriteAllText("Logs/Navigation-optimization.txt", result); Debug.Log(result);
        }
        static void CheckRoute(Vector3 from, Vector3 to, string label)
        {
            if (!NavMesh.SamplePosition(from, out var start, 2, NavMesh.AllAreas) || !NavMesh.SamplePosition(to, out var end, 2, NavMesh.AllAreas))
                throw new InvalidOperationException(label + ": endpoint not on navigation mesh.");
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
                throw new InvalidOperationException(label + ": incomplete route after rebake.");
        }
    }
}
