using System;
using System.Collections;
using System.IO;
using System.Linq;
using SheltreSim.Bestiary;
using SheltreSim.Player;
using SheltreSim.World;
using UnityEngine;

namespace SheltreSim.Diagnostics
{
    /// <summary>Run WITH graphics as <c>--camp-shots &lt;folder&gt;</c>: renders the guaranteed raider camp from four angles. Checks nothing.</summary>
    public sealed class CampShotProbe : MonoBehaviour
    {
        private string folder;
        private FirstPersonController player;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            string[] args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "--camp-shots");
            if (i < 0) return;
            new GameObject("CampShotProbe").AddComponent<CampShotProbe>().folder = i + 1 < args.Length ? args[i + 1] : "camp_shots";
        }

        private IEnumerator Start()
        {
            yield return null; yield return null;
            Directory.CreateDirectory(folder);
            ProceduralWorld world = ProceduralWorld.Active;
            if (world == null) { Debug.LogError("[CampShots] no world"); Application.Quit(1); yield break; }
            player = FindAnyObjectByType<FirstPersonController>();
            foreach (Transform t in player.GetComponentsInChildren<Transform>(true))
                if (t.name == "WeaponVisual" || t.name.StartsWith("RangedViewmodel", StringComparison.Ordinal)) t.gameObject.SetActive(false);
            world.SetRuined(true);
            RenderSettings.ambientLight = new Color(.55f, .55f, .6f);
            Vector2Int? found = EnemyCamps.Guaranteed(world.Seed);
            if (found == null) { Debug.LogError("[CampShots] no camp"); Application.Quit(1); yield break; }
            GameObject chunk = world.EnsureChunk(found.Value);
            yield return null; yield return null;
            Transform root = chunk.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.IndexOf("Camp", StringComparison.OrdinalIgnoreCase) >= 0 && t.parent == chunk.transform);
            if (root == null) { Debug.LogError("[CampShots] camp root not found"); Application.Quit(1); yield break; }
            Debug.Log("[CampShots] " + root.name + " at " + root.position);
            yield return Shot("camp_a", root, new Vector3(0f, 6f, -22f), new Vector3(0f, 1f, 0f), 60f);
            yield return Shot("camp_b", root, new Vector3(-18f, 7f, 8f), new Vector3(0f, 1f, 0f), 60f);
            yield return Shot("camp_c", root, new Vector3(16f, 4f, 14f), new Vector3(0f, 1f, 0f), 60f);
            yield return Shot("camp_fire", root, new Vector3(2f, 2.2f, -9f), new Vector3(0f, .6f, -4f), 65f);
            yield return Shot("camp_tent", root, new Vector3(0f, 3f, 2f), new Vector3(0f, 1f, 9.5f), 65f);
            Debug.Log("[CampShots] done");
            Application.Quit(0);
        }

        private IEnumerator Shot(string name, Transform building, Vector3 localEye, Vector3 localTarget, float fov = 70f)
        {
            Vector3 eye = building.TransformPoint(localEye);
            Vector3 target = building.TransformPoint(localTarget);
            // Interiors are hidden until the player is near, so stand the player where the camera is and let BuildingInterior update.
            if (player != null)
            {
                var controller = player.GetComponent<CharacterController>();
                if (controller != null) controller.enabled = false;
                player.transform.position = eye - Vector3.up * 1.6f;
                if (controller != null) controller.enabled = true;
            }
            yield return new WaitForSeconds(0.8f);
            var cameraObject = new GameObject("ShotCamera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = eye;
            camera.transform.rotation = Quaternion.LookRotation(target - eye);
            camera.fieldOfView = fov;
            camera.nearClipPlane = .05f;
            camera.farClipPlane = 400f;
            var lightObject = new GameObject("ShotLight");
            lightObject.transform.SetParent(cameraObject.transform, false);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point; light.range = 12f; light.intensity = 1.4f;
            var target2D = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = target2D;
            camera.Render();
            RenderTexture.active = target2D;
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            image.Apply();
            File.WriteAllBytes(Path.Combine(folder, name + ".png"), image.EncodeToPNG());
            RenderTexture.active = null;
            Destroy(cameraObject);
            Destroy(target2D);
            Destroy(image);
        }

    }
}
