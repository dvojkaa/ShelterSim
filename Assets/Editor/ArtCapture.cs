using System.IO;
using System.Linq;
using SheltreSim.Enemies;
using SheltreSim.Items;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SheltreSim.EditorTools
{
    /// <summary>
    /// Renders real in-game views of the built scene to ArtSource/previews/ingame_*.png, so the
    /// look can be checked without a human or Play Mode. Needs a GPU: run in -batchmode WITHOUT
    /// -nographics. This never enters Play Mode and saves nothing into the scene.
    /// </summary>
    public static class ArtCapture
    {
        private static string outDir;

        /// <summary>Batch entry point: full scene rebuild, release checks, then the screenshots.</summary>
        public static void RebuildVerifyAndCapture()
        {
            ReleaseVerification.RebuildAndVerify();
            CaptureAll();
        }

        [MenuItem("Tools/SheltreSim/Capture Art Previews")]
        public static void CaptureAll()
        {
            outDir = Path.GetFullPath("ArtSource/previews");
            Directory.CreateDirectory(outDir);

            Shot("bunker_inside", new Vector3(77f, 1.6f, -221f), new Vector3(68f, 0.8f, -214f));
            Shot("bunker_door", new Vector3(72f, 1.6f, -216f), new Vector3(72f, 1.3f, -224f));
            Shot("site", new Vector3(60f, 7.6f, -243f), new Vector3(72f, 7f, -231f));

            Near<ResourceNode>("tree", n => n.transform.Find("Trunk") != null, 7f, 2f);
            Near<ResourceNode>("log", n => n.resourceType != null && n.resourceType.id == "wood" && n.transform.Find("Trunk") == null, 5f, 1.3f);
            Near<ResourceNode>("scrap", n => n.resourceType != null && n.resourceType.id == "scrap_metal", 3.5f, 0.5f);
            Near<ResourceNode>("stone", n => n.resourceType != null && n.resourceType.id == "stone", 3.5f, 0.4f);
            Near<ResourceNode>("iron", n => n.resourceType != null && n.resourceType.id == "iron_ore", 3.5f, 0.4f);
            Near<ResourceNode>("bush", n => n.resourceType != null && n.resourceType.id == "food_berries", 3.5f, 0.9f);
            Near<EnemyAI>("raider", e => true, 3.5f, 0f);

            GameObject outdoor = GameObject.Find("OutdoorArea");
            Transform building = outdoor != null ? outdoor.transform.Find("ExpandedWasteland/Buildings/Building_1") : null;
            if (building != null)
            {
                Shot("ruin_front", building.TransformPoint(new Vector3(0f, 1.6f, -11f)), building.TransformPoint(new Vector3(0f, 1.6f, 0f)));
                Shot("ruin_corner", building.TransformPoint(new Vector3(9f, 3.2f, 9f)), building.TransformPoint(new Vector3(0f, 1.5f, 0f)));
                Shot("ruin_inside", building.TransformPoint(new Vector3(0f, 1.6f, -1.2f)), building.TransformPoint(new Vector3(0.5f, 0.6f, 3f)));
            }
            Transform car = outdoor != null ? outdoor.transform.Find("ExpandedWasteland/Cars/Car_1") : null;
            Transform car2 = outdoor != null ? outdoor.transform.Find("ExpandedWasteland/Cars/Car_2") : null;
            if (car != null) Shot("car_a", car.TransformPoint(new Vector3(-4f, 1.6f, -5f)), car.position + Vector3.up * 0.7f);
            if (car2 != null) Shot("car_b", car2.TransformPoint(new Vector3(4f, 1.6f, -5f)), car2.position + Vector3.up * 0.7f);

            ShotFromPlayer("first_person");
            ShotBuildPieces();
            ShotRuntimeStates();
            Debug.Log("[ArtCapture] Previews written to " + outDir);
        }

        /// <summary>Every build piece as a player would place it, plus a row tinted the way BuildModeController tints its ghost. Destroyed afterwards, never saved.</summary>
        private static void ShotBuildPieces()
        {
            string[] names = { "WallSegmentPiece", "FloorTilePiece", "WorkbenchPiece", "BedPiece", "StorageCratePiece", "WaterCollectorPiece", "GeneratorPiece" };
            float[] heights = { 3f, 0.2f, 1f, 0.5f, 1f, 1.2f, 1.2f };
            GameObject holder = new GameObject("CaptureHolder");
            Vector3 origin = new Vector3(0f, 0f, -120f);
            MaterialPropertyBlock ghost = new MaterialPropertyBlock();
            ghost.SetColor("_Color", new Color(0.2f, 1f, 0.3f));
            ghost.SetColor("_BaseColor", new Color(0.2f, 1f, 0.3f));
            for (int row = 0; row < 2; row++)
            {
                for (int i = 0; i < names.Length; i++)
                {
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Building/" + names[i] + ".prefab");
                    GameObject piece = Object.Instantiate(prefab, origin + new Vector3((i - 3) * 2.6f, heights[i] / 2f, row * 4f), Quaternion.identity, holder.transform);
                    if (row == 1)
                        foreach (MeshRenderer renderer in piece.GetComponentsInChildren<MeshRenderer>()) renderer.SetPropertyBlock(ghost);
                }
            }
            // Two walls and two floors edge to edge, to see whether they tile.
            GameObject wall = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Building/WallSegmentPiece.prefab");
            GameObject tile = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Building/FloorTilePiece.prefab");
            for (int i = 0; i < 3; i++)
            {
                Object.Instantiate(wall, origin + new Vector3(-2f + i * 2f, 1.5f, -8f), Quaternion.identity, holder.transform);
                Object.Instantiate(tile, origin + new Vector3(-2f + i * 2f, 0.1f, -6.9f), Quaternion.identity, holder.transform);
            }
            Shot("pieces", origin + new Vector3(0f, 3f, -5.5f), origin + new Vector3(0f, 0.5f, 1.5f));
            Shot("pieces_tiling", origin + new Vector3(1.5f, 2f, -2.5f), origin + new Vector3(0f, 1f, -7.5f));
            Object.DestroyImmediate(holder);
        }

        /// <summary>The visual states only runtime code produces: open door, damage flash, mid-stride limbs.</summary>
        private static void ShotRuntimeStates()
        {
            GameObject hinge = GameObject.Find("DoorHinge");
            if (hinge != null)
            {
                Quaternion closed = hinge.transform.localRotation;
                hinge.transform.localRotation = closed * Quaternion.Euler(0f, 90f, 0f);
                Shot("door_open_inside", new Vector3(72.4f, 1.6f, -219f), new Vector3(72f, 1.2f, -224f));
                hinge.transform.localRotation = closed;
            }

            EnemyAI raider = Object.FindObjectsByType<EnemyAI>(FindObjectsInactive.Include).OrderBy(e => e.name).FirstOrDefault();
            if (raider == null) return;
            Transform visual = raider.transform.Find("Visual");
            Transform legL = visual != null ? visual.Find("Leg_L") : null;
            Transform legR = visual != null ? visual.Find("Leg_R") : null;
            Transform armL = visual != null ? visual.Find("Arm_L") : null;
            Vector3 p = raider.transform.position;
            Vector3 eye = p + raider.transform.right * 3f + raider.transform.forward * 1.2f;
            eye.y = 1.4f;
            if (legL != null) legL.localRotation = Quaternion.Euler(32f, 0f, 0f);
            if (legR != null) legR.localRotation = Quaternion.Euler(-32f, 0f, 0f);
            if (armL != null) armL.localRotation = Quaternion.Euler(-26f, 0f, 0f);
            Shot("raider_stride", eye, p);
            if (legL != null) legL.localRotation = Quaternion.identity;
            if (legR != null) legR.localRotation = Quaternion.identity;
            if (armL != null) armL.localRotation = Quaternion.identity;

            MaterialPropertyBlock flash = new MaterialPropertyBlock();
            flash.SetColor("_Color", Color.white);
            flash.SetColor("_EmissionColor", Color.white * 0.75f);
            Renderer[] renderers = raider.GetComponentsInChildren<Renderer>();
            foreach (Renderer renderer in renderers) renderer.SetPropertyBlock(flash);
            Shot("raider_flash", eye, p);
            foreach (Renderer renderer in renderers) renderer.SetPropertyBlock(null);
        }

        private static void Near<T>(string name, System.Func<T, bool> filter, float distance, float aimHeight) where T : Component
        {
            T target = Object.FindObjectsByType<T>(FindObjectsInactive.Include).Where(filter).OrderBy(t => t.name).FirstOrDefault();
            if (target == null) return;
            Vector3 aim = new Vector3(target.transform.position.x, aimHeight > 0f ? aimHeight : target.transform.position.y, target.transform.position.z);
            Vector3 eye = aim + new Vector3(distance * 0.75f, 0f, distance * 0.66f);
            eye.y = 1.6f;
            Shot(name, eye, aim);
        }

        private static void Shot(string name, Vector3 eye, Vector3 target)
        {
            GameObject cameraObject = new GameObject("ArtCaptureCamera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = eye;
            camera.transform.rotation = Quaternion.LookRotation(target - eye);
            camera.fieldOfView = 60f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 400f;
            Render(camera, name);
            Object.DestroyImmediate(cameraObject);
        }

        private static void ShotFromPlayer(string name)
        {
            GameObject player = GameObject.Find("Player");
            Camera camera = player != null ? player.GetComponentInChildren<Camera>() : null;
            if (camera != null) Render(camera, name);
        }

        private static void Render(Camera camera, string name)
        {
            const int width = 1280, height = 720;
            RenderTexture target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            target.antiAliasing = 4;
            RenderTexture previousTarget = camera.targetTexture;
            camera.targetTexture = target;
            camera.Render();
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture.active = target;
            Texture2D image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            RenderTexture.active = previousActive;
            camera.targetTexture = previousTarget;
            File.WriteAllBytes(Path.Combine(outDir, "ingame_" + name + ".png"), image.EncodeToPNG());
            Object.DestroyImmediate(image);
            target.Release();
            Object.DestroyImmediate(target);
        }
    }
}
