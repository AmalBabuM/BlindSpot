using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Clearance.Editor
{
    public static class GroundFloorSetup
    {
        const string RootName = "_GroundFloorGameplay";
        [MenuItem("Clearance/Set Up Ground Floor %#g")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (SceneManager.GetActiveScene().name != "GroundFloor")
            { Debug.LogError("Open Assets/Scenes/GroundFloor.unity before setup."); return; }
            FixMaterials();
            if (GameObject.Find(RootName)) { PrisonerInstaller.Replace(); PatrolInstaller.Install(); Debug.Log("Ground-floor gameplay is already set up."); return; }
            var oldCamera = Object.FindFirstObjectByType<Camera>();
            if (oldCamera) { Undo.RecordObject(oldCamera.gameObject, "Disable legacy camera"); oldCamera.gameObject.SetActive(false); }
            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Set up ground floor");
            var mission = root.AddComponent<GroundFloorMission>();
            var score = root.AddComponent<DecisionScore>();
            var rules = AssetDatabase.LoadAssetAtPath<ScoringRules>("Assets/Clearance/GroundFloorScoring.asset");
            if (!rules)
            {
                rules = ScriptableObject.CreateInstance<ScoringRules>();
                AssetDatabase.CreateAsset(rules, "Assets/Clearance/GroundFloorScoring.asset");
            }
            score.Configure(rules);
            var player = new GameObject("Player_Officer");
            player.transform.SetParent(root.transform);
            player.transform.position = new Vector3(-12, .06f, 2.3f);
            player.transform.rotation = Quaternion.Euler(0, 180, 0);
            var controller = player.AddComponent<CharacterController>();
            controller.height = 1.8f; controller.radius = .28f; controller.center = Vector3.up * .9f;
            controller.stepOffset = .25f; controller.skinWidth = .035f;
            var officer = player.AddComponent<FirstPersonOfficer>();
            var viewObject = new GameObject("OfficerCamera", typeof(Camera), typeof(AudioListener));
            viewObject.tag = "MainCamera";
            viewObject.transform.SetParent(player.transform, false);
            viewObject.transform.localPosition = Vector3.up * 1.65f;
            var view = viewObject.GetComponent<Camera>();
            view.nearClipPlane = .06f; view.farClipPlane = 100; view.fieldOfView = 72;
            view.clearFlags = CameraClearFlags.SolidColor; view.backgroundColor = new Color(.025f, .035f, .05f);
            officer.Configure(view, mission);
            var lightObject = new GameObject("HandheldFlashlight", typeof(Light));
            lightObject.transform.SetParent(viewObject.transform, false);
            lightObject.transform.localPosition = new Vector3(.15f, -.1f, .15f);
            var flashlight = lightObject.GetComponent<Light>();
            flashlight.type = LightType.Spot; flashlight.range = 16; flashlight.spotAngle = 65;
            flashlight.innerSpotAngle = 30; flashlight.intensity = 5; flashlight.shadows = LightShadows.Soft;
            var actions = player.AddComponent<OfficerActions>();
            actions.Configure(officer, mission, score, flashlight);

            // Reuse the officer the user placed, preserving its prefab connection and materials.
            var model = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name.StartsWith("police_officer"));
            if (!model)
                model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Character/Police_officer/Prefab/police_officer.prefab"));
            Undo.SetTransformParent(model.transform, player.transform, "Attach officer model");
            Undo.RecordObject(model.transform, "Position officer model");
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;
            foreach (var renderer in model.GetComponentsInChildren<Renderer>()) renderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            foreach (var collider in model.GetComponentsInChildren<Collider>()) collider.enabled = false;
            foreach (var animator in model.GetComponentsInChildren<Animator>()) animator.applyRootMotion = false;

            var suspectObject = new GameObject("Suspect_TrainingPlaceholder");
            suspectObject.transform.SetParent(root.transform);
            suspectObject.transform.position = new Vector3(-12, .05f, -4.5f);
            var suspect = suspectObject.AddComponent<SuspectController>();
            var clothing = Material("SuspectClothing", new Color(.55f, .25f, .12f));
            var skin = Material("SuspectSkin", new Color(.64f, .47f, .34f));
            var dark = Material("Equipment", new Color(.07f, .09f, .11f));
            Primitive("Torso", suspectObject.transform, PrimitiveType.Capsule, new Vector3(0, 1.05f, 0), new Vector3(.55f, .42f, .35f), clothing);
            Primitive("Head", suspectObject.transform, PrimitiveType.Sphere, new Vector3(0, 1.73f, 0), Vector3.one * .3f, skin);
            Primitive("LeftLeg", suspectObject.transform, PrimitiveType.Cube, new Vector3(-.16f, .38f, 0), new Vector3(.2f, .75f, .24f), dark);
            Primitive("RightLeg", suspectObject.transform, PrimitiveType.Cube, new Vector3(.16f, .38f, 0), new Vector3(.2f, .75f, .24f), dark);
            var left = new GameObject("LeftShoulder").transform;
            left.SetParent(suspectObject.transform, false); left.localPosition = new Vector3(-.35f, 1.4f, 0);
            var right = new GameObject("RightShoulder").transform;
            right.SetParent(suspectObject.transform, false); right.localPosition = new Vector3(.35f, 1.4f, 0);
            Primitive("LeftArm", left, PrimitiveType.Cube, Vector3.down * .25f, new Vector3(.16f, .6f, .16f), clothing);
            Primitive("RightArm", right, PrimitiveType.Cube, Vector3.down * .25f, new Vector3(.16f, .6f, .16f), clothing);
            var weapon = Primitive("VisibleWeapon", right, PrimitiveType.Cube, new Vector3(0, -.55f, .15f), new Vector3(.1f, .12f, .36f), dark);
            suspect.Configure(score, left, right, weapon);
            var corners = new ObservationPoint[2];
            for (int i = 0; i < 2; i++)
            {
                var point = new GameObject(i == 0 ? "Observe_LeftCorner" : "Observe_RightCorner");
                point.transform.SetParent(root.transform);
                point.transform.position = new Vector3(i == 0 ? -14.1f : -9.9f, 1.65f, -1.7f);
                corners[i] = point.AddComponent<ObservationPoint>();
                corners[i].Configure(i == 0 ? "Left corner" : "Right corner");
            }
            mission.Configure(officer, actions, score, suspect, corners,
                new Bounds(new Vector3(-12, 1.5f, -4.65f), new Vector3(5.7f, 3, 8.7f)));
            root.AddComponent<MissionHud>().Configure(mission, score, officer, actions);
            // Add interactions to existing movable door prefabs, without changing their source assets.
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if (t.name.StartsWith("Door_Left_01") && !t.GetComponent<InteractableDoor>()) t.gameObject.AddComponent<InteractableDoor>();
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != "Assets/Scenes/GroundFloor.unity").ToList();
            scenes.Insert(0, new EditorBuildSettingsScene("Assets/Scenes/GroundFloor.unity", true));
            EditorBuildSettings.scenes = scenes.ToArray();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = player;
            if (AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Character/Prisoner/source/Prison.fbx")) PrisonerInstaller.Replace();
            PatrolInstaller.Install();
            Debug.Log("CLEARANCE: Ground floor ready. Press Play. See GROUND_FLOOR.md for controls and architecture.");
        }

        [MenuItem("Clearance/Fix Imported Materials for URP")]
        public static void FixMaterials()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) throw new System.InvalidOperationException("URP Lit shader is unavailable.");
            int count = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/Character/Police_officer", "Assets/Barking_Dog" }))
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (!mat || (mat.shader && mat.shader.name.StartsWith("Universal Render Pipeline/"))) continue;
                Undo.RecordObject(mat, "Convert imported material to URP");
                var albedo = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
                var tint = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
                var bump = mat.HasProperty("_BumpMap") ? mat.GetTexture("_BumpMap") : null;
                var metal = mat.HasProperty("_MetallicGlossMap") ? mat.GetTexture("_MetallicGlossMap") : null;
                var emission = mat.HasProperty("_EmissionColor") ? mat.GetColor("_EmissionColor") : Color.black;
                var emissionMap = mat.HasProperty("_EmissionMap") ? mat.GetTexture("_EmissionMap") : null;
                var scale = albedo ? mat.GetTextureScale("_MainTex") : Vector2.one;
                var offset = albedo ? mat.GetTextureOffset("_MainTex") : Vector2.zero;
                float smoothness = mat.HasProperty("_Glossiness") ? mat.GetFloat("_Glossiness") : .2f;
                mat.shader = shader;
                mat.shaderKeywords = new string[0];
                mat.SetTexture("_BaseMap", albedo); mat.SetColor("_BaseColor", tint);
                mat.SetTextureScale("_BaseMap", scale); mat.SetTextureOffset("_BaseMap", offset);
                mat.SetFloat("_Smoothness", smoothness); mat.SetTexture("_BumpMap", bump);
                mat.SetTexture("_MetallicGlossMap", metal);
                if (bump) mat.EnableKeyword("_NORMALMAP");
                if (metal) mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                mat.SetColor("_EmissionColor", emission); mat.SetTexture("_EmissionMap", emissionMap);
                if (emission.maxColorComponent > 0) mat.EnableKeyword("_EMISSION");
                mat.SetFloat("_Cull", 0); // The supplied character uses a two-sided Standard shader.
                if (mat.name.ToLowerInvariant().Contains("hair"))
                { mat.SetFloat("_AlphaClip", 1); mat.SetFloat("_Cutoff", .4f); mat.EnableKeyword("_ALPHATEST_ON"); }
                EditorUtility.SetDirty(mat); count++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"CLEARANCE: Converted {count} imported materials to URP; existing URP materials preserved.");
        }

        static Material Material(string name, Color color)
        {
            string path = "Assets/Clearance/" + name + ".mat";
            var result = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (result) return result;
            result = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            result.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(result, path);
            return result;
        }
        static GameObject Primitive(string name, Transform parent, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            var obj = GameObject.CreatePrimitive(type); obj.name = name;
            obj.transform.SetParent(parent, false); obj.transform.localPosition = position; obj.transform.localScale = scale;
            obj.GetComponent<Renderer>().sharedMaterial = material;
            return obj;
        }
    }
}
