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
    public static class OfficeInstaller
    {
        const string Request = "Library/ClearanceOffice.request";
        static OfficeInstaller()
        {
            EditorApplication.delayCall += Process;
            EditorApplication.playModeStateChanged += state => { if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += Process; };
        }
        static void Process()
        {
            if (!File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().name != "GroundFloor") return;
            string request = File.ReadAllText(Request).Trim();
            if (request != "pending" && request != "test") return;
            File.WriteAllText(Request, "installing");
            try { if (request == "pending") Install(); File.WriteAllText(Request, "installed"); OfficeSmokeTest.Start(); }
            catch (System.Exception e) { File.WriteAllText(Request, "failed: " + e); Debug.LogException(e); }
        }
        [MenuItem("Clearance/Set Up Office Encounter")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var mission = Object.FindFirstObjectByType<GroundFloorMission>();
            var squad = Object.FindFirstObjectByType<SquadCoordinator>();
            var employee = GameObject.Find("Employee");
            if (!mission || !squad || !employee) throw new System.InvalidOperationException("Office setup needs the configured mission, assistant and Employee object.");
            if (mission.Office)
            {
                mission.Office.CustodyPoint.position = new Vector3(-33.5f, .05f, 4);
                var oldGround = GameObject.Find("Office_OutsideCustodyGround"); if (oldGround) Undo.DestroyObjectImmediate(oldGround);
                InstallPushDoors(); RebuildNavigation(); ConfigureLighting();
                ConvertMaterials(employee); ConvertMaterials(GameObject.Find("Front Desk"));
                EditorSceneManager.SaveScene(SceneManager.GetActiveScene()); AssetDatabase.SaveAssets(); return;
            }
            var office = Undo.AddComponent<OfficeEncounter>(mission.gameObject);
            var assistant = squad.Officers[0];
            assistant.transform.position = squad.Leader.transform.position - squad.Leader.transform.forward * .8f - squad.Leader.transform.right * .8f;
            var escort = Undo.AddComponent<OfficeEscort>(assistant.gameObject);
            var suspect = mission.Suspect;
            var patrol = suspect.GetComponent<SuspectPatrol>(); if (patrol) patrol.enabled = false;
            var suspectAgent = suspect.GetComponent<NavMeshAgent>();
            if (!suspectAgent) suspectAgent = Undo.AddComponent<NavMeshAgent>(suspect.gameObject);
            suspectAgent.enabled = false; suspectAgent.radius = .27f; suspectAgent.height = 1.8f;
            suspectAgent.speed = 1.5f; suspectAgent.acceleration = 4; suspectAgent.angularSpeed = 180; suspectAgent.stoppingDistance = .2f;
            var actorSource = PrefabUtility.GetCorrespondingObjectFromSource(employee);
            string employeePath = AssetDatabase.GetAssetPath(actorSource);
            var importer = (ModelImporter)AssetImporter.GetAtPath(employeePath);
            var description = importer.humanDescription;
            HumanBone Bone(string human, string source) => new HumanBone { humanName = human, boneName = source, limit = new HumanLimit { useDefaultValues = true } };
            description.human = new[] {
                Bone("Hips", "spine"), Bone("Spine", "spine.001"), Bone("Chest", "spine.002"), Bone("UpperChest", "spine.003"),
                Bone("Neck", "spine.004"), Bone("Head", "spine.006"),
                Bone("LeftShoulder", "shoulder.L"), Bone("LeftUpperArm", "upper_arm.L"), Bone("LeftLowerArm", "forearm.L"), Bone("LeftHand", "hand.L"),
                Bone("RightShoulder", "shoulder.R"), Bone("RightUpperArm", "upper_arm.R"), Bone("RightLowerArm", "forearm.R"), Bone("RightHand", "hand.R"),
                Bone("LeftUpperLeg", "thigh.L"), Bone("LeftLowerLeg", "shin.L"), Bone("LeftFoot", "foot.L"), Bone("LeftToes", "toe.L"),
                Bone("RightUpperLeg", "thigh.R"), Bone("RightLowerLeg", "shin.R"), Bone("RightFoot", "foot.R"), Bone("RightToes", "toe.R") };
            importer.humanDescription = description; importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel; importer.SaveAndReimport();
            var avatar = AssetDatabase.LoadAllAssetsAtPath(employeePath).OfType<Avatar>().FirstOrDefault(a => a.isHuman && a.isValid);
            if (!avatar) throw new System.InvalidOperationException("Admin humanoid rig mapping failed.");
            var adminAnimator = employee.GetComponent<Animator>();
            if (!adminAnimator) adminAnimator = employee.AddComponent<Animator>();
            adminAnimator.avatar = avatar;
            adminAnimator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(PrisonerAnimationInstaller.ControllerPath);
            adminAnimator.applyRootMotion = false; adminAnimator.enabled = true; adminAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var adminCollider = employee.GetComponent<CapsuleCollider>();
            if (!adminCollider) adminCollider = Undo.AddComponent<CapsuleCollider>(employee);
            adminCollider.height = 1.8f; adminCollider.radius = .3f; adminCollider.center = Vector3.up * .9f;
            var admin = Undo.AddComponent<OfficePerson>(employee);
            admin.Configure(office, true, adminAnimator, "Office administrator identity verified - civilian protected");
            var suspectPerson = Undo.AddComponent<OfficePerson>(suspect.gameObject);
            suspectPerson.Configure(office, false, suspect.GetComponentInChildren<Animator>(), "Person matches the suspect briefing; no visible weapon found");
            Transform Point(string name, Vector3 position)
            {
                var point = new GameObject(name); Undo.RegisterCreatedObjectUndo(point, "Create office waypoint");
                point.transform.SetParent(mission.transform); point.transform.position = position; return point.transform;
            }
            var custody = Point("DoorCustodyPoint", new Vector3(-33.5f, .05f, 4));
            var stairsObject = GameObject.Find("double stairs");
            var nextFloor = Point("Stairs_NextFloor", stairsObject ? stairsObject.transform.position + new Vector3(-.5f, .05f, 1.5f) : new Vector3(-17, .05f, 4));
            office.Configure(squad.Leader, suspect, mission.GetComponent<DecisionScore>(), new[] { admin, suspectPerson }, escort, custody, nextFloor);
            escort.Configure(office, suspectAgent); assistant.ConfigureOffice(office); mission.ConfigureOffice(office);
            suspect.GetComponentInChildren<PrisonerPresentation>().ConfigureOffice(office);
            var controller = (AnimatorController)suspect.GetComponentInChildren<Animator>().runtimeAnimatorController;
            if (!controller.parameters.Any(p => p.name == "Escorted"))
            {
                controller.AddParameter("Escorted", AnimatorControllerParameterType.Bool);
                var machine = controller.layers[0].stateMachine;
                var walking = machine.states.First(s => s.state.name == "Walking").state;
                var idle = machine.states.First(s => s.state.name == "Idle").state;
                var walk = machine.AddAnyStateTransition(walking); walk.hasExitTime = false; walk.duration = .2f; walk.canTransitionToSelf = false;
                walk.AddCondition(AnimatorConditionMode.If, 0, "Escorted"); walk.AddCondition(AnimatorConditionMode.Greater, .05f, "Speed");
                var stop = machine.AddAnyStateTransition(idle); stop.hasExitTime = false; stop.duration = .2f; stop.canTransitionToSelf = false;
                stop.AddCondition(AnimatorConditionMode.If, 0, "Escorted"); stop.AddCondition(AnimatorConditionMode.Less, .05f, "Speed");
                EditorUtility.SetDirty(controller);
            }
            InstallPushDoors();
            foreach (var filter in GameObject.Find("Front Desk").GetComponentsInChildren<MeshFilter>())
                if (!filter.GetComponent<Collider>()) { var collider = filter.gameObject.AddComponent<MeshCollider>(); collider.sharedMesh = filter.sharedMesh; }
            RebuildNavigation(); ConfigureLighting();
            ConvertMaterials(employee); ConvertMaterials(GameObject.Find("Front Desk"));
            EditorUtility.SetDirty(office); EditorUtility.SetDirty(escort); EditorUtility.SetDirty(mission); EditorUtility.SetDirty(assistant);
            EditorUtility.SetDirty(suspect.GetComponentInChildren<PrisonerPresentation>());
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene()); EditorSceneManager.SaveScene(SceneManager.GetActiveScene()); AssetDatabase.SaveAssets();
            Debug.Log("CLEARANCE: Office identification, civilian protection, custody escort, assistant return and push-door stair route installed.");
        }
        static void InstallPushDoors()
        {
            var root = GameObject.Find("Open Door");
            if (!root) return;
            foreach (Transform leaf in root.transform)
            {
                if (!leaf.GetComponentInChildren<Collider>()) continue;
                foreach (var scripted in leaf.GetComponentsInChildren<InteractableDoor>(true)) scripted.enabled = false;
                foreach (var t in leaf.GetComponentsInChildren<Transform>()) GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
                var body = leaf.GetComponent<Rigidbody>(); if (!body) body = Undo.AddComponent<Rigidbody>(leaf.gameObject);
                body.mass = 15; body.useGravity = false; body.isKinematic = true; body.angularDamping = 4;
                body.maxAngularVelocity = 2; body.interpolation = RigidbodyInterpolation.Interpolate; body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                var hinge = leaf.GetComponent<HingeJoint>(); if (hinge) Undo.DestroyObjectImmediate(hinge);
                if (!leaf.GetComponent<PushDoor>()) Undo.AddComponent<PushDoor>(leaf.gameObject);
            }
        }
        [MenuItem("Clearance/Rebuild Office Navigation")]
        public static void RebuildNavigation()
        {
            var squad = Object.FindFirstObjectByType<SquadCoordinator>();
            var sources = new List<NavMeshBuildSource>(); var marks = new List<NavMeshBuildMarkup>();
            foreach (var actor in Object.FindObjectsByType<OfficePerson>(FindObjectsSortMode.None)) marks.Add(new NavMeshBuildMarkup { root = actor.transform, ignoreFromBuild = true });
            foreach (var actor in Object.FindObjectsByType<FirstPersonOfficer>(FindObjectsSortMode.None)) marks.Add(new NavMeshBuildMarkup { root = actor.transform, ignoreFromBuild = true });
            foreach (var actor in Object.FindObjectsByType<AssistingOfficer>(FindObjectsSortMode.None)) marks.Add(new NavMeshBuildMarkup { root = actor.transform, ignoreFromBuild = true });
            foreach (var door in Object.FindObjectsByType<InteractableDoor>(FindObjectsSortMode.None)) marks.Add(new NavMeshBuildMarkup { root = door.transform, ignoreFromBuild = true });
            foreach (var door in Object.FindObjectsByType<PushDoor>(FindObjectsSortMode.None)) marks.Add(new NavMeshBuildMarkup { root = door.transform, ignoreFromBuild = true });
            NavMeshBuilder.CollectSources((Transform)null, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, marks, sources);
            var settings = NavMesh.GetSettingsByIndex(0); settings.agentRadius = .27f; settings.agentHeight = 1.8f; settings.agentClimb = .3f;
            settings.overrideVoxelSize = true; settings.voxelSize = .08f;
            var bounds = new Bounds(new Vector3(-15, 3, 3), new Vector3(65, 16, 40));
            string path = "Assets/Clearance/OfficeNavigation.asset";
            var data = AssetDatabase.LoadAssetAtPath<NavMeshData>(path);
            if (data) NavMeshBuilder.UpdateNavMeshData(data, settings, sources, bounds);
            else { data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity); AssetDatabase.CreateAsset(data, path); }
            squad.ConfigureNavigation(data); EditorUtility.SetDirty(squad);
        }
        [MenuItem("Clearance/Configure Office Baked Lighting")]
        public static void ConfigureLighting()
        {
            if (AssetDatabase.LoadMainAssetAtPath("Assets/Clearance/Lighting/OfficeLightingPipeline.asset"))
            { Debug.Log("Office lighting has been repaired. Use Clearance > Repair And Bake Office Lighting to update its bake safely."); return; }
            const string settingsPath = "Assets/Clearance/OfficeLighting.lighting";
            var settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(settingsPath);
            if (!settings)
            {
                settings = new LightingSettings { name = "OfficeLighting", bakedGI = true, realtimeGI = false,
                    lightmapper = LightingSettings.Lightmapper.ProgressiveCPU, lightmapResolution = 16, lightmapPadding = 4 };
                AssetDatabase.CreateAsset(settings, settingsPath);
            }
            Lightmapping.lightingSettings = settings;
            LightmapSettings.lightmapsMode = LightmapsMode.CombinedDirectional;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.14f, .18f, .23f);
            RenderSettings.ambientEquatorColor = new Color(.09f, .105f, .13f);
            RenderSettings.ambientGroundColor = new Color(.045f, .05f, .06f);
            var uvSources = new HashSet<string>();
            foreach (var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                if (renderer.GetComponentInParent<FirstPersonOfficer>() || renderer.GetComponentInParent<AssistingOfficer>() ||
                    renderer.GetComponentInParent<OfficePerson>() || renderer.GetComponentInParent<PushDoor>() ||
                    renderer.GetComponentInParent<InteractableDoor>() || renderer.GetComponentInParent<UnsafeElevator>() || renderer.GetComponent<TextMesh>()) continue;
                GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, GameObjectUtility.GetStaticEditorFlags(renderer.gameObject) | StaticEditorFlags.ContributeGI);
                renderer.receiveGI = ReceiveGI.Lightmaps;
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter && filter.sharedMesh)
                {
                    string source = AssetDatabase.GetAssetPath(filter.sharedMesh);
                    if (!string.IsNullOrEmpty(source) && source.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)) uvSources.Add(source);
                }
            }
            var level = GameObject.Find("_Level");
            foreach (var light in level.GetComponentsInChildren<Light>()) { light.lightmapBakeType = LightmapBakeType.Mixed; light.shadows = LightShadows.None; }
            for (int i = 0; i < 2; i++)
            {
                string name = "Office_FillLight_" + i; var go = GameObject.Find(name);
                if (!go) go = new GameObject(name, typeof(Light));
                go.transform.position = new Vector3(i == 0 ? -26 : -32, 2.8f, 4);
                var light = go.GetComponent<Light>(); light.type = LightType.Point; light.range = 11;
                light.intensity = i == 0 ? 1.5f : .8f; light.color = i == 0 ? new Color(.88f, .94f, 1) : new Color(1, .93f, .83f);
                light.lightmapBakeType = LightmapBakeType.Mixed; light.shadows = LightShadows.Soft;
            }
            foreach (string source in uvSources)
            {
                var importer = AssetImporter.GetAtPath(source) as ModelImporter;
                if (importer != null && !importer.generateSecondaryUV) { importer.generateSecondaryUV = true; importer.SaveAndReimport(); }
            }
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }
        static void ConvertMaterials(GameObject root)
        {
            if (!root) return;
            if (!AssetDatabase.IsValidFolder("Assets/Clearance/OfficeMaterials")) AssetDatabase.CreateFolder("Assets/Clearance", "OfficeMaterials");
            foreach (var renderer in root.GetComponentsInChildren<Renderer>())
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    var old = materials[i];
                    if (!old || (old.shader.name.StartsWith("Universal Render Pipeline/") && root.name != "Front Desk")) continue;
                    string id = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(old));
                    string name = System.Text.RegularExpressions.Regex.Replace(old.name, "[^a-zA-Z0-9_]", "_");
                    string path = "Assets/Clearance/OfficeMaterials/" + name + "_" + id + ".mat";
                    var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (!material)
                    {
                        var albedo = old.HasProperty("_BaseMap") ? old.GetTexture("_BaseMap") : old.HasProperty("_MainTex") ? old.GetTexture("_MainTex") : null;
                        var color = old.HasProperty("_BaseColor") ? old.GetColor("_BaseColor") : old.HasProperty("_Color") ? old.GetColor("_Color") : Color.white;
                        if (!albedo && color.maxColorComponent < .03f) color = new Color(.4f, .42f, .45f);
                        material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                        material.SetColor("_BaseColor", color); material.SetTexture("_BaseMap", albedo);
                        if (old.HasProperty("_BumpMap") && old.GetTexture("_BumpMap"))
                        { material.SetTexture("_BumpMap", old.GetTexture("_BumpMap")); material.EnableKeyword("_NORMALMAP"); }
                        material.SetFloat("_Smoothness", .25f); AssetDatabase.CreateAsset(material, path);
                    }
                    materials[i] = material;
                }
                renderer.sharedMaterials = materials;
            }
        }
        [MenuItem("Clearance/Bake Office Lighting")]
        public static void BakeLighting()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("Exit Play Mode before baking lighting."); return; }
            OfficeLightingRepair.RepairAndBake();
        }
    }
}
