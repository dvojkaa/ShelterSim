using System;
using System.Collections;
using System.IO;
using System.Linq;
using SheltreSim.Player;
using SheltreSim.World;
using UnityEngine;

namespace SheltreSim.Diagnostics
{
    /// <summary>
    /// Run the built player WITH graphics (no -nographics) as <c>--landmark-shots &lt;folder&gt;</c>: builds the ruined version of
    /// each of the six landmark kinds and writes front, three-quarter, aerial and entrance PNGs. Checks nothing, for a human/agent to look at.
    /// </summary>
    public sealed class LandmarkShotProbe : MonoBehaviour
    {
        private string folder;
        private FirstPersonController player;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            string[] args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "--landmark-shots");
            if (i < 0) return;
            var go = new GameObject("LandmarkShotProbe");
            go.AddComponent<LandmarkShotProbe>().folder = i + 1 < args.Length ? args[i + 1] : "landmark_shots";
        }

        private IEnumerator Start()
        {
            yield return null;
            yield return null;
            Directory.CreateDirectory(folder);
            ProceduralWorld world = ProceduralWorld.Active;
            if (world == null) { Debug.LogError("[LandmarkShots] no world"); Application.Quit(1); yield break; }
            player = FindAnyObjectByType<FirstPersonController>();
            // Shots are of the buildings, not of the player's hand: hide the held viewmodels (melee grip and ranged weapon).
            foreach (Transform t in player.GetComponentsInChildren<Transform>(true))
                if (t.name == "WeaponVisual" || t.name.StartsWith("RangedViewmodel", StringComparison.Ordinal)) t.gameObject.SetActive(false);
            world.SetRuined(true);
            RenderSettings.ambientLight = new Color(.55f, .55f, .6f);
            for (int kind = 0; kind < ProceduralLandmarks.KindCount; kind++)
            {
                Vector2Int? found = ProceduralLandmarks.FindNearest(kind, Vector3.zero, world.Seed, 64);
                if (found == null) { Debug.Log("[LandmarkShots] kind " + kind + " none found"); continue; }
                GameObject chunk = world.EnsureChunk(found.Value);
                yield return null;
                Transform root = chunk == null ? null : chunk.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.StartsWith("Landmark_", StringComparison.Ordinal));
                if (root == null) { Debug.LogError("[LandmarkShots] no Landmark_ root for kind " + kind); continue; }
                string n = ProceduralLandmarks.KindName(kind);
                Debug.Log("[LandmarkShots] " + n + " at " + root.position);
                yield return Shot(n + "_front", root, new Vector3(0f, 2.2f, -34f), new Vector3(0f, 3f, 6f), 65f);
                yield return Shot(n + "_threequarter", root, new Vector3(-30f, 9f, -26f), new Vector3(0f, 3f, 6f), 60f);
                yield return Shot(n + "_aerial", root, new Vector3(26f, 24f, -34f), new Vector3(0f, 2f, 6f), 60f);
                yield return Shot(n + "_entrance", root, new Vector3(-3.5f, 1.7f, -14f), new Vector3(0f, 1.6f, 10f), 75f);
            }
            Debug.Log("[LandmarkShots] done");
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
