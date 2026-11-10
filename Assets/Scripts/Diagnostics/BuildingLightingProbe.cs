using System;
using System.Collections;
using System.Linq;
using SheltreSim.Player;
using SheltreSim.World;
using UnityEngine;

namespace SheltreSim.Diagnostics
{
    /// <summary>Silent --building-lighting-probe check for generated-house and ruined-building fixtures.</summary>
    public sealed class BuildingLightingProbe : MonoBehaviour
    {
        private int failures;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--building-lighting-probe") >= 0)
                new GameObject(nameof(BuildingLightingProbe)).AddComponent<BuildingLightingProbe>();
        }

        private void Check(bool ok, string message)
        {
            if (ok) Debug.Log("[BuildingLightingProbe] PASS: " + message);
            else { failures++; Debug.LogError("[BuildingLightingProbe] FAIL: " + message); }
        }

        private IEnumerator Start()
        {
            yield return null;
            yield return null;
            ProceduralWorld world = ProceduralWorld.Active;
            WorldAtmosphere atmosphere = FindFirstObjectByType<WorldAtmosphere>();
            FirstPersonController player = FindFirstObjectByType<FirstPersonController>();
            Check(world != null && atmosphere != null && player != null, "world, atmosphere and player exist");
            if (world == null || atmosphere == null || player == null) { Finish(); yield break; }

            world.SetRuined(false);
            Vector2Int suburb = FindChunk(world.Seed, false);
            Teleport(player, new Vector3(suburb.x * WorldLayout.ChunkSize + 64f, 5f, suburb.y * WorldLayout.ChunkSize + 64f));
            GameObject chunk = world.EnsureChunk(suburb);
            yield return null;
            BuildingInterior house = chunk != null
                ? chunk.GetComponentsInChildren<BuildingInterior>(true).FirstOrDefault(b => b.name.StartsWith("House_", StringComparison.Ordinal))
                : null;
            Check(house != null, "generated a normal house");
            if (house == null) { Finish(); yield break; }
            Light[] warm = house.GetComponentsInChildren<Light>(true);
            Check(warm.Length == 2 && warm.Any(l => l.name == "WarmLamp_Living") && warm.Any(l => l.name == "WarmLamp_Kitchen"),
                "normal house has living-room and kitchen warm lamps");
            atmosphere.RestoreTime(200f); // 12:00
            Teleport(player, house.transform.position + new Vector3(0f, .1f, 0f));
            yield return new WaitForSeconds(.3f);
            Check(warm.All(l => !l.enabled), "normal lamps are off by day");
            atmosphere.RestoreTime(700f); // 22:00
            yield return new WaitForSeconds(.3f);
            Check(warm.All(l => l.enabled), "normal lamps turn on at night near the house");
            Teleport(player, house.transform.position + new Vector3(40f, .1f, 0f));
            yield return new WaitForSeconds(.3f);
            Check(warm.All(l => !l.enabled), "normal lamps turn off outside the local 30 m area");

            world.SetRuined(true);
            // SetRuined rebuilds loaded chunks nearest the player; keep the test chunk in that budget.
            Teleport(player, new Vector3(suburb.x * WorldLayout.ChunkSize + 64f, 5f, suburb.y * WorldLayout.ChunkSize + 64f));
            chunk = world.EnsureChunk(suburb);
            yield return null;
            BuildingInterior ruinedHouse = chunk != null
                ? chunk.GetComponentsInChildren<BuildingInterior>(true).FirstOrDefault(b => b.name.StartsWith("House_", StringComparison.Ordinal))
                : null;
            Check(ruinedHouse != null, "generated a ruined house");
            if (ruinedHouse == null) { Finish(); yield break; }
            Light[] emergency = ruinedHouse.GetComponentsInChildren<Light>(true);
            int storeys = ruinedHouse.transform.childCount > 0 ? CountFloors(ruinedHouse.transform) : 0;
            Check(emergency.Length == (storeys + 1) / 2, "ruined house has one emergency lamp per two storeys");
            Check(emergency.Length <= 4, "ruined house stays within four active lights");
            Check(emergency.All(l => Mathf.Abs(l.range - 7f) < .01f && l.intensity > 0f && l.intensity <= .8f),
                "emergency lamps use range 7 and flicker below their 0.8 maximum intensity");
            Renderer[] fixtureModels = emergency.SelectMany(l => l.GetComponentsInChildren<Renderer>(true)).ToArray();
            Check(emergency.All(l => l.transform.Find("Fixture") != null &&
                    l.transform.parent != null && l.transform.parent.name == "Interior") && fixtureModels.Length > 0,
                "emergency lamps use the authored fixture model");
            Teleport(player, ruinedHouse.transform.position + new Vector3(40f, .1f, 0f));
            yield return new WaitForSeconds(.3f);
            Check(emergency.All(l => !l.enabled), "emergency lamps are disabled beyond 30 m");
            Teleport(player, ruinedHouse.transform.position + new Vector3(0f, .1f, 0f));
            yield return new WaitForSeconds(.3f);
            Check(emergency.All(l => l.enabled), "emergency lamps enable within 30 m");
            float[] initialIntensity = emergency.Select(l => l.intensity).ToArray();
            for (int sample = 0; sample < 5; sample++) yield return new WaitForSeconds(.12f);
            Check(emergency.Where((light, i) => Mathf.Abs(light.intensity - initialIntensity[i]) > .02f).Any(),
                "ruined emergency lamps visibly flicker over time");
            Teleport(player, ruinedHouse.transform.position + new Vector3(40f, .1f, 0f));
            yield return new WaitForSeconds(.3f);
            Check(fixtureModels.All(r => !r.enabled), "emergency lamp fixture models follow floor culling at distance");
            Finish();
        }

        private static int CountFloors(Transform root) => root.GetComponentsInChildren<Transform>(true).Count(t => t.name.StartsWith("Floor_", StringComparison.Ordinal));

        private static Vector2Int FindChunk(int seed, bool town)
        {
            for (int radius = 1; radius < 64; radius++)
                for (int z = -radius; z <= radius; z++)
                    for (int x = -radius; x <= radius; x++)
                    {
                        var c = new Vector2Int(x, z);
                        if ((town ? WorldLayout.IsTownChunk(c, seed) : WorldLayout.IsSuburbChunk(c, seed)) && !ProceduralLandmarks.HasLandmark(c, seed))
                            return c;
                    }
            return Vector2Int.zero;
        }

        private static void Teleport(FirstPersonController player, Vector3 position)
        {
            CharacterController controller = player.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            player.transform.position = position;
            if (controller != null) controller.enabled = true;
        }

        private void Finish()
        {
            Debug.Log(failures == 0 ? "[BuildingLightingProbe] ALL CHECKS PASSED" : $"[BuildingLightingProbe] {failures} CHECK(S) FAILED");
            Application.Quit(failures == 0 ? 0 : 1);
        }
    }
}
