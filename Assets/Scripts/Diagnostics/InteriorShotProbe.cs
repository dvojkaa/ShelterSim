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
    /// Run the built player WITH graphics (no -nographics) as <c>--interior-shots &lt;folder&gt;</c>: generates a ruined house and an
    /// apartment block, puts a camera outside, in the hall, the stairs and an upper room, and writes PNGs. For a person (or an agent)
    /// to look at the furnished interiors; it checks nothing.
    /// </summary>
    public sealed class InteriorShotProbe : MonoBehaviour
    {
        private string folder;
        private FirstPersonController player;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            string[] args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "--interior-shots");
            if (i < 0) return;
            var go = new GameObject("InteriorShotProbe");
            go.AddComponent<InteriorShotProbe>().folder = i + 1 < args.Length ? args[i + 1] : "interior_shots";
        }

        private IEnumerator Start()
        {
            yield return null;
            yield return null;
            Directory.CreateDirectory(folder);
            ProceduralWorld world = ProceduralWorld.Active;
            if (world == null) { Debug.LogError("[InteriorShots] no world"); Application.Quit(1); yield break; }
            player = FindAnyObjectByType<FirstPersonController>();
            world.SetRuined(true);
            RenderSettings.ambientLight = new Color(.45f, .45f, .5f);
            Vector2Int suburb = Find(world.Seed, false);
            Vector2Int town = Find(world.Seed, true);
            GameObject suburbChunk = world.EnsureChunk(suburb);
            GameObject townChunk = world.EnsureChunk(town);
            yield return null;
            Transform house = Roots(suburbChunk, "House_").FirstOrDefault();
            Transform block = Roots(townChunk, "Building_").FirstOrDefault(b => b.Find("Floor_4") != null) ?? Roots(townChunk, "Building_").FirstOrDefault();
            Debug.Log($"[InteriorShots] house={(house != null ? house.name : "none")} block={(block != null ? block.name : "none")}");
            if (house != null)
            {
                yield return Shot("house_front", house, new Vector3(-4f, 2.2f, -14f), new Vector3(0f, 1.8f, 0f));
                yield return Shot("house_hall", house, new Vector3(0f, 1.6f, -4.5f), new Vector3(0f, 1.2f, 4f));
                yield return Shot("house_living", house, new Vector3(3f, 1.6f, -2f), new Vector3(-4.3f, 1.2f, -2f));
                yield return Shot("house_kitchen", house, new Vector3(-3f, 1.6f, 1f), new Vector3(-1f, 1.2f, 5.4f));
                yield return Shot("house_stairs", house, new Vector3(-3f, 1.6f, -4f), new Vector3(3f, 1.8f, 0f));
                yield return Shot("house_upstairs", house, new Vector3(0f, 4.6f, 0f), new Vector3(-3f, 4.0f, 5f));
            }
            if (block != null)
            {
                yield return Shot("block_front", block, new Vector3(-6f, 2.5f, -24f), new Vector3(0f, 8f, 0f), 62f);
                yield return Shot("block_lobby", block, new Vector3(0f, 1.6f, -9f), new Vector3(0f, 1.4f, 0f));
                int roofLevel = block.Cast<Transform>().Count(c => c.name.StartsWith("Floor_", StringComparison.Ordinal)) - 1;
                yield return Shot("block_roof", block, new Vector3(0f, roofLevel * 3f + 1.9f, -2f), new Vector3(0f, roofLevel * 3f + 1.2f, 6f), 75f);
                yield return Shot("block_floor2", block, new Vector3(0f, 7.6f, -3f), new Vector3(0f, 7.2f, 5f));
            }
            Debug.Log("[InteriorShots] done");
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
            camera.farClipPlane = 250f;
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

        private static Vector2Int Find(int seed, bool town)
        {
            for (int radius = 1; radius < 64; radius++)
                for (int z = -radius; z <= radius; z++)
                    for (int x = -radius; x <= radius; x++)
                    {
                        var coordinate = new Vector2Int(x, z);
                        if (town ? !WorldLayout.IsTownChunk(coordinate, seed) : !WorldLayout.IsSuburbChunk(coordinate, seed)) continue;
                        if (ProceduralLandmarks.HasLandmark(coordinate, seed)) continue;
                        return coordinate;
                    }
            return Vector2Int.zero;
        }

        private static Transform[] Roots(GameObject chunk, string prefix) => chunk.GetComponentsInChildren<Transform>(true)
            .Where(t => t.name.StartsWith(prefix, StringComparison.Ordinal) &&
                        (t.parent == null || !t.parent.name.StartsWith(prefix, StringComparison.Ordinal))).ToArray();
    }
}
