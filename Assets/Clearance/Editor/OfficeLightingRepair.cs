using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Clearance.Editor
{
    [InitializeOnLoad]
    public static class OfficeLightingRepair
    {
        const string Request = "Library/ClearanceLightingRepair.request";
        const string Folder = "Assets/Clearance/Lighting";
        static double nextProgress;
        static bool observingBake;
        static OfficeLightingRepair()
        {
            EditorApplication.delayCall += Process;
            Lightmapping.bakeCompleted += Completed;
            EditorApplication.update += Progress;
        }
        static void Process()
        {
            if (!File.Exists(Request) || File.ReadAllText(Request).Trim() != "pending") return;
            if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().name != "GroundFloor") return;
            File.WriteAllText(Request, "preparing");
            try { RepairAndBake(); }
            catch (Exception e) { File.WriteAllText(Request, "failed: " + e); Debug.LogException(e); }
        }
        [MenuItem("Clearance/Repair And Bake Office Lighting")]
        public static void RepairAndBake()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("Exit Play Mode before baking lighting."); return; }
            if (Lightmapping.isRunning) { Debug.LogWarning("A lighting bake is already running."); return; }
            var scene = SceneManager.GetActiveScene();
            if (scene.name != "GroundFloor") throw new InvalidOperationException("Open GroundFloor before this operation.");
            EditorSceneManager.SaveScene(scene);
            string backup = "LightingBackups/" + DateTime.Now.ToString("yyyy-MM-dd-HHmmss");
            Directory.CreateDirectory(backup);
            foreach (string file in new[] { scene.path, "ProjectSettings/GraphicsSettings.asset", "ProjectSettings/QualitySettings.asset", "Assets/Clearance/OfficeLighting.lighting" })
                if (File.Exists(file)) File.Copy(file, backup + "/" + Path.GetFileName(file), true);
            string previousBake = Path.Combine(Path.GetDirectoryName(scene.path), Path.GetFileNameWithoutExtension(scene.path));
            if (Directory.Exists(previousBake))
                foreach (string file in Directory.GetFiles(previousBake, "*", SearchOption.AllDirectories))
                {
                    string target = backup + "/BakedLighting/" + Path.GetRelativePath(previousBake, file);
                    Directory.CreateDirectory(Path.GetDirectoryName(target)); File.Copy(file, target, true);
                }
            Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/Office-lighting-backup.txt", Path.GetFullPath(backup));
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Clearance", "Lighting");
            ConfigurePipeline();
            var settings = AssetDatabase.LoadAssetAtPath<LightingSettings>("Assets/Clearance/OfficeLighting.lighting");
            settings.bakedGI = true; settings.realtimeGI = false;
            settings.lightmapper = LightingSettings.Lightmapper.ProgressiveCPU;
            settings.lightmapResolution = 16; settings.lightmapPadding = 4;
            var serializedSettings = new SerializedObject(settings);
            SetInt(serializedSettings, "m_PVRDirectSampleCount", 64);
            SetInt(serializedSettings, "m_PVRSampleCount", 128);
            SetInt(serializedSettings, "m_PVREnvironmentSampleCount", 64);
            SetInt(serializedSettings, "m_LightmapMaxSize", 1024);
            serializedSettings.ApplyModifiedPropertiesWithoutUndo();
            Lightmapping.lightingSettings = settings;
            LightmapSettings.lightmapsMode = LightmapsMode.CombinedDirectional;
            // Detach the imported Test_Map bake; its original lighting assets are kept untouched.
            Lightmapping.lightingDataAsset = null;
            LightmapSettings.lightmaps = Array.Empty<LightmapData>();
            foreach (var renderer in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)) renderer.lightmapIndex = -1;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.22f, .25f, .30f);
            RenderSettings.ambientEquatorColor = new Color(.15f, .17f, .20f);
            RenderSettings.ambientGroundColor = new Color(.075f, .085f, .10f);
            RenderSettings.reflectionIntensity = .8f;
            ConfigureGeometry();
            ConfigureProbes();
            var level = GameObject.Find("_Level");
            if (level) foreach (var light in level.GetComponentsInChildren<Light>())
            { light.lightmapBakeType = LightmapBakeType.Baked; light.shadows = LightShadows.Soft; }
            var desk = GameObject.Find("Front Desk");
            if (desk)
            {
                var bounds = BoundsOf(desk);
                RoomLight("Office_DeskLighting", bounds.center + Vector3.up * (2.65f - bounds.center.y), 2.2f, 8);
                var player = UnityEngine.Object.FindFirstObjectByType<FirstPersonOfficer>();
                Vector3 front = player ? player.transform.position - bounds.center : Vector3.left;
                front.y = 0; front = front.normalized;
                Vector3 fill = bounds.center + front * 2; fill.y = 1.2f;
                RoomLight("Office_DeskFrontLighting", fill, 1.8f, 4.5f);
            }
            var doorRoot = GameObject.Find("Open Door");
            if (doorRoot) RoomLight("Office_StairDoorLighting", BoundsOf(doorRoot).center + new Vector3(-1.2f, 1, 0), 1.6f, 7);
            var lift = UnityEngine.Object.FindFirstObjectByType<UnsafeElevator>();
            if (lift)
            {
                RoomLight("Office_LiftApproachLighting", lift.transform.TransformPoint(new Vector3(0, 2.4f, -1.5f)), 1.4f, 6);
                RoomLight("Office_LiftCabinLighting", lift.transform.TransformPoint(new Vector3(0, 2.1f, .8f)), .75f, 3);
                Reflection("Office_LiftReflection", BoundsOf(lift.gameObject));
            }
            Reflection("Office_RoomReflection", new Bounds(new Vector3(-26, 1.5f, 4), new Vector3(20, 5, 22)));
            EditorUtility.SetDirty(settings);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            File.WriteAllText(Request, "baking"); observingBake = true;
            if (!Lightmapping.BakeAsync()) throw new InvalidOperationException("Unity refused to start the bake. See the Lighting window.");
            File.WriteAllText("Logs/Office-lighting-bake.txt", "Baking started. Original scene/settings backup: " + backup);
        }
        static void ConfigurePipeline()
        {
            string path = Folder + "/OfficeLightingPipeline.asset";
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if (!pipeline)
            {
                var source = QualitySettings.renderPipeline ? QualitySettings.renderPipeline : GraphicsSettings.defaultRenderPipeline;
                pipeline = UnityEngine.Object.Instantiate((UniversalRenderPipelineAsset)source);
                pipeline.name = "OfficeLightingPipeline"; AssetDatabase.CreateAsset(pipeline, path);
            }
            var serialized = new SerializedObject(pipeline);
            SetInt(serialized, "m_MainLightRenderingMode", 1);
            SetInt(serialized, "m_AdditionalLightsRenderingMode", 1);
            SetInt(serialized, "m_AdditionalLightsPerObjectLimit", 8);
            SetBool(serialized, "m_MainLightShadowsSupported", true);
            SetBool(serialized, "m_AdditionalLightShadowsSupported", true);
            SetBool(serialized, "m_SoftShadowsSupported", true);
            SetBool(serialized, "m_SupportsHDR", true);
            SetInt(serialized, "m_AdditionalLightsShadowmapResolution", 2048);
            var renderers = serialized.FindProperty("m_RendererDataList");
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(Folder + "/OfficeRenderer.asset");
            if (!renderer)
            {
                renderer = UnityEngine.Object.Instantiate((UniversalRendererData)renderers.GetArrayElementAtIndex(0).objectReferenceValue);
                renderer.name = "OfficeRenderer"; AssetDatabase.CreateAsset(renderer, Folder + "/OfficeRenderer.asset");
            }
            var rendererSettings = new SerializedObject(renderer);
            SetInt(rendererSettings, "m_RenderingMode", 2); // Forward+ avoids per-object light selection on large room meshes.
            rendererSettings.ApplyModifiedPropertiesWithoutUndo();
            renderers.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            int originalQuality = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            { QualitySettings.SetQualityLevel(i, false); QualitySettings.renderPipeline = pipeline; }
            QualitySettings.SetQualityLevel(originalQuality, false);
            GraphicsSettings.defaultRenderPipeline = pipeline;
            EditorUtility.SetDirty(pipeline); EditorUtility.SetDirty(renderer);
        }
        static void ConfigureGeometry()
        {
            var sources = new HashSet<string>();
            foreach (var renderer in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (renderer is not MeshRenderer && renderer is not SkinnedMeshRenderer) continue;
                bool moving = renderer is SkinnedMeshRenderer || renderer.GetComponentInParent<FirstPersonOfficer>() ||
                    renderer.GetComponentInParent<AssistingOfficer>() || renderer.GetComponentInParent<OfficePerson>() ||
                    renderer.GetComponentInParent<PushDoor>() || renderer.GetComponentInParent<InteractableDoor>() ||
                    renderer.GetComponentInParent<NavMeshAgent>() || renderer.name == "LeftDoor_3D" || renderer.name == "RightDoor_3D";
                moving |= renderer.GetComponentsInParent<MonoBehaviour>().Any(b => b && b.GetType().Name == "Turn_Move");
                var flags = GameObjectUtility.GetStaticEditorFlags(renderer.gameObject);
                if (moving || renderer.GetComponent<TextMesh>())
                {
                    GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, flags & ~StaticEditorFlags.ContributeGI);
                    if (renderer is MeshRenderer mesh) mesh.receiveGI = ReceiveGI.LightProbes;
                    renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
                }
                else
                {
                    GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, flags | StaticEditorFlags.ContributeGI);
                    ((MeshRenderer)renderer).receiveGI = ReceiveGI.Lightmaps;
                    var filter = renderer.GetComponent<MeshFilter>();
                    if (filter && filter.sharedMesh)
                    {
                        string source = AssetDatabase.GetAssetPath(filter.sharedMesh);
                        if (source.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)) sources.Add(source);
                    }
                }
                renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbesAndSkybox;
                renderer.renderingLayerMask = 1;
            }
            foreach (string source in sources)
            {
                var importer = (ModelImporter)AssetImporter.GetAtPath(source);
                if (!importer.generateSecondaryUV) { importer.generateSecondaryUV = true; importer.SaveAndReimport(); }
            }
        }
        static void ConfigureProbes()
        {
            var root = GameObject.Find("Office_BakedLightProbes");
            if (!root) root = new GameObject("Office_BakedLightProbes", typeof(LightProbeGroup));
            var positions = new List<Vector3>();
            Physics.SyncTransforms();
            for (float x = -37; x <= 3; x += 3)
                for (float z = -8; z <= 12; z += 3)
                    foreach (float y in new[] { .55f, 1.45f, 2.45f })
                    {
                        var point = new Vector3(x, y, z);
                        if (Physics.CheckSphere(point, .12f, ~0, QueryTriggerInteraction.Ignore)) continue;
                        if (Physics.Raycast(point, Vector3.down, 4, ~0, QueryTriggerInteraction.Ignore)) positions.Add(root.transform.InverseTransformPoint(point));
                    }
            root.GetComponent<LightProbeGroup>().probePositions = positions.ToArray();
            var lift = UnityEngine.Object.FindFirstObjectByType<UnsafeElevator>();
            if (lift)
            {
                foreach (float x in new[] { -.5f, .5f }) foreach (float z in new[] { .5f, 1.1f })
                    foreach (float y in new[] { .55f, 1.4f, 2.1f }) positions.Add(root.transform.InverseTransformPoint(lift.transform.TransformPoint(new Vector3(x, y, z))));
                root.GetComponent<LightProbeGroup>().probePositions = positions.ToArray();
            }
        }
        static void RoomLight(string name, Vector3 position, float intensity, float range)
        {
            var root = GameObject.Find(name); if (!root) root = new GameObject(name, typeof(Light));
            root.transform.position = position;
            var light = root.GetComponent<Light>(); light.type = LightType.Point; light.color = new Color(.94f, .96f, 1);
            light.intensity = intensity; light.range = range; light.lightmapBakeType = LightmapBakeType.Mixed;
            light.shadows = name == "Office_DeskLighting" ? LightShadows.Soft : LightShadows.None; light.cullingMask = ~0;
        }
        static void Reflection(string name, Bounds bounds)
        {
            var root = GameObject.Find(name); if (!root) root = new GameObject(name, typeof(ReflectionProbe));
            root.transform.position = bounds.center;
            var probe = root.GetComponent<ReflectionProbe>(); probe.size = bounds.size + Vector3.one;
            probe.mode = ReflectionProbeMode.Baked; probe.boxProjection = true; probe.resolution = 128;
            probe.intensity = .8f; probe.clearFlags = ReflectionProbeClearFlags.SolidColor;
            probe.backgroundColor = new Color(.15f, .17f, .2f); probe.cullingMask = ~0;
        }
        static Bounds BoundsOf(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<MeshRenderer>();
            var bounds = renderers.Length > 0 ? renderers[0].bounds : new Bounds(root.transform.position, Vector3.one);
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds); return bounds;
        }
        static void SetInt(SerializedObject obj, string property, int value) { var p = obj.FindProperty(property); if (p != null) p.intValue = value; }
        static void SetBool(SerializedObject obj, string property, bool value) { var p = obj.FindProperty(property); if (p != null) p.boolValue = value; }
        static void Progress()
        {
            if (!observingBake || EditorApplication.timeSinceStartup < nextProgress) return;
            nextProgress = EditorApplication.timeSinceStartup + 5;
            File.WriteAllText("Logs/Office-lighting-progress.txt", "Baking=" + Lightmapping.isRunning + "; progress=" + Lightmapping.buildProgress.ToString("P0"));
        }
        static void Completed()
        {
            if (!observingBake) return;
            observingBake = false;
            File.WriteAllText("Logs/Office-lighting-progress.txt", "Baking=False; progress=100%");
            LightProbes.Tetrahedralize();
            foreach (var probe in UnityEngine.Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None))
                if (probe.name.StartsWith("Office_") && !probe.bakedTexture)
                    Lightmapping.BakeReflectionProbe(probe, Folder + "/" + probe.name + ".exr");
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene()); AssetDatabase.SaveAssets();
            int count = LightmapSettings.lightmaps.Length;
            bool valid = count > 0 && Lightmapping.lightingDataAsset;
            File.WriteAllText(Request, valid ? "complete" : "failed: no saved lighting data");
            File.WriteAllText("Logs/Office-lighting-bake.txt", (valid ? "PASS" : "FAIL") + ": lightmaps=" + count + ", probes=" + (LightmapSettings.lightProbes ? LightmapSettings.lightProbes.count : 0) + ", data=" + AssetDatabase.GetAssetPath(Lightmapping.lightingDataAsset));
            Debug.Log("CLEARANCE: Office lighting bake finished; saved lightmaps=" + count);
            if (valid && File.Exists("Library/ClearanceLightingVerify.request") && File.ReadAllText("Library/ClearanceLightingVerify.request").Trim() == "pending")
                EditorApplication.delayCall += OfficeLightingVerification.Start;
        }
    }
}
