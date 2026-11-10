using System.Collections.Generic;
using SheltreSim.Building;
using SheltreSim.Enemies;
using SheltreSim.Player;
using SheltreSim.Survival;
using SheltreSim.UI;
using SheltreSim.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SheltreSim.Audio
{
    /// <summary>
    /// The one place sound happens. Creates itself after every scene load (no scene edit, and the
    /// death screen's restart reloads the scene), so gameplay scripts only ever make a one-line
    /// Play call and those calls are harmless no-ops at edit time.
    ///
    /// Clips come from ArtSource/audio/gen_audio.py. Variant selection uses its own System.Random:
    /// sound must never consume from UnityEngine.Random, which world code could depend on.
    /// </summary>
    public sealed class GameAudio : MonoBehaviour
    {
        private const string MasterKey = "audio.master";
        private const string MusicKey = "audio.music";
        private const string EffectsKey = "audio.effects";
        private const int PoolSize = 24;
        private const float EnclosureRange = 14f;

        private static readonly string[] BiomeLoops = { Sfx.AmbWasteland, Sfx.AmbForest, Sfx.AmbDesert, Sfx.AmbSnow, Sfx.AmbMarsh, Sfx.AmbHighlands };

        // Random one-shots for each biome index, all far enough away to read as the world, not the player.
        private static readonly string[][] BiomeEvents =
        {
            new[] { Sfx.AmbCreak, Sfx.AmbCrow, Sfx.AmbCreak, Sfx.AmbBoom },
            new[] { Sfx.AmbBird, Sfx.AmbBird, Sfx.AmbBird, Sfx.AmbCrow },
            new[] { Sfx.AmbCrow, Sfx.AmbBoom, Sfx.AmbCreak },
            new[] { Sfx.AmbWolf, Sfx.AmbCrow },
            new[] { Sfx.AmbFrog, Sfx.AmbFrog, Sfx.AmbBird },
            new[] { Sfx.AmbBird, Sfx.AmbCrow, Sfx.AmbBoom },
        };
        private static readonly string[] BunkerEvents = { Sfx.AmbDrip, Sfx.AmbDrip, Sfx.AmbPipe };

        private static GameAudio instance;
        private static Dictionary<string, List<AudioClip>> clips;
        private static bool sceneHookInstalled;

        public static float MasterVolume { get; private set; } = 1f;
        public static float MusicVolume { get; private set; } = 0.7f;
        public static float EffectsVolume { get; private set; } = 1f;

        /// <summary>Probes measure at the default level whatever the player has set, without overwriting their setting.</summary>
        public static void DebugEffectsVolume(float value) => EffectsVolume = Mathf.Clamp01(value);

        /// <summary>Set by AudioRuntimeProbe to record what plays. Null in a normal session.</summary>
        public static List<string> PlayLog;
        public static GameAudio Instance => instance;
        /// <summary>Raised when a thunderclap plays; the weather presentation flashes the sky.</summary>
        public static event System.Action ThunderStruck;

        // Exposed for AudioRuntimeProbe, which asserts real runtime state instead of trusting code paths.
        public string CurrentAmbience => activeLoop != null && activeLoop.clip != null ? activeLoop.clip.name : null;
        public float AmbienceVolume => activeLoop != null ? activeLoop.volume : 0f;
        public float BunkerHumVolume => hum != null ? hum.volume : 0f;
        public float MuffleCutoff => activeFilter != null ? activeFilter.cutoffFrequency : 22000f;
        public bool ExploreMusicPlaying => music != null && music.isPlaying;
        public float TensionVolume => tension != null ? tension.volume : 0f;
        public float RainVolume => rain != null ? rain.volume : 0f;
        public float NightVolume => night != null ? night.volume : 0f;
        public bool IsIndoors => indoors;
        public bool IsInBunker => bunkerAmount > 0.99f;

        private readonly System.Random random = new System.Random(20261108);
        private readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();
        private readonly Dictionary<string, int> lastVariant = new Dictionary<string, int>();
        private AudioSource[] pool;
        private float[] poolStarted;

        private AudioSource loopA, loopB, activeLoop, fadingLoop, hum, music, tension;
        private AudioLowPassFilter filterA, filterB, activeFilter, fadingFilter;
        private AudioSource rain, night;
        private AudioLowPassFilter rainFilter;
        private WorldAtmosphere atmosphere;
        private float nextAtmosphereSearch;
        private float nextThunder;
        private bool isNight;

        private FirstPersonController player;
        private SurvivalStats stats;
        private AudioListener listener;
        private BuildModeController buildMode;
        private bool wasBuilding;
        private float nextRaiderScan;
        private float nextPlayerSearch;
        private float nextEnvironmentCheck;
        private bool indoors;
        private float indoorAmount;
        private float bunkerAmount;
        private int shownBiome = -1;
        private int pendingBiome = -1;
        private float pendingBiomeSince;
        private float nextWorldEvent;
        private float nextExploreMusic;
        private float lastChaseTime = float.NegativeInfinity;
        private float ignoreChasersUntil;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
            clips = null;
            sceneHookInstalled = false;
            PlayLog = null;
            RaiderAudio.ResetStatics();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (!sceneHookInstalled)
            {
                sceneHookInstalled = true;
                SceneManager.sceneLoaded += (scene, mode) => Ensure();
            }
            Ensure();
        }

        private static void Ensure()
        {
            if (instance != null) return;
            new GameObject("GameAudio").AddComponent<GameAudio>();
        }

        // ------------------------------------------------------------- public play API

        /// <summary>Non-positional sound: UI, the player's own body, and anything felt rather than located.</summary>
        public static void Play2D(string key, float volume = 1f, float pitchSpread = 0.03f, float delay = 0f)
        {
            if (instance != null) instance.Fire(key, Vector3.zero, false, volume, 0f, pitchSpread, delay, 1f);
        }

        /// <summary>Sound located in the world. Silent beyond maxDistance.</summary>
        public static void PlayAt(string key, Vector3 position, float volume = 1f, float maxDistance = 40f,
            float pitchSpread = 0.06f, float delay = 0f, float pitch = 1f)
        {
            if (instance != null) instance.Fire(key, position, true, volume, maxDistance, pitchSpread, delay, pitch);
        }

        public static void UiClick() => Play2D(Sfx.UiClick, 0.7f, 0.02f);

        /// <summary>A load replaces the world: anything still ringing (a siren, a quake) belonged to the old one.</summary>
        public static void OnGameLoaded()
        {
            if (instance != null) instance.StopWorldSounds();
            PlayerAudio.SuppressLandingBriefly();
            RaiderAudio.NoteGameLoaded();
        }

        /// <summary>For a device playing a loop on its own AudioSource. Null if the key is unknown.</summary>
        public static AudioClip GetClip(string key) { LoadClips(); return Single(key); }

        public static bool HasClip(string key)
        {
            LoadClips();
            return clips.ContainsKey(key);
        }

        public static string GatherKeyFor(string resourceId)
        {
            switch (resourceId)
            {
                case "wood": return Sfx.GatherWood;
                case "stone": return Sfx.GatherStone;
                case "iron_ore": return Sfx.GatherOre;
                case "scrap_metal": return Sfx.GatherScrap;
                case "food_berries": return Sfx.GatherLeaves;
                default: return Sfx.Rummage;
            }
        }

        // ------------------------------------------------------------- volume settings

        public static void SetMaster(float value)
        {
            MasterVolume = Mathf.Clamp01(value);
            AudioListener.volume = MasterVolume;
            PlayerPrefs.SetFloat(MasterKey, MasterVolume);
        }

        public static void SetMusic(float value)
        {
            MusicVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(MusicKey, MusicVolume);
        }

        public static void SetEffects(float value)
        {
            EffectsVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(EffectsKey, EffectsVolume);
        }

        // ------------------------------------------------------------- lifecycle

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
            LoadClips();

            MasterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MasterKey, 1f));
            MusicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicKey, 0.7f));
            EffectsVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(EffectsKey, 1f));
            AudioListener.volume = MasterVolume;

            pool = new AudioSource[PoolSize];
            poolStarted = new float[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                // Each voice has its own transform, because a positional sound is located by it.
                GameObject voice = new GameObject("Voice" + i);
                voice.transform.SetParent(transform, false);
                AudioSource source = voice.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.dopplerLevel = 0f; // The player and raiders can teleport on load.
                source.rolloffMode = AudioRolloffMode.Linear;
                pool[i] = source;
            }

            loopA = CreateLoop("AmbienceA", out filterA);
            loopB = CreateLoop("AmbienceB", out filterB);
            activeLoop = loopA;
            activeFilter = filterA;
            fadingLoop = loopB;
            fadingFilter = filterB;

            hum = CreateLoop("BunkerHum", out _);
            hum.clip = Single(Sfx.AmbBunker);
            if (hum.clip != null) hum.Play();

            music = CreateLoop("Music", out _);
            music.loop = false;
            music.clip = Single(Sfx.MusicExplore);
            tension = CreateLoop("Tension", out _);
            tension.clip = Single(Sfx.MusicTension);

            rain = CreateLoop("Rain", out rainFilter);
            rain.clip = Single(Sfx.AmbRain);
            night = CreateLoop("Night", out _);
            night.clip = Single(Sfx.AmbNight);
            AttachLimiter();

            // The bunker is a concrete room under the home plot.
            GameObject reverb = new GameObject("BunkerReverb");
            reverb.transform.SetParent(transform, false);
            reverb.transform.position = WorldLayout.BunkerCenter + Vector3.up * 1.5f;
            AudioReverbZone zone = reverb.AddComponent<AudioReverbZone>();
            zone.minDistance = 8f;
            zone.maxDistance = 12f;
            zone.reverbPreset = AudioReverbPreset.Stoneroom;

            float now = Time.unscaledTime;
            nextWorldEvent = now + 12f;
            nextExploreMusic = now + 25f;
        }

        private void OnEnable() => GameMessageBus.OnMessage += OnGameMessage;

        private void OnDisable() => GameMessageBus.OnMessage -= OnGameMessage;

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        /// <summary>
        /// Save, load and build placement report only through the message bus, and those scripts
        /// have no audio code of their own. Keep these prefixes in step with the real messages:
        /// AudioRuntimeProbe posts its own copies, so it proves this routing, not their wording.
        /// </summary>
        private static void OnGameMessage(string message)
        {
            if (message.StartsWith("Discovered ")) Play2D(Sfx.Discovery, 0.7f, 0f);
            else if (message.StartsWith("Installed ")) Play2D(Sfx.Upgrade, 0.8f, 0f);
            else if (message.StartsWith("Placed ")) Play2D(Sfx.PlacePiece, 0.85f, 0.06f);
            else if (message.StartsWith("Cannot afford") || message.StartsWith("Could not save") || message.StartsWith("Could not load")) Play2D(Sfx.UiDeny, 0.35f);
            else if (message.StartsWith("Game saved")) Play2D(Sfx.GameSaved, 0.6f, 0f);
            else if (message.StartsWith("Game loaded") || message.StartsWith("Recovered the previous saved game"))
            {
                OnGameLoaded();
                Play2D(Sfx.GameLoaded, 0.6f, 0f);
            }
        }

        /// <summary>A safety net at the very end of the mix: a pile-up gets squeezed instead of hard-clipping.</summary>
        private static void AttachLimiter()
        {
            AudioListener sceneListener = FindAnyObjectByType<AudioListener>();
            if (sceneListener != null && !sceneListener.TryGetComponent<AudioLimiter>(out _))
            {
                sceneListener.gameObject.AddComponent<AudioLimiter>();
            }
        }

        private AudioSource CreateLoop(string objectName, out AudioLowPassFilter filter)
        {
            GameObject child = new GameObject(objectName);
            child.transform.SetParent(transform, false);
            AudioSource source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.volume = 0f;
            source.bypassReverbZones = true;
            filter = child.AddComponent<AudioLowPassFilter>();
            filter.cutoffFrequency = 22000f;
            return source;
        }

        private static void LoadClips()
        {
            if (clips != null) return;
            clips = new Dictionary<string, List<AudioClip>>();
            foreach (AudioClip clip in Resources.LoadAll<AudioClip>("Audio"))
            {
                string key = KeyOf(clip.name);
                if (!clips.TryGetValue(key, out List<AudioClip> list))
                {
                    list = new List<AudioClip>();
                    clips[key] = list;
                }
                list.Add(clip);
            }
        }

        /// <summary>"foot_dirt_03" and "foot_dirt" are both the key "foot_dirt".</summary>
        private static string KeyOf(string clipName)
        {
            int last = clipName.LastIndexOf('_');
            if (last > 0 && last == clipName.Length - 3 && char.IsDigit(clipName[last + 1]) && char.IsDigit(clipName[last + 2]))
            {
                return clipName.Substring(0, last);
            }
            return clipName;
        }

        private static AudioClip Single(string key) =>
            clips.TryGetValue(key, out List<AudioClip> list) && list.Count > 0 ? list[0] : null;

        // ------------------------------------------------------------- one-shots

        private void Fire(string key, Vector3 position, bool positional, float volume, float maxDistance,
            float pitchSpread, float delay, float pitch)
        {
            if (!clips.TryGetValue(key, out List<AudioClip> variants) || variants.Count == 0)
            {
                Debug.LogWarning($"[GameAudio] No clip for '{key}'.");
                return;
            }

            // The same sound twice in one instant only gets louder and phasier, never richer.
            float now = Time.unscaledTime;
            if (delay <= 0f && lastPlayed.TryGetValue(key, out float previous) && now - previous < 0.035f) return;
            lastPlayed[key] = now;

            int index = 0;
            if (variants.Count > 1)
            {
                lastVariant.TryGetValue(key, out int last);
                index = random.Next(variants.Count - 1);
                if (index >= last) index++; // Never the same take twice running.
                lastVariant[key] = index;
            }

            int slot = FreeSlot();
            AudioSource source = pool[slot];
            poolStarted[slot] = now;
            source.Stop();
            source.clip = variants[index];
            source.volume = Mathf.Clamp01(volume * EffectsVolume);
            source.pitch = pitch * (1f + ((float)random.NextDouble() * 2f - 1f) * pitchSpread);
            source.spatialBlend = positional ? 1f : 0f;
            if (positional)
            {
                source.transform.position = position;
                source.minDistance = Mathf.Max(1f, maxDistance * 0.08f);
                source.maxDistance = Mathf.Max(source.minDistance + 0.1f, maxDistance);
            }
            if (delay > 0f) source.PlayDelayed(delay);
            else source.Play();
            PlayLog?.Add(key);
        }

        /// <summary>A free voice, or else the one that has been playing longest.</summary>
        private int FreeSlot()
        {
            int oldest = 0;
            float oldestTime = float.PositiveInfinity;
            for (int i = 0; i < PoolSize; i++)
            {
                if (!pool[i].isPlaying) return i;
                if (poolStarted[i] < oldestTime)
                {
                    oldestTime = poolStarted[i];
                    oldest = i;
                }
            }
            return oldest;
        }

        private void StopWorldSounds()
        {
            for (int i = 0; i < PoolSize; i++) pool[i].Stop();

            // Chase music belonged to the old world too. The short wait lets every raider re-read its restored state.
            lastChaseTime = float.NegativeInfinity;
            ignoreChasersUntil = Time.unscaledTime + 0.25f;

            // A raider the load just spawned must be observed before its first wind-up, or its raised arm reads as rest.
            nextRaiderScan = 0f;
        }

        // ------------------------------------------------------------- continuous layers

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            float now = Time.unscaledTime;
            FindPlayer(now);
            AttachToRaiders(now);
            UpdateBuildMode();

            if (player != null && now >= nextEnvironmentCheck)
            {
                nextEnvironmentCheck = now + 0.25f;
                SampleEnvironment(now);
            }

            UpdateAmbience(dt);
            UpdateAtmosphere(dt, now);
            UpdateMusic(dt, now);
            UpdateWorldEvents(now);
        }

        private void FindPlayer(float now)
        {
            if (player != null || now < nextPlayerSearch) return;
            nextPlayerSearch = now + 1f;
            player = FindAnyObjectByType<FirstPersonController>(FindObjectsInactive.Include);
            if (player == null) return;
            stats = player.GetComponent<SurvivalStats>();
            listener = player.GetComponentInChildren<AudioListener>(true);
            buildMode = player.GetComponentInChildren<BuildModeController>(true);
            if (!player.TryGetComponent<PlayerAudio>(out _)) player.gameObject.AddComponent<PlayerAudio>();
            if (!player.TryGetComponent<VehicleAudio>(out _)) player.gameObject.AddComponent<VehicleAudio>();
        }

        /// <summary>Raiders appear from scene load, respawn and streamed encounters; this catches all three.</summary>
        private void AttachToRaiders(float now)
        {
            if (now < nextRaiderScan) return;
            nextRaiderScan = now + 1f;
            foreach (EnemyAI enemy in FindObjectsByType<EnemyAI>())
            {
                if (!enemy.TryGetComponent<RaiderAudio>(out _)) enemy.gameObject.AddComponent<RaiderAudio>();
            }
        }

        private void UpdateBuildMode()
        {
            if (buildMode == null) return;
            bool building = buildMode.IsBuildModeActive;
            if (building != wasBuilding) Play2D(building ? Sfx.UiOpen : Sfx.UiClose, 0.6f);
            wasBuilding = building;
        }

        private void SampleEnvironment(float now)
        {
            Vector3 position = player.transform.position;
            Vector3 head = position + Vector3.up * 1.5f;
            bool inBunker = WorldLayout.UndergroundAmount(head) > 0.5f;
            indoors = inBunker || IsEnclosedAt(head);
            bunkerAmount = inBunker ? 1f : 0.3f * WorldLayout.UndergroundAmount(head);

            int biome = BiomeAt(position);
            if (biome != pendingBiome)
            {
                pendingBiome = biome;
                pendingBiomeSince = now;
            }
            // Debounced so skirting a border doesn't flap between two loops.
            if (biome != shownBiome && (shownBiome < 0 || now - pendingBiomeSince > 3f))
            {
                SwitchAmbience(biome);
            }
        }

        /// <summary>
        /// Walls on most sides, not a roof overhead: the roof slabs in this game are visual only, with
        /// no collider, while walls always have one. Steep hits only, so a hillside isn't a room.
        /// </summary>
        private static bool IsEnclosedAt(Vector3 head)
        {
            const int directions = 8;
            int walls = 0;
            for (int i = 0; i < directions; i++)
            {
                float angle = i * Mathf.PI * 2f / directions;
                Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                if (Physics.Raycast(head, direction, out RaycastHit hit, EnclosureRange, ~0, QueryTriggerInteraction.Ignore) && hit.normal.y < 0.5f)
                {
                    walls++;
                }
            }
            return walls >= 6;
        }

        private static int BiomeAt(Vector3 position)
        {
            ProceduralWorld world = ProceduralWorld.Active;
            if (world == null) return 0;
            int biome = WorldLayout.BiomeIndexAt(position.x, position.z, world.Seed);
            return biome >= 0 && biome < BiomeLoops.Length ? biome : 0;
        }

        private void SwitchAmbience(int biome)
        {
            shownBiome = biome;
            AudioClip clip = Single(BiomeLoops[biome]);
            if (clip == null) return;

            AudioSource previous = activeLoop;
            AudioLowPassFilter previousFilter = activeFilter;
            activeLoop = fadingLoop;
            activeFilter = fadingFilter;
            fadingLoop = previous;
            fadingFilter = previousFilter;

            activeLoop.clip = clip;
            activeLoop.time = Random01() * clip.length * 0.9f;
            activeLoop.Play();
        }

        private float Random01() => (float)random.NextDouble();

        private void UpdateAmbience(float dt)
        {
            bool paused = Time.timeScale <= 0f;
            indoorAmount = Mathf.MoveTowards(indoorAmount, indoors ? 1f : 0f, dt * 1.6f);

            float duck = paused ? 0.55f : 1f;
            float effects = EffectsVolume * duck;
            float wind = Mathf.Lerp(0.5f, 0.13f, indoorAmount) * effects;
            activeLoop.volume = Mathf.MoveTowards(activeLoop.volume, wind, dt * 0.25f);
            fadingLoop.volume = Mathf.MoveTowards(fadingLoop.volume, 0f, dt * 0.25f);
            if (fadingLoop.isPlaying && fadingLoop.volume <= 0f) fadingLoop.Stop();

            // Wind through a wall loses its top end, which is what makes "inside" feel inside.
            float cutoff = Mathf.Lerp(22000f, 650f, Mathf.Pow(indoorAmount, 0.55f));
            activeFilter.cutoffFrequency = cutoff;
            fadingFilter.cutoffFrequency = cutoff;

            float humTarget = 0.26f * bunkerAmount * effects;
            hum.volume = Mathf.MoveTowards(hum.volume, humTarget, dt * 0.5f);
        }

        /// <summary>Weather and time of day, read from the simulation. Presentation only: it changes nothing there.</summary>
        private void UpdateAtmosphere(float dt, float now)
        {
            if (atmosphere == null && now >= nextAtmosphereSearch)
            {
                nextAtmosphereSearch = now + 2f;
                ProceduralWorld world = ProceduralWorld.Active;
                if (world != null) atmosphere = world.GetComponent<WorldAtmosphere>();
            }

            float rainAmount = 0f;
            float nightAmount = 0f;
            if (atmosphere != null)
            {
                rainAmount = atmosphere.WeatherName == "Rain" ? atmosphere.WeatherIntensity : 0f;
                float hour = atmosphere.Hour;
                // Full dark from 21:00 to 04:30, with an hour's fade either side.
                nightAmount = Mathf.Max(Mathf.InverseLerp(20f, 21f, hour), 1f - Mathf.InverseLerp(4.5f, 5.5f, hour));
            }
            isNight = nightAmount > 0.5f;

            float duck = Time.timeScale <= 0f ? 0.55f : 1f;
            FadeLoop(rain, rainAmount * Mathf.Lerp(0.6f, 0.34f, indoorAmount) * EffectsVolume * duck, dt * 0.2f);
            rainFilter.cutoffFrequency = Mathf.Lerp(22000f, 1500f, indoorAmount); // Now it's rain on a roof.

            // Crickets don't sing in snow, or in the rain.
            bool insects = shownBiome != 3 && rainAmount < 0.3f;
            float nightTarget = (insects ? nightAmount * 0.3f : 0f) * Mathf.Lerp(1f, 0.25f, indoorAmount) * EffectsVolume * duck;
            FadeLoop(night, nightTarget, dt * 0.08f);

            if (rainAmount > 0.6f && now >= nextThunder && Time.timeScale > 0f)
            {
                bool wasWaiting = nextThunder > 0f;
                nextThunder = now + 22f + Random01() * 38f;
                if (wasWaiting)
                {
                    Play2D(Sfx.Thunder, Mathf.Lerp(0.75f, 0.45f, indoorAmount), 0.1f);
                    ThunderStruck?.Invoke();
                }
            }
        }

        private static void FadeLoop(AudioSource source, float target, float step)
        {
            if (source.clip == null) return;
            source.volume = Mathf.MoveTowards(source.volume, target, step);
            if (target > 0f && !source.isPlaying) source.Play();
            else if (target <= 0f && source.volume <= 0f && source.isPlaying) source.Stop();
        }

        private void UpdateMusic(float dt, float now)
        {
            bool dead = stats != null && (stats.IsDead || stats.health <= 0f);
            bool chased = !dead && now >= ignoreChasersUntil && RaiderAudio.ChaserCount > 0;
            if (chased) lastChaseTime = now;
            bool tense = !dead && now - lastChaseTime < 5f;

            float tensionTarget = tense ? 0.5f * MusicVolume : 0f;
            tension.volume = Mathf.MoveTowards(tension.volume, tensionTarget, dt * (tense ? 0.8f : 0.14f));
            if (tensionTarget > 0f && !tension.isPlaying && tension.clip != null) tension.Play();
            else if (tensionTarget <= 0f && tension.isPlaying && tension.volume <= 0f) tension.Stop();

            float exploreTarget = tense || dead ? 0f : 0.55f * MusicVolume;
            music.volume = Mathf.MoveTowards(music.volume, exploreTarget, dt * (dead ? 1.2f : 0.2f));

            // Silence between pieces is deliberate: constant music stops being heard at all.
            if (!music.isPlaying && !tense && !dead && music.clip != null && now >= nextExploreMusic)
            {
                music.Play();
                nextExploreMusic = now + music.clip.length + 80f + Random01() * 100f;
            }
        }

        /// <summary>An animal actually nearby gets to call, so a sound can lead the player to something real.</summary>
        private bool TryPlayWildlife()
        {
            if (Random01() > 0.5f) return false;
            Vector3 from = player.transform.position;
            WorldWildlife nearest = null;
            float best = 45f * 45f;
            foreach (WorldWildlife animal in FindObjectsByType<WorldWildlife>())
            {
                float distance = (animal.transform.position - from).sqrMagnitude;
                if (distance < best)
                {
                    best = distance;
                    nearest = animal;
                }
            }
            if (nearest == null) return false;
            string call = nearest.SpeciesName == "Deer" ? Sfx.DeerCall : nearest.SpeciesName == "Boar" ? Sfx.BoarGrunt : Sfx.AmbBird;
            if (isNight && call == Sfx.AmbBird) return false;
            PlayAt(call, nearest.transform.position, 0.55f, 60f, 0.08f);
            return true;
        }

        private void UpdateWorldEvents(float now)
        {
            if (now < nextWorldEvent || player == null || Time.timeScale <= 0f) return;
            nextWorldEvent = now + 9f + Random01() * 18f;

            if (!IsInBunker && TryPlayWildlife()) return;

            string[] options = IsInBunker ? BunkerEvents : shownBiome >= 0 ? BiomeEvents[shownBiome] : BiomeEvents[0];
            string key = options[random.Next(options.Length)];
            if (isNight && key == Sfx.AmbBird) return; // Birds are asleep.
            if (IsInBunker)
            {
                Vector3 offset = new Vector3(Random01() * 4f - 2f, 2.4f, Random01() * 4f - 2f);
                PlayAt(key, offset, key == Sfx.AmbDrip ? 0.3f : 0.2f, 14f, 0.12f);
                return;
            }

            // Being indoors in a ruin still hears the outside, just less of it.
            float angle = Random01() * Mathf.PI * 2f;
            float range = 18f + Random01() * 30f;
            Vector3 position = (listener != null ? listener.transform.position : player.transform.position)
                + new Vector3(Mathf.Cos(angle) * range, 6f + Random01() * 8f, Mathf.Sin(angle) * range);
            float loudness = key == Sfx.AmbBoom ? 0.55f : key == Sfx.AmbWolf ? 0.4f : key == Sfx.AmbBird ? 0.22f : 0.3f;
            PlayAt(key, position, loudness * (indoors ? 0.5f : 1f), 110f, 0.1f);
        }
    }
}
