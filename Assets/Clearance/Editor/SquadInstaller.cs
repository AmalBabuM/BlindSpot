using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Clearance.Editor
{
    [InitializeOnLoad]
    public static class SquadInstaller
    {
        const string Request = "Library/ClearanceSquad.request";
        static SquadInstaller() => EditorApplication.delayCall += Process;
        static void Process()
        {
            if (!File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().name != "GroundFloor") return;
            string request = File.ReadAllText(Request).Trim();
            if (request != "pending" && request != "test") return;
            File.WriteAllText(Request, "installing");
            try { if (request == "pending") Install(); File.WriteAllText(Request, "installed"); SquadSmokeTest.Start(); }
            catch (System.Exception e) { File.WriteAllText(Request, "failed: " + e); Debug.LogException(e); }
        }
        [MenuItem("Clearance/Set Up Assisting Officer")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var mission = Object.FindFirstObjectByType<GroundFloorMission>();
            if (!mission) throw new System.InvalidOperationException("Open the ground-floor scene first.");
            if (mission.GetComponent<SquadCoordinator>()) { SingleAssistantInstaller.Install(); return; }
            var player = Object.FindFirstObjectByType<FirstPersonOfficer>();
            var level = GameObject.Find("_Level");
            if (!level) throw new System.InvalidOperationException("The scene has no _Level geometry root.");

            // Bake only environment colliders. Doors remain interactive and officers open them en route.
            var sources = new List<NavMeshBuildSource>();
            var marks = new List<NavMeshBuildMarkup>();
            foreach (var door in level.GetComponentsInChildren<InteractableDoor>())
                marks.Add(new NavMeshBuildMarkup { root = door.transform, ignoreFromBuild = true });
            NavMeshBuilder.CollectSources(level.transform, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, marks, sources);
            var settings = NavMesh.GetSettingsByIndex(0);
            settings.agentRadius = .27f; settings.agentHeight = 1.8f; settings.agentClimb = .25f;
            settings.overrideVoxelSize = true; settings.voxelSize = .08f;
            var renderers = level.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            bounds.Expand(4);
            var navigation = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            if (!navigation) throw new System.InvalidOperationException("Could not build ground-floor navigation.");
            navigation.name = "GroundFloorSquadNavigation";
            string navPath = "Assets/Clearance/GroundFloorSquadNavigation.asset";
            var existing = AssetDatabase.LoadAssetAtPath<NavMeshData>(navPath);
            if (existing) { NavMeshBuilder.UpdateNavMeshData(existing, settings, sources, bounds); Object.DestroyImmediate(navigation); navigation = existing; }
            else AssetDatabase.CreateAsset(navigation, navPath);

            const string modelPath = "Assets/Character/Police_officer/Base_mesh/police_officer.fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.SaveAndReimport();
            var avatar = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Avatar>().FirstOrDefault(a => a.isHuman && a.isValid);
            if (!avatar) throw new System.InvalidOperationException("The police officer could not be mapped to a humanoid avatar.");
            AnimationClip Clip(string name) => AssetDatabase.LoadAllAssetsAtPath("Assets/Character/Prisoner/source/Prison@" + name + ".fbx")
                .OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview"));
            string controllerPath = "Assets/Clearance/PoliceTeammate.controller";
            var animation = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (!animation)
            {
                animation = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                animation.AddParameter("Speed", AnimatorControllerParameterType.Float);
                var machine = animation.layers[0].stateMachine;
                var idle = machine.AddState("Idle"); idle.motion = Clip("Breathing Idle");
                var walking = machine.AddState("Walking"); walking.motion = Clip("Walking"); walking.speed = 1.4f;
                machine.defaultState = idle;
                var move = idle.AddTransition(walking); move.hasExitTime = false; move.duration = .15f;
                move.AddCondition(AnimatorConditionMode.Greater, .08f, "Speed");
                var stop = walking.AddTransition(idle); stop.hasExitTime = false; stop.duration = .15f;
                stop.AddCondition(AnimatorConditionMode.Less, .08f, "Speed");
            }
            var squad = Undo.AddComponent<SquadCoordinator>(mission.gameObject);
            var team = new AssistingOfficer[1];
            var coverPositions = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                var point = new GameObject(i == 0 ? "TeamCover_Left" : "TeamCover_Right");
                Undo.RegisterCreatedObjectUndo(point, "Create cover position");
                point.transform.SetParent(mission.transform);
                point.transform.position = mission.Corners[i].transform.position + new Vector3(i == 0 ? .3f : -.3f, -1.6f, .7f);
                coverPositions[i] = point.transform;
                if (i >= team.Length) continue;
                var root = new GameObject("AssistingOfficer_" + (i + 1));
                Undo.RegisterCreatedObjectUndo(root, "Add assisting officer");
                root.transform.SetParent(mission.transform);
                root.transform.position = player.transform.position + player.transform.right * (i == 0 ? -.85f : .85f) - player.transform.forward * .5f;
                root.transform.rotation = player.transform.rotation;
                var agent = root.AddComponent<NavMeshAgent>();
                agent.enabled = false; agent.agentTypeID = settings.agentTypeID;
                agent.radius = .27f; agent.height = 1.8f; agent.speed = 2.9f; agent.acceleration = 10;
                agent.angularSpeed = 300; agent.avoidancePriority = 35 + i * 10;
                var capsule = root.AddComponent<CapsuleCollider>();
                capsule.height = 1.8f; capsule.radius = .25f; capsule.center = Vector3.up * .9f;
                var body = root.AddComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Character/Police_officer/Prefab/police_officer.prefab"), root.transform);
                visual.transform.localPosition = Vector3.zero; visual.transform.localRotation = Quaternion.identity;
                foreach (var renderer in visual.GetComponentsInChildren<Renderer>()) renderer.shadowCastingMode = ShadowCastingMode.On;
                foreach (var collider in visual.GetComponentsInChildren<Collider>()) collider.enabled = false;
                var animator = visual.GetComponent<Animator>();
                animator.avatar = avatar; animator.runtimeAnimatorController = animation;
                animator.applyRootMotion = false; animator.enabled = true; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var eye = new GameObject("CornerObservation", typeof(Camera));
                eye.transform.SetParent(root.transform, false); eye.transform.localPosition = new Vector3(0, 1.65f, .12f);
                var camera = eye.GetComponent<Camera>(); camera.enabled = false;
                var torch = new GameObject("OfficerFlashlight", typeof(Light));
                torch.transform.SetParent(eye.transform, false);
                var light = torch.GetComponent<Light>(); light.type = LightType.Spot;
                light.intensity = 3; light.range = 10; light.spotAngle = 65; light.innerSpotAngle = 35;
                team[i] = root.AddComponent<AssistingOfficer>(); team[i].Configure(squad, i, camera, light, animator);
            }
            squad.Configure(player, mission, mission.GetComponent<DecisionScore>(), team, coverPositions, navigation);
            player.ConfigureSquad(squad);
            mission.GetComponent<MissionHud>().ConfigureSquad(squad);
            var obstacle = player.GetComponent<NavMeshObstacle>();
            if (!obstacle) obstacle = Undo.AddComponent<NavMeshObstacle>(player.gameObject);
            obstacle.shape = NavMeshObstacleShape.Capsule; obstacle.center = Vector3.up * .9f;
            obstacle.radius = .35f; obstacle.height = 1.8f; obstacle.carving = false;
            EditorUtility.SetDirty(player); EditorUtility.SetDirty(squad); EditorUtility.SetDirty(mission.GetComponent<MissionHud>());
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene()); AssetDatabase.SaveAssets();
            SingleAssistantInstaller.Install();
            Debug.Log("CLEARANCE: One animated police assistant, entry choice, following and corner-search orders installed.");
        }
    }
}
