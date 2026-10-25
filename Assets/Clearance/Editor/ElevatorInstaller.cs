using System.IO;
using System.Linq;
using GreyDoorGames;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Clearance.Editor
{
    [InitializeOnLoad]
    public static class ElevatorInstaller
    {
        const string Request = "Library/ClearanceElevator.request";
        static ElevatorInstaller() => EditorApplication.delayCall += Process;
        static void Process()
        {
            if (!File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().name != "GroundFloor") return;
            string request = File.ReadAllText(Request).Trim();
            if (request != "pending" && request != "test") return;
            File.WriteAllText(Request, "installing");
            try { if (request == "pending") Install(); File.WriteAllText(Request, "installed"); ElevatorSmokeTest.Start(); }
            catch (System.Exception e) { File.WriteAllText(Request, "failed: " + e); Debug.LogException(e); }
        }
        [MenuItem("Clearance/Add 3D Unsafe Elevator")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Object.FindFirstObjectByType<UnsafeElevator>()) return;
            var root = new GameObject("Elevator_3D_Unsafe");
            Undo.RegisterCreatedObjectUndo(root, "Add 3D elevator");
            root.transform.position = new Vector3(1, .02f, 5.6f);
            var elevator = root.AddComponent<UnsafeElevator>();
            var steel = Material("LiftSteel", new Color(.25f, .29f, .32f), null);
            var dark = Material("LiftDark", new Color(.045f, .055f, .065f), null);
            var doors = Material("LiftDoor", Color.white, AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/GreyDoorGames/Simple 2D Elevator System/Art/Sprite/Elevator Door.png"));
            var green = Material("LiftButton", new Color(.3f, .8f, .35f), null);
            Cube("CabinFloor", root.transform, new Vector3(0, -.06f, .8f), new Vector3(2.05f, .12f, 1.65f), steel);
            Cube("CabinBack", root.transform, new Vector3(0, 1.25f, 1.55f), new Vector3(2.05f, 2.5f, .12f), dark);
            Cube("CabinLeft", root.transform, new Vector3(-1.03f, 1.25f, .8f), new Vector3(.12f, 2.5f, 1.65f), steel);
            Cube("CabinRight", root.transform, new Vector3(1.03f, 1.25f, .8f), new Vector3(.12f, 2.5f, 1.65f), steel);
            Cube("CabinRoof", root.transform, new Vector3(0, 2.5f, .8f), new Vector3(2.15f, .12f, 1.7f), steel);
            Cube("FrameLeft", root.transform, new Vector3(-1.4f, 1.2f, -.12f), new Vector3(1, 2.4f, .22f), steel);
            Cube("FrameRight", root.transform, new Vector3(1.4f, 1.2f, -.12f), new Vector3(1, 2.4f, .22f), steel);
            Cube("FrameHeader", root.transform, new Vector3(0, 2.45f, -.12f), new Vector3(3.8f, .3f, .22f), dark);
            var left = Cube("LeftDoor_3D", root.transform, new Vector3(-.46f, 1.125f, .03f), new Vector3(.94f, 2.25f, .12f), doors);
            var right = Cube("RightDoor_3D", root.transform, new Vector3(.46f, 1.125f, .03f), new Vector3(.94f, 2.25f, .12f), doors);
            var button = Cube("CallButton", root.transform, new Vector3(1.32f, 1.2f, -.27f), new Vector3(.18f, .22f, .07f), green);
            var blocker = new GameObject("UnsafeCabinBarrier", typeof(BoxCollider));
            blocker.transform.SetParent(root.transform, false); blocker.transform.localPosition = new Vector3(0, 1.15f, .22f);
            blocker.GetComponent<BoxCollider>().size = new Vector3(1.85f, 2.3f, .1f);
            var obstacle = root.AddComponent<NavMeshObstacle>(); obstacle.shape = NavMeshObstacleShape.Box; obstacle.carving = true;
            obstacle.center = new Vector3(0, 1.25f, .7f); obstacle.size = new Vector3(3.9f, 2.6f, 1.8f);
            var text = new GameObject("ElevatorSign", typeof(TextMesh)); text.transform.SetParent(root.transform, false);
            text.transform.localPosition = new Vector3(0, 2.45f, -.245f);
            var label = text.GetComponent<TextMesh>(); label.text = "LIFT / UPPER FLOOR"; label.anchor = TextAnchor.MiddleCenter;
            label.fontSize = 64; label.characterSize = .045f; label.color = Color.white;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.GetComponent<MeshRenderer>().sharedMaterial = label.font.material;

            var driver = new GameObject("ImportedAnimationDriver", typeof(RectTransform), typeof(Canvas));
            driver.transform.SetParent(root.transform, false);
            driver.transform.localScale = Vector3.one * .01f;
            var canvas = driver.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.enabled = false;
            driver.GetComponent<RectTransform>().sizeDelta = new Vector2(300, 300);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/GreyDoorGames/Simple 2D Elevator System/Prefabs/Elevator/Elevator Door.prefab");
            var imported = (GameObject)PrefabUtility.InstantiatePrefab(source, driver.transform);
            var animation = imported.GetComponent<ElevatorDoorAnimation>();
            imported.GetComponent<Animator>().cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var transforms = imported.GetComponentsInChildren<RectTransform>();
            elevator.Configure(animation, transforms.First(t => t.name == "Left Door"), transforms.First(t => t.name == "Right Door"), left.transform, right.transform, button.transform);
            const string prefabPath = "Assets/Clearance/UnsafeElevator3D.prefab";
            PrefabUtility.SaveAsPrefabAssetAndConnect(root, prefabPath, InteractionMode.AutomatedAction);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene()); AssetDatabase.SaveAssets();
            Selection.activeGameObject = root;
            Debug.Log("CLEARANCE: 3D lift installed. E selects a floor; choosing upper floor opens the imported animated doors and warns that lift travel is unsafe.");
        }
        static Material Material(string name, Color color, Texture texture)
        {
            string path = "Assets/Clearance/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            material.SetColor("_BaseColor", color); material.SetFloat("_Smoothness", .5f);
            if (texture) material.SetTexture("_BaseMap", texture);
            AssetDatabase.CreateAsset(material, path); return material;
        }
        static GameObject Cube(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube); cube.name = name;
            cube.transform.SetParent(parent, false); cube.transform.localPosition = position; cube.transform.localScale = scale;
            cube.GetComponent<Renderer>().sharedMaterial = material; return cube;
        }
    }
}
