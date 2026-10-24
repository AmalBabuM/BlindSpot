using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Clearance.Editor
{
    [InitializeOnLoad]
    public static class PrisonerAnimationInstaller
    {
        const string Folder = "Assets/Character/Prisoner/source/";
        const string Request = "Library/ClearanceAnimations.request";
        public const string ControllerPath = "Assets/Clearance/PrisonerAnimations.controller";
        static PrisonerAnimationInstaller() => EditorApplication.delayCall += Process;
        static void Process()
        {
            if (!File.Exists(Request) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            string request = File.ReadAllText(Request).Trim();
            if (request != "pending" && request != "test") return;
            if (SceneManager.GetActiveScene().name != "GroundFloor") return;
            File.WriteAllText(Request, "installing");
            try { if (request == "pending") Install(); File.WriteAllText(Request, "installed"); GroundFloorSmokeTest.Start(); }
            catch (System.Exception e) { File.WriteAllText(Request, "failed: " + e); Debug.LogException(e); }
        }
        [MenuItem("Clearance/Install Mixamo Prisoner Animations")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var mission = Object.FindFirstObjectByType<GroundFloorMission>();
            if (!mission) throw new System.InvalidOperationException("Open the configured ground-floor scene.");
            var importer = (ModelImporter)AssetImporter.GetAtPath(Folder + "Prison.fbx");
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.SaveAndReimport();
            var avatar = AssetDatabase.LoadAllAssetsAtPath(Folder + "Prison.fbx").OfType<Avatar>().FirstOrDefault(a => a.isValid && a.isHuman);
            if (!avatar) throw new System.InvalidOperationException("Prisoner humanoid avatar could not be created.");
            AnimationClip Clip(string file, string name, bool loop)
            {
                string path = Folder + "Prison@" + file + ".fbx";
                var modelImporter = (ModelImporter)AssetImporter.GetAtPath(path);
                if (modelImporter == null) throw new System.InvalidOperationException("Missing animation: " + path);
                modelImporter.animationType = ModelImporterAnimationType.Human;
                modelImporter.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                modelImporter.sourceAvatar = avatar;
                modelImporter.importAnimation = true;
                var clips = modelImporter.defaultClipAnimations;
                if (clips.Length == 0) throw new System.InvalidOperationException("No animation takes found: " + path);
                foreach (var clip in clips)
                {
                    clip.name = name; clip.loopTime = loop; clip.loopPose = loop;
                    clip.lockRootRotation = true; clip.lockRootHeightY = true; clip.lockRootPositionXZ = true;
                    clip.keepOriginalOrientation = true; clip.keepOriginalPositionXZ = true;
                    clip.heightFromFeet = true;
                }
                modelImporter.clipAnimations = clips;
                modelImporter.SaveAndReimport();
                return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview"));
            }
            var idle = Clip("Breathing Idle", "Breathing Idle", true);
            var walk = Clip("Walking", "Walking", true);
            var kneel = Clip("Kneel", "Kneel", false);
            var fall = Clip("Falling Back Death", "Falling Back Death", false);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (!controller)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
                controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
                controller.AddParameter("Surrender", AnimatorControllerParameterType.Bool);
                controller.AddParameter("Fallen", AnimatorControllerParameterType.Bool);
                var machine = controller.layers[0].stateMachine;
                var idleState = machine.AddState("Idle"); idleState.motion = idle;
                var walkState = machine.AddState("Walking"); walkState.motion = walk;
                var kneelState = machine.AddState("Kneel"); kneelState.motion = kneel;
                var fallState = machine.AddState("Fall"); fallState.motion = fall;
                machine.defaultState = idleState;
                var toWalk = idleState.AddTransition(walkState); toWalk.hasExitTime = false; toWalk.duration = .15f;
                toWalk.AddCondition(AnimatorConditionMode.Greater, .05f, "Speed");
                var toIdle = walkState.AddTransition(idleState); toIdle.hasExitTime = false; toIdle.duration = .15f;
                toIdle.AddCondition(AnimatorConditionMode.Less, .05f, "Speed");
                var toKneel = machine.AddAnyStateTransition(kneelState); toKneel.hasExitTime = false;
                toKneel.canTransitionToSelf = false; toKneel.duration = .15f;
                toKneel.AddCondition(AnimatorConditionMode.If, 0, "Surrender");
                toKneel.AddCondition(AnimatorConditionMode.IfNot, 0, "Fallen");
                var toFall = machine.AddAnyStateTransition(fallState); toFall.hasExitTime = false;
                toFall.canTransitionToSelf = false; toFall.duration = .05f;
                toFall.AddCondition(AnimatorConditionMode.If, 0, "Fallen");
            }
            var presentation = mission.Suspect.GetComponentInChildren<PrisonerPresentation>();
            // Some Mixamo kneeling takes stand up again. End on the last stable low pose.
            var preview = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "Prison.fbx"));
            preview.hideFlags = HideFlags.HideAndDontSave;
            preview.transform.position = Vector3.zero;
            preview.transform.localScale = presentation.transform.lossyScale;
            try
            {
                var previewAnimator = preview.GetComponent<Animator>();
                previewAnimator.avatar = avatar; previewAnimator.runtimeAnimatorController = controller;
                previewAnimator.applyRootMotion = false; previewAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                previewAnimator.Rebind(); previewAnimator.Update(0);
                var hips = previewAnimator.GetBoneTransform(HumanBodyBones.Hips);
                var head = previewAnimator.GetBoneTransform(HumanBodyBones.Head);
                var heights = new float[101]; var heads = new float[101];
                for (int i = 0; i <= 100; i++)
                {
                    previewAnimator.Play("Kneel", 0, i / 100f * .999f); previewAnimator.Update(0);
                    heights[i] = hips.position.y; heads[i] = head.position.y;
                }
                if (heads[100] > heads[0] * .85f)
                {
                    float lowest = heights.Min();
                    int hold = -1;
                    for (int i = 1; i < 100; i++)
                        if (heights[i] <= lowest + .04f && heads[i] < heads[0] * .85f) hold = i;
                    if (hold < 0) throw new System.InvalidOperationException("Could not find a stable kneeling pose in the supplied clip.");
                    var kneelImporter = (ModelImporter)AssetImporter.GetAtPath(Folder + "Prison@Kneel.fbx");
                    var settings = kneelImporter.clipAnimations;
                    settings[0].lastFrame = Mathf.Floor(settings[0].firstFrame + (settings[0].lastFrame - settings[0].firstFrame) * hold / 100f);
                    kneelImporter.clipAnimations = settings; kneelImporter.SaveAndReimport();
                    kneel = AssetDatabase.LoadAllAssetsAtPath(Folder + "Prison@Kneel.fbx").OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview"));
                }
            }
            finally { Object.DestroyImmediate(preview); }
            var animator = presentation.GetComponent<Animator>();
            if (!animator) animator = presentation.gameObject.AddComponent<Animator>();
            Undo.RecordObject(animator, "Connect Mixamo animations");
            animator.avatar = avatar; animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; animator.enabled = true;
            presentation.ConfigureAnimations(mission.Suspect, animator);
            Undo.RecordObject(mission.Suspect, "Use animated surrender and falling");
            mission.Suspect.ConfigureAnimatedResponse(kneel.length);
            mission.Suspect.ConfigureUnarmedEncounter();
            EditorUtility.SetDirty(presentation); EditorUtility.SetDirty(mission.Suspect); EditorUtility.SetDirty(animator);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/Prisoner-animations.txt", $"Humanoid avatar valid. Idle {idle.length:F2}s; Walk {walk.length:F2}s; Kneel {kneel.length:F2}s; Fall {fall.length:F2}s. Root motion disabled; final kneel/fall frames held.");
            Debug.Log("CLEARANCE: Mixamo idle/walk/kneel/fall animations installed.");
        }
    }
}
