using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SheltreSim.Items;
using SheltreSim.Player;
using SheltreSim.Survival;
using SheltreSim.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace SheltreSim.Diagnostics
{
    /// <summary>Opt-in standalone streaming/physics probe. Never reads or writes a user save.</summary>
    public sealed class ProceduralWorldProbe : MonoBehaviour
    {
        private ProceduralWorld world;
        private FirstPersonController player;
        private string folder;
        private float started;
        private bool finished;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "--world-probe");
            if (index < 0) return;
            string destination = index + 1 < args.Length && !args[index + 1].StartsWith("-", StringComparison.Ordinal)
                ? args[index + 1] : Path.Combine(Application.temporaryCachePath, "WorldProbe");
            new GameObject("ProceduralWorldProbe").AddComponent<ProceduralWorldProbe>().folder = destination;
        }

        private IEnumerator Start()
        {
            started = Time.realtimeSinceStartup;
            // Flatten nested iterators so exceptions from every test share one failure path.
            var routines = new Stack<IEnumerator>();
            routines.Push(Run());
            while (routines.Count > 0 && !finished)
            {
                object next = null;
                bool advanced;
                try
                {
                    advanced = routines.Peek().MoveNext();
                    if (advanced) next = routines.Peek().Current;
                    else routines.Pop();
                }
                catch (Exception exception)
                {
                    Fail(exception.ToString());
                    yield break;
                }
                if (!advanced) continue;
                if (next is IEnumerator nested) routines.Push(nested);
                else yield return next;
            }
            if (finished) yield break;
            finished = true;
            Debug.Log("[ProceduralWorldProbe] ALL CHECKS PASSED");
            Application.Quit(0);
        }

        private void Update()
        {
            if (!finished && started > 0f && Time.realtimeSinceStartup - started > 60f)
                Fail("Timed out after 60 seconds.");
        }

        private IEnumerator Run()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            Directory.CreateDirectory(folder);
            yield return null;
            yield return null;
            world = ProceduralWorld.Active;
            player = FindFirstObjectByType<FirstPersonController>();
            Require(world != null && player != null, "Runtime world and player exist");
            SheltreSim.Core.HomeBase.Active.DebugStartSurvival();
            player.GetComponent<SurvivalStats>().decayPaused = true;

            var locations = new Dictionary<int, Vector3>();
            for (int z = -12; z <= 12; z++)
                for (int x = -12; x <= 12; x++)
                {
                    float px = x * 512f + 64f;
                    float pz = z * 512f + 64f;
                    int biome = WorldLayout.BiomeIndexAt(px, pz, world.Seed);
                    if (!locations.ContainsKey(biome)) locations.Add(biome, new Vector3(px, 0f, pz));
                }
            Require(locations.Count == 6, "Seed exposes all six biomes within the sampled 12km region");
            for (int biome = 0; biome < 6; biome++)
            {
                Vector3 point = locations[biome];
                yield return Visit(point);
                CheckGround(point);
                CheckNodes();
                Debug.Log($"[ProceduralWorldProbe] Biome {biome}: {WorldLayout.BiomeNames[biome]} at {point}");
                if (biome < 3)
                    Capture("biome_" + biome, point + Vector3.up * (WorldLayout.HeightAt(point.x, point.z, world.Seed) + 8f),
                        point + new Vector3(80f, WorldLayout.HeightAt(point.x + 80f, point.z + 110f, world.Seed) + 5f, 110f));
            }

            yield return Visit(new Vector3(192f, 0f, -320f));
            CheckTown();
            Capture("town_overhead", new Vector3(256f, 170f, -256f), new Vector3(256f, 3f, -255f));
            yield return CheckRevisit();
        }

        private IEnumerator Visit(Vector3 point)
        {
            var body = player.GetComponent<CharacterController>();
            body.enabled = false;
            point.y = WorldLayout.HeightAt(point.x, point.z, world.Seed) + 1.1f;
            player.transform.SetPositionAndRotation(point, Quaternion.identity);
            player.RestoreView(0f);
            body.enabled = true;
            Physics.SyncTransforms();
            for (int frame = 0; frame < 80; frame++) yield return null;
            Physics.SyncTransforms();
            Require(world.LoadedChunkCount > 0 && world.LoadedChunkCount <= 81,
                "Streaming residency is bounded: " + world.LoadedChunkCount);
            Vector2Int coordinate = WorldLayout.Coordinate(point);
            Require(world.transform.Find($"Chunk_{coordinate.x}_{coordinate.y}") != null,
                "Exploration generated the player's chunk without EnsureChunk");
        }

        private void CheckGround(Vector3 point)
        {
            // Filter to terrain so trees, roofs, cars and the player cannot hide a hole.
            for (int i = 0; i < 5; i++)
            {
                float x = point.x + i * 3.1f;
                float z = point.z + i * 2.3f;
                float height = WorldLayout.HeightAt(x, z, world.Seed);
                RaycastHit[] hits = Physics.RaycastAll(new Vector3(x, height + 100f, z), Vector3.down, 200f);
                RaycastHit ground = hits.FirstOrDefault(hit => hit.collider.GetComponent<ProceduralTerrain>() != null);
                Require(ground.collider != null && Mathf.Abs(ground.point.y - height) < .025f,
                    "Terrain collider matches interpolated layout height at " + new Vector2(x, z));
            }
        }

        private void CheckNodes()
        {
            ResourceNode[] nodes = world.GetComponentsInChildren<ResourceNode>();
            foreach (ResourceNode node in nodes)
            {
                Vector3 p = node.transform.position;
                Require(Mathf.Abs(p.y - WorldLayout.HeightAt(p.x, p.z, world.Seed)) < .03f,
                    "Resource pivot rests on terrain: " + node.name);
                Collider collider = node.GetComponent<Collider>();
                Require(collider != null && collider.enabled, "Resource has a live interaction collider: " + node.name);
                // Query strictly inside the collider, avoiding a tangent-only physics test.
                Vector3 inside = collider.bounds.center;
                Require(Physics.OverlapSphere(inside, .04f).Contains(collider),
                    "Resource collider participates in runtime physics: " + node.name);
            }
            Debug.Log($"[ProceduralWorldProbe] Checked {nodes.Length} loaded resource collider/ground pairs");
        }

        private void CheckTown()
        {
            LootContainer[] crates = world.GetComponentsInChildren<LootContainer>()
                .Where(crate => crate.transform.parent.name.StartsWith("Building_", StringComparison.Ordinal)).ToArray();
            Require(crates.Length > 0, "Generated town contains searchable building crates");
            foreach (LootContainer crate in crates)
            {
                Transform building = crate.transform.parent;
                Vector3 door = building.position + new Vector3(0f, .12f, -5.85f);
                Collider[] blocked = Physics.OverlapCapsule(door + Vector3.up * .37f,
                    door + Vector3.up * 1.47f, .35f);
                Require(blocked.All(collider => collider.transform.IsChildOf(player.transform)),
                    "Town doorway admits a standing player capsule: " + building.name);
                Vector3 approach = new Vector3(crate.transform.position.x, building.position.y + 1f, building.position.z - 2f);
                Vector3 target = crate.GetComponent<Collider>().bounds.center;
                Require(Physics.Raycast(approach, (target - approach).normalized, out RaycastHit hit, 10f)
                    && hit.collider.GetComponentInParent<LootContainer>() == crate,
                    "Town crate is reachable from its room aisle");
            }
        }

        private IEnumerator CheckRevisit()
        {
            ResourceNode node = world.GetComponentsInChildren<ResourceNode>().FirstOrDefault(n => n.remainingCharges > 1);
            Require(node != null, "Loaded region has a resource for unload/revisit regression");
            Vector3 anchor = node.transform.position;
            Vector2Int coordinate = WorldLayout.Coordinate(anchor);
            string name = node.name;
            int expected = node.remainingCharges - 1;
            node.Interact(player.gameObject);
            Require(node.remainingCharges == expected, "Gathering changes the real runtime node");
            yield return Visit(anchor + new Vector3(4096f, 0f, 4096f));
            Require(node == null, "Distant original node was destroyed by chunk unloading");
            yield return Visit(anchor + new Vector3(2f, 0f, 2f));
            ResourceNode restored = world.GetComponentsInChildren<ResourceNode>().FirstOrDefault(n =>
                n.name == name && WorldLayout.Coordinate(n.transform.position) == coordinate);
            Require(restored != null && restored.remainingCharges == expected,
                "Revisited generated node preserves harvested charges");
        }

        private void Capture(string name, Vector3 eye, Vector3 target)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
            var host = new GameObject("ProbeCamera");
            Camera camera = host.AddComponent<Camera>();
            camera.CopyFrom(player.GetComponentInChildren<Camera>());
            camera.enabled = false;
            camera.transform.position = eye;
            camera.transform.LookAt(target);
            var render = new RenderTexture(1280, 720, 24);
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.targetTexture = render;
                camera.Render();
                RenderTexture.active = render;
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                image.Apply();
                string path = Path.Combine(folder, name + ".png");
                File.WriteAllBytes(path, image.EncodeToPNG());
                Debug.Log("[ProceduralWorldProbe] Screenshot " + path);
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                Destroy(image);
                render.Release();
                Destroy(render);
                Destroy(host);
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private void Fail(string message)
        {
            if (finished) return;
            finished = true;
            Debug.LogError("[ProceduralWorldProbe] FAILED: " + message);
            Application.Quit(1);
        }
    }
}
