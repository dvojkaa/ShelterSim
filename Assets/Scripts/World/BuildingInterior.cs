using SheltreSim.Player;
using SheltreSim.Items;
using System.Collections.Generic;
using UnityEngine;

namespace SheltreSim.World
{
    /// <summary>
    /// Limits interior rendering to the player's current storey and its neighbours. Geometry and
    /// furniture colliders remain live so doors, navigation, projectiles, and save state keep working.
    /// </summary>
    public sealed class BuildingInterior : MonoBehaviour
    {
        private const float StoreyHeight = 3f;
        private const float GroundFloorRevealDistance = 25f;
        private const float LampPlayerDistance = 30f;
        private const float NightStart = 20f;
        private const float NightEnd = 6f;

        /// <summary>The building the player is inside right now (any storey), or null. For ambience.</summary>
        public static BuildingInterior PlayerIn { get; private set; }
        private static readonly List<BuildingInterior> all = new List<BuildingInterior>();
        public static IReadOnlyList<BuildingInterior> All => all;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry()
        {
            all.Clear();
            PlayerIn = null;
        }

        private void OnEnable()
        {
            if (!all.Contains(this)) all.Add(this);
        }

        private void OnDisable()
        {
            all.Remove(this);
            if (PlayerIn == this) PlayerIn = null;
        }

        private float halfWidth, halfDepth;
        private Transform[] floorRoots = System.Array.Empty<Transform>();
        private Renderer[][] floorRenderers = System.Array.Empty<Renderer[]>();
        private bool[] floorVisible = System.Array.Empty<bool>();
        private Transform player;
        private float nextUpdate;
        private static WorldAtmosphere sharedAtmosphere;
        private Light[] buildingLights = System.Array.Empty<Light>();
        private float[] buildingLightBaseIntensity = System.Array.Empty<float>();
        private float[] buildingLightFlickerPhase = System.Array.Empty<float>();
        private bool ruinedLighting;

        /// <summary>Called when any searchable container in this generated building changes state.</summary>
        public void RefreshClearanceState()
        {
            if (!Application.isPlaying || !isActiveAndEnabled) return;
            LootContainer[] containers = GetComponentsInChildren<LootContainer>(true);
            if (containers.Length == 0) return;
            for (int i = 0; i < containers.Length; i++)
                if (containers[i] == null || !containers[i].Searched) return;

            ProceduralWorld world = ProceduralWorld.Active;
            if (world == null) return;
            Vector3 position = transform.position;
            int halfMeterX = Mathf.RoundToInt(position.x * 2f);
            int halfMeterZ = Mathf.RoundToInt(position.z * 2f);
            string id = $"building:{world.Seed}:{name}:{halfMeterX}:{halfMeterZ}";
            BuildingClearanceTracker.MarkCleared(id, world.Seed, position);
        }

        private void OnDestroy()
        {
            if (PlayerIn == this) PlayerIn = null;
        }

        public void Configure(float width, float depth, int storeys)
        {
            halfWidth = Mathf.Max(0f, width * .5f);
            halfDepth = Mathf.Max(0f, depth * .5f);
            int count = Mathf.Max(1, storeys);
            floorRoots = new Transform[count];
            floorRenderers = new Renderer[count][];
            floorVisible = new bool[count];
            for (int floor = 0; floor < count; floor++)
            {
                Transform group = transform.Find("Floor_" + floor + "/Interior");
                floorRoots[floor] = group;
                floorVisible[floor] = true;
            }
            ConfigureLighting(count);
            // Include fixture models in the same culling groups as the floor they light.
            for (int floor = 0; floor < count; floor++)
                floorRenderers[floor] = floorRoots[floor] != null
                    ? floorRoots[floor].GetComponentsInChildren<Renderer>(true)
                    : System.Array.Empty<Renderer>();
        }

        private void ConfigureLighting(int storeys)
        {
            // These are generated structures only. The generation pass already records whether this
            // chunk is the pre-bomb or ruined version; keep the light setup self-contained here.
            bool generatedHouse = gameObject.name.StartsWith("House_", System.StringComparison.Ordinal);
            bool generatedBuilding = gameObject.name.StartsWith("Building_", System.StringComparison.Ordinal);
            if (!generatedHouse && !generatedBuilding) return;
            ruinedLighting = ProceduralContent.BuildingRuinedPass;

            var lights = new System.Collections.Generic.List<Light>();
            if (ruinedLighting)
            {
                // One emergency fixture for each pair of floors. A generated block tops out at six
                // storeys, so this also keeps each building below the four-light block budget.
                for (int floor = 0; floor < storeys; floor += 2)
                    lights.Add(CreateLight("EmergencyLamp_" + floor, new Vector3(0f, floor * StoreyHeight + 2.35f, 0f),
                        new Color(1f, .68f, .34f), 7f, .8f, floor));
            }
            else if (generatedHouse)
            {
                // HouseRooms places the living room at the front-left and kitchen behind the
                // dividing wall on the right. Keep fixtures in those two rooms.
                lights.Add(CreateLight("WarmLamp_Living", new Vector3(-3.9f, 2.55f, -3.8f),
                    new Color(1f, .72f, .42f), 8f, 1.05f, 0));
                lights.Add(CreateLight("WarmLamp_Kitchen", new Vector3(1.25f, 2.55f, -3.5f),
                    new Color(1f, .76f, .48f), 8f, 1.05f, 0));
            }
            buildingLights = lights.ToArray();
            buildingLightBaseIntensity = new float[buildingLights.Length];
            buildingLightFlickerPhase = new float[buildingLights.Length];
            for (int i = 0; i < buildingLights.Length; i++)
            {
                buildingLightBaseIntensity[i] = buildingLights[i].intensity;
                float floor = ruinedLighting ? i * 2f : i;
                buildingLightFlickerPhase[i] = Mathf.Repeat(
                    Mathf.Abs(transform.position.x * .013f + transform.position.z * .017f + floor * 2.31f),
                    Mathf.PI * 2f);
            }
        }

        private Light CreateLight(string fixtureName, Vector3 localPosition, Color color, float range, float intensity, int floor)
        {
            var fixture = new GameObject(fixtureName);
            Transform parent = transform.Find("Floor_" + floor + "/Interior");
            fixture.transform.SetParent(parent != null ? parent : transform, false);
            fixture.transform.localPosition = localPosition;
            GameObject model = Resources.Load<GameObject>("Props/bunker_lamp");
            if (model != null)
            {
                GameObject visual = Instantiate(model, fixture.transform, false);
                visual.name = "Fixture";
            }
            Light light = fixture.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            light.enabled = false;
            return light;
        }

        private void LateUpdate()
        {
            if (Time.unscaledTime < nextUpdate) return;
            nextUpdate = Time.unscaledTime + .2f;
            if (player == null)
            {
                FirstPersonController controller = FindFirstObjectByType<FirstPersonController>();
                if (controller == null) return;
                player = controller.transform;
            }
            UpdateBuildingLights();
            if (floorRenderers == null || floorRenderers.Length == 0) return;

            Vector3 local = transform.InverseTransformPoint(player.position);
            bool inside = Mathf.Abs(local.x) <= halfWidth + 2f && Mathf.Abs(local.z) <= halfDepth + 2f &&
                          local.y >= -.5f && local.y <= floorRenderers.Length * StoreyHeight + 2f;
            if (inside) PlayerIn = this;
            else if (PlayerIn == this) PlayerIn = null;
            int currentFloor = Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(0f, local.y - .1f) / StoreyHeight), 0, floorRenderers.Length - 1);
            bool groundVisible = new Vector2(local.x, local.z).sqrMagnitude <= GroundFloorRevealDistance * GroundFloorRevealDistance;

            for (int floor = 0; floor < floorRenderers.Length; floor++)
            {
                bool visible = (inside && Mathf.Abs(floor - currentFloor) <= 1) || (floor == 0 && groundVisible);
                if (visible == floorVisible[floor]) continue;
                floorVisible[floor] = visible;
                Renderer[] renderers = floorRenderers[floor];
                for (int i = 0; i < renderers.Length; i++)
                    if (renderers[i] != null) renderers[i].enabled = visible;
            }
        }

        private void UpdateBuildingLights()
        {
            if (buildingLights == null || buildingLights.Length == 0) return;
            if (player == null) return;
            bool nearby = (player.position - transform.position).sqrMagnitude <= LampPlayerDistance * LampPlayerDistance;
            if (sharedAtmosphere == null) sharedAtmosphere = FindFirstObjectByType<WorldAtmosphere>();
            float hour = sharedAtmosphere != null ? sharedAtmosphere.Hour : 12f;
            bool night = hour < NightEnd || hour >= NightStart;
            bool shouldEnable = nearby && (ruinedLighting || night);
            for (int i = 0; i < buildingLights.Length; i++)
            {
                Light light = buildingLights[i];
                if (light == null) continue;
                if (light.enabled != shouldEnable) light.enabled = shouldEnable;
                if (ruinedLighting && shouldEnable)
                {
                    // Stable, position-seeded flicker: emergency lights pulse independently but
                    // repeat when the same streamed building is regenerated.
                    float pulse = .82f + .18f * (.5f + .5f * Mathf.Sin(Time.time * 23f + buildingLightFlickerPhase[i]));
                    if (Mathf.Sin(Time.time * 2.7f + buildingLightFlickerPhase[i] * 1.7f) > .985f) pulse = .12f;
                    light.intensity = buildingLightBaseIntensity[i] * pulse;
                }
                else if (light.intensity != buildingLightBaseIntensity[i])
                    light.intensity = buildingLightBaseIntensity[i];
            }
        }
    }
}
