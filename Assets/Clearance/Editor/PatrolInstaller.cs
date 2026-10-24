using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Clearance.Editor
{
    [InitializeOnLoad]
    public static class PatrolInstaller
    {
        const string Request = "Library/ClearancePatrol.request";
        static PatrolInstaller() => EditorApplication.delayCall += Process;
        static void Process()
        {
            if (!File.Exists(Request) || File.ReadAllText(Request).Trim() != "pending" || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (SceneManager.GetActiveScene().name != "GroundFloor") return;
            try { Install(); File.WriteAllText(Request, "installed"); GroundFloorSmokeTest.Start(); }
            catch (System.Exception e) { File.WriteAllText(Request, "failed: " + e); Debug.LogException(e); }
        }
        [MenuItem("Clearance/Add Ground Floor Prisoner Patrol")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var mission = Object.FindFirstObjectByType<GroundFloorMission>();
            if (!mission) throw new System.InvalidOperationException("Open the ground-floor scene first.");
            var suspect = mission.Suspect;
            if (suspect.GetComponent<SuspectPatrol>()) return;
            Transform Point(string name, Vector3 position)
            {
                var point = new GameObject(name);
                Undo.RegisterCreatedObjectUndo(point, "Add patrol waypoint");
                point.transform.SetParent(mission.transform);
                point.transform.position = position;
                return point.transform;
            }
            var a = Point("Patrol_A", new Vector3(-13.3f, .05f, -4.5f));
            var b = Point("Patrol_B", new Vector3(-10.7f, .05f, -4.5f));
            Undo.AddComponent<SuspectPatrol>(suspect.gameObject).Configure(a, b, Object.FindFirstObjectByType<FirstPersonOfficer>());
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            Debug.Log("CLEARANCE: A/B patrol installed. Approach within 4.2 m to alert; aim and press Q to request hands up.");
        }
    }
}
