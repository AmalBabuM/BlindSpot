using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Clearance.Editor
{
    [InitializeOnLoad]
    public static class SingleAssistantInstaller
    {
        const string Folder = "Assets/Character/Police_officer/Base_mesh/";
        const string Request = "Library/ClearanceSingleAssistant.request";
        static SingleAssistantInstaller() => EditorApplication.delayCall += Process;
        static void Process()
        {
            if (!File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().name != "GroundFloor") return;
            string request = File.ReadAllText(Request).Trim();
            if (request != "pending" && request != "test") return;
            File.WriteAllText(Request, "installing");
            try { if (request == "pending") Install(); File.WriteAllText(Request, "installed"); SquadSmokeTest.Start(); }
            catch (System.Exception e) { File.WriteAllText(Request, "failed: " + e); Debug.LogException(e); }
        }
        [MenuItem("Clearance/Use One Assistant With Torch Animations")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var squad = Object.FindFirstObjectByType<SquadCoordinator>();
            if (!squad || squad.Officers.Length == 0) throw new System.InvalidOperationException("Set up an assisting officer first.");
            var officer = squad.Officers[0];
            var animator = officer.GetComponentInChildren<Animator>();
            var avatar = animator.avatar;
            AnimationClip Import(string file, string name)
            {
                string path = Folder + "police_officer@" + file + ".fbx";
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                if (!importer) throw new System.InvalidOperationException("Missing torch animation: " + path);
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                importer.sourceAvatar = avatar; importer.importAnimation = true;
                var clips = importer.defaultClipAnimations;
                foreach (var clip in clips)
                {
                    clip.name = name; clip.loopTime = true; clip.loopPose = true;
                    clip.lockRootRotation = true; clip.lockRootHeightY = true; clip.lockRootPositionXZ = true;
                    clip.keepOriginalOrientation = true; clip.keepOriginalPositionXZ = true; clip.heightFromFeet = true;
                }
                importer.clipAnimations = clips; importer.SaveAndReimport();
                return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview"));
            }
            var walk = Import("Standing Torch Walk Forward", "Torch Walk");
            var search = Import("Standing Torch Inspect Forward", "Torch Search");
            var controller = (AnimatorController)animator.runtimeAnimatorController;
            foreach (var state in controller.layers[0].stateMachine.states)
            {
                if (state.state.name == "Idle") state.state.motion = search;
                if (state.state.name == "Walking") state.state.motion = walk;
                state.state.speed = 1;
            }
            Undo.RecordObject(squad, "Use one assistant");
            foreach (var other in squad.Officers.Skip(1).ToArray()) Undo.DestroyObjectImmediate(other.gameObject);
            squad.UseSingleAssistant(officer);
            Undo.RecordObject(officer, "Use torch clips"); officer.UseTorchAnimations();
            var hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (!hand) throw new System.InvalidOperationException("Police avatar has no right hand.");
            var torch = officer.Flashlight;
            Undo.SetTransformParent(torch.transform, hand, "Attach assistant torch to hand");
            torch.transform.position = hand.position + officer.transform.forward * .06f;
            torch.transform.rotation = officer.transform.rotation;
            if (!torch.transform.Find("TorchBody"))
            {
                string materialPath = "Assets/Clearance/AssistantTorch.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (!material)
                {
                    material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    material.SetColor("_BaseColor", new Color(.04f, .05f, .065f));
                    material.SetFloat("_Metallic", .5f); AssetDatabase.CreateAsset(material, materialPath);
                }
                var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder); body.name = "TorchBody";
                body.transform.SetParent(torch.transform, false);
                body.transform.localRotation = Quaternion.Euler(90, 0, 0);
                body.transform.localPosition = Vector3.back * .06f;
                body.transform.localScale = new Vector3(.045f, .1f, .045f);
                Object.DestroyImmediate(body.GetComponent<Collider>());
                body.GetComponent<Renderer>().sharedMaterial = material;
            }
            var suspect = squad.Mission.Suspect;
            if (!suspect.GetComponent<NavMeshObstacle>())
            {
                var obstacle = Undo.AddComponent<NavMeshObstacle>(suspect.gameObject);
                obstacle.shape = NavMeshObstacleShape.Capsule; obstacle.center = Vector3.up * .9f;
                obstacle.radius = .34f; obstacle.height = 1.8f; obstacle.carving = false;
            }
            EditorUtility.SetDirty(controller); EditorUtility.SetDirty(officer); EditorUtility.SetDirty(squad);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene()); AssetDatabase.SaveAssets();
            Debug.Log("CLEARANCE: One assistant installed with torch walk/search clips, sequential corner checks and surrender support.");
        }
    }
}
