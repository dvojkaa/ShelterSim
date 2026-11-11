using SheltreSim.Audio;
using SheltreSim.Crafting;
using SheltreSim.Items;
using SheltreSim.Saving;
using SheltreSim.Survival;
using SheltreSim.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SheltreSim.World
{
    /// <summary>
    /// Rads (0 to 100) build up while the player stands in a RadiationZones hot zone in the ruined world, and decay
    /// slowly anywhere else. Rads lower the health cap: health above maxHealth x (1 - rads/100) drains down to it.
    /// A carried Gas Mask halves the intake; Rad-Away removes 40 rads. The bunker and the stairs are always safe,
    /// and nothing happens before the bombs. A Geiger counter clicks faster the hotter the ground, and a small
    /// passive readout shows the dose. Creates itself after every scene load, no scene edit. Saved as "radiation".
    /// </summary>
    public sealed class RadiationSystem : MonoBehaviour, ISaveableSystem
    {
        public const string AntiRadId = "anti_rad", GasMaskId = "gas_mask";
        public const float MaxRads = 100f;
        public const float DecayPerSecond = 0.2f;          // 100 rads fade in a little over 8 minutes
        public const float HealthDrainPerSecond = 2f;      // how fast health sinks to the lowered cap
        public const float AntiRadAmount = 40f;
        public const float MaskFactor = 0.5f;

        private static RadiationSystem instance;
        private static bool hooked;

        private float rads;
        private float intensity, intake;
        private SurvivalStats stats;
        private Inventory inventory;
        private float nextLookup, nextClick, lastWarnedAt = -999f;
        private int warnedLevel;
        private bool wasInZone;
        private Canvas canvas;
        private GameObject readout;
        private Text readoutLabel;
        private int shownRads = -1;
        private bool shownRising;

        public string SaveKey => "radiation";

        /// <summary>Current dose, 0..100.</summary>
        public static float Rads => instance != null ? instance.rads : 0f;
        /// <summary>Rads per second of the ground where the player stands, before the mask.</summary>
        public static float Intensity => instance != null ? instance.intensity : 0f;
        /// <summary>Rads per second the player is actually taking in.</summary>
        public static float Intake => instance != null ? instance.intake : 0f;
        /// <summary>The highest health the player can have at the current dose.</summary>
        public static float HealthCap(float maxHealth) => maxHealth * (1f - Mathf.Clamp(Rads, 0f, MaxRads) / MaxRads);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
            hooked = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (!hooked)
            {
                hooked = true;
                SceneManager.sceneLoaded += (scene, mode) => Ensure();
            }
            Ensure();
        }

        private static void Ensure()
        {
            if (instance == null) new GameObject("RadiationSystem").AddComponent<RadiationSystem>();
        }

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            SaveableSystems.Register(this);
            BuildReadout();
        }

        private void OnEnable() => GameMessageBus.OnMessage += OnMessage;
        private void OnDisable() => GameMessageBus.OnMessage -= OnMessage;

        private void OnDestroy()
        {
            if (instance != this) return;
            SaveableSystems.Unregister(this);
            instance = null;
        }

        /// <summary>Removes rads (Rad-Away, or anything else that treats radiation). Returns the amount removed.</summary>
        public static float Treat(float amount)
        {
            if (instance == null || amount <= 0f || float.IsNaN(amount)) return 0f;
            float before = instance.rads;
            instance.rads = Mathf.Max(0f, instance.rads - amount);
            instance.warnedLevel = Mathf.Min(instance.warnedLevel, Level(instance.rads));
            return before - instance.rads;
        }

        /// <summary>Probe only: sets the dose directly.</summary>
        public static void DebugSetRads(float value)
        {
            if (instance == null) return;
            instance.rads = Mathf.Clamp(value, 0f, MaxRads);
            instance.warnedLevel = Level(instance.rads);
        }

        // InventoryUI reports every use through the message bus as "Used <name>: ...". Rad-Away has its effect here, so
        // no other system needs to know about radiation. It is a Medicine item with a small heal, so InventoryUI lets
        // the player use it whenever health is below full, which the lowered cap guarantees once rads have done harm.
        private void OnMessage(string message)
        {
            if (message == null || !message.StartsWith("Used " + ItemCatalog.NameOf(AntiRadId) + ":")) return;
            float removed = Treat(AntiRadAmount);
            GameMessageBus.Post(removed > 0f
                ? $"Radiation down by {removed:0} rads, now {rads:0}."
                : "No radiation to flush out.");
        }

        private void Update()
        {
            if (Time.timeScale <= 0f) return;
            float dt = Time.deltaTime;
            FindPlayer();
            if (stats == null || stats.IsDead)
            {
                intensity = intake = 0f;
                UpdateReadout();
                return;
            }

            Vector3 position = stats.transform.position;
            ProceduralWorld world = ProceduralWorld.Active;
            bool exposed = world != null && world.Ruined && WorldLayout.UndergroundAmount(position) < 0.5f;
            intensity = exposed ? RadiationZones.IntensityAt(position.x, position.z, world.Seed) : 0f;
            bool masked = inventory != null && inventory.GetCount(GasMaskId) > 0;
            intake = intensity * (masked ? MaskFactor : 1f);

            if (intake > 0f) rads = Mathf.Min(MaxRads, rads + intake * dt);
            else rads = Mathf.Max(0f, rads - DecayPerSecond * dt);

            // Health above the lowered cap sinks to it. SurvivalStats resolves death on its own Update.
            float cap = HealthCap(stats.maxHealth);
            if (stats.health > cap) stats.health = Mathf.Max(cap, stats.health - HealthDrainPerSecond * dt);

            Announce(masked);
            Click(dt);
            UpdateReadout();
        }

        private void FindPlayer()
        {
            if (stats != null || Time.unscaledTime < nextLookup) return;
            nextLookup = Time.unscaledTime + 1f;
            stats = FindFirstObjectByType<SurvivalStats>();
            // No ?? here: a missing Unity component can be a fake null that ?? treats as real.
            inventory = stats != null ? stats.GetComponentInParent<Inventory>() : null;
            if (inventory == null && stats != null) inventory = stats.GetComponentInChildren<Inventory>();
        }

        private static int Level(float value) => Mathf.Clamp(Mathf.FloorToInt(value / 25f), 0, 4);

        private void Announce(bool masked)
        {
            bool inZone = intensity > 0f;
            if (inZone && !wasInZone && Time.time - lastWarnedAt > 20f)
            {
                lastWarnedAt = Time.time;
                GameMessageBus.Post(masked
                    ? "The Geiger counter starts clicking. Your gas mask filters some of the fallout."
                    : "The Geiger counter starts clicking. This ground is radioactive: a gas mask would help.");
            }
            wasInZone = inZone;

            int level = Level(rads);
            if (level > warnedLevel)
                GameMessageBus.Post($"Radiation sickness: {rads:0} rads. Your health can't rise above {HealthCap(stats.maxHealth):0}. Rad-Away flushes it out.");
            warnedLevel = level;
        }

        private void Click(float dt)
        {
            if (intensity <= 0f) { nextClick = 0f; return; }
            float rate = Mathf.Lerp(1.5f, 22f, Mathf.Clamp01(intensity / RadiationZones.TownPeak)); // clicks per second
            nextClick -= dt;
            if (nextClick > 0f) return;
            GameAudio.Play2D(Sfx.GeigerClick, 0.45f, 0.08f);
            // A Geiger counter is a Poisson process: random gaps sound right, an even beat sounds like a clock.
            nextClick = -Mathf.Log(Mathf.Max(0.0001f, Random.value)) / rate;
        }

        // ------------------------------------------------------------- readout

        private void BuildReadout()
        {
            Vector2 topLeft = new Vector2(0f, 1f);
            canvas = GameUI.CreateCanvas(transform, "RadiationHUD", 3, false);
            Image back = GameUI.Panel(canvas.transform, "Rad", topLeft, topLeft, new Vector2(334f, -24f), new Vector2(118f, 38f), GameUI.PanelColor);
            readout = back.gameObject;
            readoutLabel = GameUI.Label(back.transform, "Value", string.Empty, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(112f, 34f), 21, GameUI.Warning, TextAnchor.MiddleCenter, true);
            readout.SetActive(false);
        }

        private void UpdateReadout()
        {
            if (readout == null) return;
            bool visible = stats != null && !ModalUI.IsOpen && (rads >= 0.5f || intensity > 0f);
            if (readout.activeSelf != visible) readout.SetActive(visible);
            if (!visible) return;
            int value = Mathf.FloorToInt(rads);
            bool rising = intake > 0f;
            if (value == shownRads && rising == shownRising) return;
            shownRads = value;
            shownRising = rising;
            readoutLabel.text = rising ? $"RAD {value} +" : $"RAD {value}";
            readoutLabel.color = rising ? GameUI.Danger : GameUI.Warning;
        }

        // ------------------------------------------------------------- saving

        [System.Serializable]
        private sealed class State { public float rads; }

        public string CaptureState() => JsonUtility.ToJson(new State { rads = rads });

        public void RestoreState(string state)
        {
            float value = 0f;
            if (!string.IsNullOrEmpty(state))
            {
                State parsed = JsonUtility.FromJson<State>(state);
                if (parsed != null && !float.IsNaN(parsed.rads) && !float.IsInfinity(parsed.rads)) value = parsed.rads;
            }
            rads = Mathf.Clamp(value, 0f, MaxRads);
            warnedLevel = Level(rads);
            wasInZone = false;
            nextClick = 0f;
            shownRads = -1;
        }
    }
}
