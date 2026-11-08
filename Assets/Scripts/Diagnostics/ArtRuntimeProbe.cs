using System;
using System.Collections;
using System.IO;
using System.Linq;
using SheltreSim.Enemies;
using SheltreSim.Interaction;
using SheltreSim.Items;
using SheltreSim.Player;
using UnityEngine;

namespace SheltreSim.Diagnostics
{
    /// <summary>
    /// Opt-in check of the M16 art swap in the running player: `--art-probe &lt;screenshot folder&gt;`
    /// without -nographics. Covers what only exists at runtime and batch construction can't prove:
    /// limbs moving with real AI movement, damage flash, node depletion, door swing, and shaders
    /// surviving into the build (the screenshots would be pink otherwise). Normal launches never
    /// create this component.
    /// </summary>
    public sealed class ArtRuntimeProbe : MonoBehaviour
    {
        private string folder;
        private int failures;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "--art-probe");
            if (index < 0 || index + 1 >= args.Length) return;
            GameObject host = new GameObject("ArtRuntimeProbe");
            host.AddComponent<ArtRuntimeProbe>().folder = args[index + 1];
        }

        private IEnumerator Start()
        {
            // Uncapped batch frame rates (9000+ fps) make per-frame moves smaller than
            // CharacterController.minMoveDistance, which silently freezes every raider.
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Directory.CreateDirectory(folder);
            yield return null;
            yield return null;
            Capture("runtime_spawn");
            SheltreSim.Core.HomeBase.Active.DebugStartSurvival();
            var world = SheltreSim.World.ProceduralWorld.Active;
            foreach (var chunk in new[] { new Vector2Int(1, -2), new Vector2Int(1, -3), new Vector2Int(2, -2), new Vector2Int(2, -3), new Vector2Int(6, -6), new Vector2Int(7, -6), new Vector2Int(15, 15), new Vector2Int(16, 15) })
                world.EnsureChunk(chunk);
            yield return null;

            yield return CheckRaiderAnimation();
            yield return CheckDamageFlash();
            yield return CheckNodeDepletion();
            yield return CheckDoor();

            Debug.Log(failures == 0 ? "[ArtRuntimeProbe] ALL CHECKS PASSED" : $"[ArtRuntimeProbe] {failures} CHECK(S) FAILED");
            Application.Quit(failures == 0 ? 0 : 1);
        }

        private IEnumerator CheckRaiderAnimation()
        {
            EnemyAI[] raiders = FindObjectsByType<EnemyAI>();
            Check(raiders.Length > 0, $"{raiders.Length} raiders exist");
            Check(raiders.All(r => r.GetComponent<RaiderWalkAnimator>() != null && r.transform.Find("Visual/Leg_L") != null && r.transform.Find("WeaponProp/Visual") != null),
                "Every raider has its model, weapon and walk animator");

            // A hit makes a raider chase whatever its detection radius says: real AI movement then drives the limbs.
            FirstPersonController player = FindFirstObjectByType<FirstPersonController>();
            EnemyAI raider = raiders.OrderBy(r => r.name).First();
            CharacterController playerBody = player.GetComponent<CharacterController>();
            playerBody.enabled = false;
            player.transform.position = raider.transform.position + Vector3.back * 8f + Vector3.up * 0.1f;
            playerBody.enabled = true;

            yield return null;
            raider.TakeDamage(1f);

            Transform leg = raider.transform.Find("Visual/Leg_L");
            CharacterController body = raider.GetComponent<CharacterController>();
            float maxSwing = 0f, maxSpeed = 0f;
            Vector3 start = raider.transform.position;
            int frames = 0;
            float nextLog = 0f;
            for (float t = 0f; t < 2.5f; t += Time.deltaTime)
            {
                if (raider == null) break;
                frames++;
                if (t >= nextLog)
                {
                    nextLog += 0.5f;
                    Debug.Log($"[ArtRuntimeProbe] t={t:F2} frame={frames} raider={raider.transform.position:F2} player={player.transform.position:F2} velocity={body.velocity:F2} leg={leg.localEulerAngles.x:F1} timeScale={Time.timeScale}");
                }
                float angle = Quaternion.Angle(Quaternion.identity, leg.localRotation);
                maxSwing = Mathf.Max(maxSwing, angle);
                maxSpeed = Mathf.Max(maxSpeed, new Vector3(body.velocity.x, 0f, body.velocity.z).magnitude);
                yield return null;
            }
            Debug.Log($"[ArtRuntimeProbe] raider moved {(raider.transform.position - start).magnitude:F2} m, peak speed {maxSpeed:F2} m/s, peak leg swing {maxSwing:F1} deg");
            Check((raider.transform.position - start).magnitude > 2f, "Raider really walked metres during the test");
            Check(maxSwing > 20f, "Legs swing fully while walking");
            Check(raider.transform.Find("WeaponProp/Visual") != null, "Weapon model still attached");

            Camera camera = player.GetComponentInChildren<Camera>();
            camera.transform.LookAt(raider.transform.position + Vector3.up * 0.3f);
            Capture("runtime_raider_walking");
        }

        private IEnumerator CheckDamageFlash()
        {
            EnemyAI raider = FindObjectsByType<EnemyAI>().OrderBy(r => r.name).Last();
            Renderer renderer = raider.GetComponentsInChildren<Renderer>().First();
            float before = raider.GetComponentsInChildren<Renderer>().Length;
            float feet = raider.GetComponentsInChildren<Renderer>().Min(r => r.bounds.min.y);
            Debug.Log($"[ArtRuntimeProbe] idle raider pivot y={raider.transform.position.y:F3}, lowest model point y={feet:F3}");
            float ground = SheltreSim.World.WorldLayout.HeightAt(raider.transform.position.x, raider.transform.position.z, SheltreSim.World.ProceduralWorld.Active.Seed);
            Check(Mathf.Abs(feet - ground) < 0.1f, $"Standing raider's feet are on the ground ({feet - ground:F3})");
            raider.TakeDamage(1f);
            yield return null;
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            Check(block.GetColor("_EmissionColor").maxColorComponent > 0.1f, "Hit raider flashes through emission");
            Check(renderer.sharedMaterial.IsKeywordEnabled("_EMISSION"), "SS_Raider keeps its emission keyword in the build");
            yield return new WaitForSeconds(0.3f);
            renderer.GetPropertyBlock(block);
            Check(block.GetColor("_EmissionColor").maxColorComponent < 0.01f, "Flash ends and the raider returns to normal");
            Check(before > 3f, "Raider renders through its limb parts");
        }

        private IEnumerator CheckNodeDepletion()
        {
            FirstPersonController player = FindFirstObjectByType<FirstPersonController>();
            foreach (string id in new[] { "wood", "stone", "iron_ore", "food_berries" })
            {
                ResourceNode node = FindObjectsByType<ResourceNode>().First(n => n.resourceType != null && n.resourceType.id == id);
                Renderer[] renderers = node.GetComponentsInChildren<Renderer>(true);
                Check(renderers.Length > 0 && renderers.All(r => r.transform.IsChildOf(node.transform) && IsUnderVisual(r.transform, node.transform)),
                    $"{id} node {node.name} renders only through its model");
                for (int i = 0; i < 20 && !node.IsDepleted; i++) node.Interact(player.gameObject);
                yield return null;
                Check(node.IsDepleted && renderers.All(r => !r.enabled), $"{id} model disappears when depleted");
                node.RestoreState(3, false, 0f);
                yield return null;
                Check(renderers.All(r => r.enabled), $"{id} model returns on regrow");
            }
        }

        /// <summary>A clone of the inactive template must look, animate and flash like a scene original.</summary>
        private void MovePlayer(Vector3 position)
        {
            FirstPersonController player = FindFirstObjectByType<FirstPersonController>();
            CharacterController body = player.GetComponent<CharacterController>();
            body.enabled = false;
            player.transform.position = position;
            body.enabled = true;
        }

        private static bool IsUnderVisual(Transform t, Transform root)
        {
            for (Transform p = t; p != null && p != root; p = p.parent)
                if (p.name == "Visual") return true;
            return false;
        }

        private IEnumerator CheckDoor()
        {
            InteractableDoor door = FindFirstObjectByType<InteractableDoor>();
            Transform leaf = door.transform.Find("Visual");
            Check(leaf != null && leaf.GetComponent<Renderer>() != null, "Door uses the leaf model");
            Vector3 closed = leaf.position;
            door.RestoreState(true);
            yield return null;
            Check((leaf.position - closed).magnitude > 0.5f, "Door model swings with the hinge");
            door.RestoreState(false);
            yield return null;
            Check((leaf.position - closed).magnitude < 0.01f, "Door model returns when closed");

            FirstPersonController player = FindFirstObjectByType<FirstPersonController>();
            CharacterController body = player.GetComponent<CharacterController>();
            body.enabled = false;
            player.transform.position = new Vector3(72f, 0.2f, -217.5f);
            body.enabled = true;
            player.GetComponentInChildren<Camera>().transform.localRotation = Quaternion.identity;
            yield return null;
            Capture("runtime_bunker");
        }

        private void Capture(string name)
        {
            Camera camera = FindFirstObjectByType<FirstPersonController>().GetComponentInChildren<Camera>();
            RenderTexture target = new RenderTexture(1280, 720, 24);
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            Texture2D image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            camera.targetTexture = null;
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(folder, name + ".png"), image.EncodeToPNG());
            Destroy(image);
            Destroy(target);
        }

        private void Check(bool condition, string message)
        {
            Debug.Log($"[ArtRuntimeProbe] {(condition ? "PASS" : "FAIL")}: {message}");
            if (!condition) failures++;
        }
    }
}
