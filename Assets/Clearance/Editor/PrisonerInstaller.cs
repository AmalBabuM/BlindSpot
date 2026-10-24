using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Clearance.Editor
{
    [InitializeOnLoad]
    public static class PrisonerInstaller
    {
        const string Request = "Library/ClearancePrisoner.request";
        static PrisonerInstaller() => EditorApplication.delayCall += ProcessRequest;
        static void ProcessRequest()
        {
            if (!File.Exists(Request) || File.ReadAllText(Request).Trim() != "pending") return;
            if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().name != "GroundFloor") return;
            try
            {
                Replace();
                File.WriteAllText(Request, "installed");
                GroundFloorSmokeTest.Start();
            }
            catch (System.Exception e) { File.WriteAllText(Request, "failed: " + e); Debug.LogException(e); }
        }

        [MenuItem("Clearance/Use Prisoner as Ground Floor Suspect")]
        public static void Replace()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var mission = Object.FindFirstObjectByType<GroundFloorMission>();
            if (!mission || SceneManager.GetActiveScene().name != "GroundFloor")
                throw new System.InvalidOperationException("Open the configured GroundFloor scene first.");
            var suspect = mission.Suspect;
            if (suspect.GetComponentInChildren<PrisonerPresentation>(true)) return;
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Character/Prisoner/source/Prison.fbx");
            if (!source) throw new System.InvalidOperationException("Prisoner model was not imported.");
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(source, suspect.transform);
            Undo.RegisterCreatedObjectUndo(visual, "Use prisoner suspect");
            visual.name = "PrisonerVisual";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            var renderers = visual.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) throw new System.InvalidOperationException("Prisoner model has no renderers.");
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            visual.transform.localScale *= 1.85f / Mathf.Max(.01f, bounds.size.y);
            bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            visual.transform.position += new Vector3(suspect.transform.position.x - bounds.center.x,
                suspect.transform.position.y - bounds.min.y, suspect.transform.position.z - bounds.center.z);

            const string materialPath = "Assets/Clearance/PrisonerURP.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (!material)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "PrisonerURP" };
                material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Character/Prisoner/textures/PowerPlantA.png"));
                material.SetColor("_BaseColor", Color.white);
                material.SetFloat("_Smoothness", .2f);
                AssetDatabase.CreateAsset(material, materialPath);
            }
            foreach (var renderer in renderers)
                renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => material).ToArray();
            foreach (var animator in visual.GetComponentsInChildren<Animator>()) animator.enabled = false;
            foreach (var collider in visual.GetComponentsInChildren<Collider>()) collider.enabled = false;
            foreach (var camera in visual.GetComponentsInChildren<Camera>()) camera.enabled = false;
            foreach (var light in visual.GetComponentsInChildren<Light>()) light.enabled = false;
            var bones = visual.GetComponentsInChildren<Transform>();
            Transform Bone(string suffix) => bones.FirstOrDefault(b => b.name.Split(':').Last() == suffix);
            var rightHand = Bone("RightHand");
            var leftHand = Bone("LeftHand");
            if (!rightHand || !leftHand || !Bone("LeftArm") || !Bone("RightArm") || !Bone("LeftForeArm") || !Bone("RightForeArm"))
                throw new System.InvalidOperationException("Expected Mixamo arm bones are missing.");
            // Keep the old placeholder objects inactive so this visual swap is easy to undo.
            foreach (Transform child in suspect.transform)
                if (child != visual.transform) { Undo.RecordObject(child.gameObject, "Hide placeholder"); child.gameObject.SetActive(false); }
            var capsule = suspect.GetComponent<CapsuleCollider>();
            if (!capsule) capsule = Undo.AddComponent<CapsuleCollider>(suspect.gameObject);
            capsule.height = 1.8f; capsule.radius = .32f; capsule.center = Vector3.up * .9f;
            var weapon = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(weapon, "Add suspect weapon visual");
            weapon.name = "HeldWeapon";
            weapon.transform.SetParent(rightHand, false);
            weapon.transform.position = rightHand.position + suspect.transform.forward * .1f;
            weapon.transform.rotation = suspect.transform.rotation;
            // Set world dimensions, independent of the imported rig's unit scale.
            weapon.transform.localScale = new Vector3(.08f / rightHand.lossyScale.x, .12f / rightHand.lossyScale.y, .28f / rightHand.lossyScale.z);
            var equipment = AssetDatabase.LoadAssetAtPath<Material>("Assets/Clearance/Equipment.mat");
            weapon.GetComponent<Renderer>().sharedMaterial = equipment;
            Object.DestroyImmediate(weapon.GetComponent<Collider>());
            Undo.RecordObject(suspect, "Bind prisoner presentation");
            suspect.Configure(Object.FindFirstObjectByType<DecisionScore>(), null, null, weapon);
            visual.AddComponent<PrisonerPresentation>().Configure(suspect, Bone("LeftArm"), Bone("LeftForeArm"), leftHand,
                Bone("RightArm"), Bone("RightForeArm"), rightHand);
            Undo.RecordObject(suspect.gameObject, "Name prisoner suspect");
            suspect.name = "Suspect_Prisoner";
            EditorUtility.SetDirty(suspect);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Debug.Log("CLEARANCE: Prisoner installed with URP material, hit collider and rigged surrender pose.");
        }
    }
}
