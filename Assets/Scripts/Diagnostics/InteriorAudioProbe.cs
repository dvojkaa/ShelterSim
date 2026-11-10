using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using SheltreSim.Audio;
using SheltreSim.Interaction;
using SheltreSim.Items;
using SheltreSim.Player;
using SheltreSim.World;
using UnityEngine;

namespace SheltreSim.Diagnostics
{
    /// <summary>
    /// <c>--interior-audio-probe</c>: inside a generated house the room tone plays and outside it does not; searching a drawer
    /// plays the drawer sound and searching a cupboard door plays the cabinet sound. Reads GameAudio.PlayLog and
    /// InteriorAudio.Playing, nothing is heard.
    /// </summary>
    public sealed class InteriorAudioProbe : MonoBehaviour
    {
        private int failures;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--interior-audio-probe") >= 0)
                new GameObject(nameof(InteriorAudioProbe)).AddComponent<InteriorAudioProbe>();
        }

        private void Check(bool ok, string message)
        {
            if (ok) Debug.Log("[InteriorAudioProbe] PASS: " + message);
            else { failures++; Debug.LogError("[InteriorAudioProbe] FAIL: " + message); }
        }

        private IEnumerator Start()
        {
            yield return null;
            yield return null;
            ProceduralWorld world = ProceduralWorld.Active;
            FirstPersonController player = FindAnyObjectByType<FirstPersonController>();
            if (world == null || player == null) { Check(false, "world and player exist"); Finish(); yield break; }
            foreach (string key in new[] { Sfx.DrawerOpen, Sfx.CabinetOpen, Sfx.EngineStart, Sfx.EngineStop, Sfx.AmbInterior, Sfx.MotorBuggy, Sfx.MotorVan, Sfx.BikeChain })
                Check(GameAudio.HasClip(key), "clip imported: " + key);

            world.SetRuined(true);
            Vector2Int suburb = Find(world.Seed);
            GameObject chunk = world.EnsureChunk(suburb);
            yield return null;
            Transform house = chunk.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.StartsWith("House_", StringComparison.Ordinal));
            if (house == null) { Check(false, "found a house"); Finish(); yield break; }

            Teleport(player, house.TransformPoint(new Vector3(0f, .1f, -2.5f)));
            yield return new WaitForSeconds(2.5f);
            InteriorAudio interior = player.GetComponent<InteriorAudio>();
            Check(interior != null, "the player has InteriorAudio");
            Check(BuildingInterior.PlayerIn != null, "the player counts as inside the house");
            Check(interior != null && interior.Playing, "the room tone plays inside the house");

            // Searching furniture: one with drawers, one with doors.
            var containers = house.GetComponentsInChildren<LootContainer>(true).ToList();
            LootContainer drawers = containers.FirstOrDefault(c => c.GetComponentsInChildren<Transform>(true).Any(t => t.name.StartsWith("Drawer_", StringComparison.Ordinal)) && !c.Searched);
            LootContainer doors = containers.FirstOrDefault(c => c.GetComponentsInChildren<Transform>(true).Any(t => t.name == "Door_L" || t.name == "Door_R") && !c.Searched);
            Check(drawers != null, "the house has a searchable container with drawers");
            Check(doors != null, "the house has a searchable container with doors");
            if (drawers != null) Check(Search(drawers, player, Sfx.DrawerOpen), "searching a drawer plays the drawer sound");
            if (doors != null) Check(Search(doors, player, Sfx.CabinetOpen), "searching a cupboard plays the cabinet sound");

            Teleport(player, house.TransformPoint(new Vector3(0f, .1f, -60f)));
            yield return new WaitForSeconds(3.5f);
            Check(BuildingInterior.PlayerIn == null, "the player is outside after walking away");
            Check(interior != null && !interior.Playing, "the room tone stops outside");
            Finish();
        }

        private bool Search(LootContainer container, FirstPersonController player, string expectedKey)
        {
            GameAudio.PlayLog = new List<string>();
            ((IInteractable)container).Interact(player.gameObject);
            bool ok = GameAudio.PlayLog.Contains(expectedKey);
            if (!ok) Debug.Log("[InteriorAudioProbe] played: " + string.Join(",", GameAudio.PlayLog));
            return ok;
        }

        private static void Teleport(FirstPersonController player, Vector3 position)
        {
            var controller = player.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            player.transform.position = position;
            if (controller != null) controller.enabled = true;
        }

        private static Vector2Int Find(int seed)
        {
            for (int radius = 1; radius < 64; radius++)
                for (int z = -radius; z <= radius; z++)
                    for (int x = -radius; x <= radius; x++)
                    {
                        var c = new Vector2Int(x, z);
                        if (!WorldLayout.IsSuburbChunk(c, seed) || ProceduralLandmarks.HasLandmark(c, seed)) continue;
                        return c;
                    }
            return Vector2Int.zero;
        }

        private void Finish()
        {
            Debug.Log(failures == 0 ? "[InteriorAudioProbe] ALL CHECKS PASSED" : $"[InteriorAudioProbe] {failures} CHECKS FAILED");
            Application.Quit(failures == 0 ? 0 : 1);
        }
    }
}
