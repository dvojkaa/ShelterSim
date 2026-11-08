using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using SheltreSim.Audio;
using SheltreSim.Core;
using SheltreSim.Enemies;
using SheltreSim.Interaction;
using SheltreSim.Items;
using SheltreSim.Player;
using SheltreSim.Survival;
using SheltreSim.UI;
using SheltreSim.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SheltreSim.Diagnostics
{
    /// <summary>
    /// Opt-in check of the audio system in the running player: `--audio-probe &lt;folder&gt;`. Drives the
    /// real gameplay code, asserts what plays, and writes the real final mix at the AudioListener
    /// to a WAV, silencing it afterwards so nothing reaches the speakers. Never writes a save file.
    /// Normal launches never create this component.
    /// </summary>
    public sealed class AudioRuntimeProbe : MonoBehaviour
    {
        private string folder;
        private int failures;
        private readonly List<float> captured = new List<float>();
        private readonly List<string> marks = new List<string>();
        private int channels = 2;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "--audio-probe");
            if (index < 0 || index + 1 >= args.Length) return;
            GameObject host = new GameObject("AudioRuntimeProbe");
            DontDestroyOnLoad(host); // Only this test survives a reload: it has to check the new scene.
            host.AddComponent<AudioRuntimeProbe>().folder = args[index + 1];
        }

        private FirstPersonController Player => FindFirstObjectByType<FirstPersonController>();
        private SurvivalStats Stats => Player.GetComponent<SurvivalStats>();

        private IEnumerator Start()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Directory.CreateDirectory(folder);
            GameAudio.PlayLog = new List<string>();
            // Time stops if the test player dies, so a stuck wait must fail rather than hang.
            StartCoroutine(Watchdog());
            StartCoroutine(KeepRaidersCalm());
            yield return null;
            // The audio under test lives in the ruined world. The opening has its own probe.
            GameAudio.DebugEffectsVolume(1f);
            HomeBase.Active.DebugStartSurvival();
            ProceduralWorld.Active.EnsureChunk(new Vector2Int(1, -2));
            ProceduralWorld.Active.EnsureChunk(new Vector2Int(1, -3));
            ProceduralWorld.Active.EnsureChunk(new Vector2Int(2, -2));
            SetRaidersAggressive(false);
            yield return null;

            Log($"sample rate {AudioSettings.outputSampleRate}, speaker mode {AudioSettings.speakerMode}, driver {AudioSettings.driverCapabilities}");
            channels = AudioSettings.speakerMode == AudioSpeakerMode.Mono ? 1 : 2;
            InstallTap();

            CheckCatalogue();
            yield return CheckEnvironment();
            yield return CheckFootsteps();
            yield return CheckInteractions();
            yield return CheckPlayerBody();
            yield return CheckMessageCues();
            yield return CheckUi();
            yield return CheckRaiders();
            yield return CheckAtmosphere();
            yield return CheckMusic();
            yield return CheckApocalypse();
            yield return CheckDeathAndReload();

            WriteCapture();
            Debug.Log(failures == 0 ? "[AudioRuntimeProbe] ALL CHECKS PASSED" : $"[AudioRuntimeProbe] {failures} CHECK(S) FAILED");
            Application.Quit(failures == 0 ? 0 : 1);
        }

        /// <summary>Patrols stream in during the run, and each new one must ignore the test player too.</summary>
        private IEnumerator KeepRaidersCalm()
        {
            while (true)
            {
                if (!raidersAggressive) SetRaidersAggressive(false);
                yield return null;
            }
        }

        private IEnumerator Watchdog()
        {
            yield return new WaitForSecondsRealtime(170f);
            Check(false, "Probe finished within its time limit");
            Application.Quit(1);
        }

        /// <summary>Raiders ignore the test player except during the raider check, where they must really chase.</summary>
        private readonly Dictionary<EnemyAI, float> savedDetection = new Dictionary<EnemyAI, float>();

        private bool raidersAggressive;

        private void SetRaidersAggressive(bool aggressive)
        {
            raidersAggressive = aggressive;
            foreach (EnemyAI raider in FindObjectsByType<EnemyAI>(FindObjectsInactive.Include))
            {
                if (!savedDetection.ContainsKey(raider)) savedDetection[raider] = raider.detectionRadius;
                raider.detectionRadius = aggressive ? savedDetection[raider] : 0f;
            }
        }

        /// <summary>The restart check replaces the scene's listener, so the tap goes back on each new one.</summary>
        private void InstallTap()
        {
            AudioListener listener = FindFirstObjectByType<AudioListener>();
            if (listener != null && !listener.TryGetComponent<ListenerTap>(out _))
            {
                listener.gameObject.AddComponent<ListenerTap>().sink = this;
            }
        }

        /// <summary>Runs on the audio thread, on the final mix.</summary>
        private void Receive(float[] data, int mixChannels)
        {
            lock (captured)
            {
                channels = mixChannels;
                captured.AddRange(data);
            }
            Array.Clear(data, 0, data.Length);
        }

        private sealed class ListenerTap : MonoBehaviour
        {
            public AudioRuntimeProbe sink;
            private void OnAudioFilterRead(float[] data, int mixChannels) => sink?.Receive(data, mixChannels);
        }

        private void Mark(string label)
        {
            lock (captured) marks.Add($"{captured.Count / Mathf.Max(1, channels) / (float)AudioSettings.outputSampleRate:F2}\t{label}");
        }

        // ------------------------------------------------------------------- checks

        private void CheckCatalogue()
        {
            string[] keys = typeof(Sfx).GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.IsLiteral).Select(f => (string)f.GetRawConstantValue()).ToArray();
            string[] missing = keys.Where(k => !GameAudio.HasClip(k)).ToArray();
            Check(keys.Length > 40 && missing.Length == 0, $"All {keys.Length} sound keys resolve to imported clips. Missing: {string.Join(",", missing)}");
            AudioListener[] listeners = FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
            Check(listeners.Length == 1 && listeners[0].enabled, $"Exactly one enabled AudioListener ({listeners.Length})");
            Check(listeners.Length == 1 && listeners[0].TryGetComponent<AudioLimiter>(out _), "The limiter sits on the listener");
            Check(GameAudio.Instance != null, "GameAudio created itself with no scene wiring");
            AudioClip[] imported = Resources.LoadAll<AudioClip>("Audio");
            AudioClip musicClip = imported.First(c => c.name == Sfx.MusicExplore);
            AudioClip stepClip = imported.First(c => c.name.StartsWith(Sfx.FootDirt));
            Log($"import: {imported.Length} clips, music {musicClip.loadType}, step {stepClip.loadType}");
            Check(musicClip.loadType == AudioClipLoadType.Streaming, "Import settings reached the build: music streams");
            Check(stepClip.loadType == AudioClipLoadType.DecompressOnLoad, "Import settings reached the build: footsteps are decoded up front");
            AudioSource delayed = gameObject.AddComponent<AudioSource>();
            delayed.clip = stepClip;
            delayed.volume = 0f;
            delayed.PlayDelayed(5f);
            bool delayedCountsAsPlaying = delayed.isPlaying;
            delayed.Stop();
            Destroy(delayed);
            Check(delayedCountsAsPlaying, "A delayed sound reads as playing, so the voice pool can't steal it while it waits");
        }

        private IEnumerator CheckEnvironment()
        {
            Mark("environment: bunker");
            Move(new Vector3(72f, 0.2f, -217.5f));
            yield return new WaitForSeconds(3f);
            GameAudio audio = GameAudio.Instance;
            Log($"bunker: ambience={audio.CurrentAmbience} volume={audio.AmbienceVolume:F2} hum={audio.BunkerHumVolume:F2} cutoff={audio.MuffleCutoff:F0} indoors={audio.IsIndoors}");
            Check(!string.IsNullOrEmpty(audio.CurrentAmbience), "An ambience loop is playing");
            Check(audio.IsIndoors && audio.IsInBunker, "Bunker reads as indoors");
            Check(audio.BunkerHumVolume > 0.2f, "Bunker hum is audible inside");
            Check(audio.MuffleCutoff < 2000f, "Wind is muffled inside");

            Mark("environment: outdoors");
            Move(new Vector3(60f, 6.2f, -242f));
            yield return new WaitForSeconds(3f);
            Log($"outside: ambience={audio.CurrentAmbience} volume={audio.AmbienceVolume:F2} hum={audio.BunkerHumVolume:F2} cutoff={audio.MuffleCutoff:F0} indoors={audio.IsIndoors}");
            Check(!audio.IsIndoors, "Open ground reads as outdoors");
            Check(audio.MuffleCutoff > 15000f && audio.AmbienceVolume > 0.3f, "Wind is open and full outside");
            Check(audio.BunkerHumVolume < 0.1f, "Bunker hum fades away outside");

            Mark("environment: inside a ruin");
            Transform ruin = ProceduralWorld.Active.EnsureChunk(new Vector2Int(1, -2)).GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name.StartsWith("Building_"));
            Check(ruin != null, "Ruin exists for the indoor check");
            if (ruin != null)
            {
                Move(ruin.position + Vector3.up * 0.3f);
                yield return new WaitForSeconds(2.5f);
                Log($"ruin: volume={audio.AmbienceVolume:F2} hum={audio.BunkerHumVolume:F2} cutoff={audio.MuffleCutoff:F0} indoors={audio.IsIndoors}");
                Check(audio.IsIndoors && audio.MuffleCutoff < 2000f, "A roofed ruin muffles the wind");
                Check(audio.BunkerHumVolume < 0.1f, "A ruin has no bunker machinery hum");
            }
        }

        private IEnumerator CheckFootsteps()
        {
            FirstPersonController player = Player;
            CharacterController body = player.GetComponent<CharacterController>();
            player.enabled = false; // FPC's own Move would replace the velocity this test is producing.

            Mark("footsteps: outdoor walk");
            CountDuringWalk(body, new Vector3(50f, 6.2f, -242f), Vector3.right * 4f, 3f, out IEnumerator walk);
            yield return walk;
            int outdoor = Played(Sfx.FootDirt);
            Log($"outdoor walk: {outdoor} dirt steps, peak speed {lastWalkSpeed:F2}, log: {string.Join(" ", GameAudio.PlayLog)}");
            Check(outdoor >= 4 && outdoor <= 10, $"Walking 12 m outdoors gives a sensible step count of dirt ({outdoor})");

            Mark("footsteps: bunker walk");
            CountDuringWalk(body, new Vector3(68f, 0.2f, -219f), new Vector3(0f, 0f, 2.5f), 1.8f, out walk);
            yield return walk;
            int concrete = Played(Sfx.FootConcrete);
            Check(concrete >= 1, $"Walking on the bunker floor gives concrete ({concrete})");

            Mark("jump and land");
            GameAudio.PlayLog.Clear();
            Move(new Vector3(60f, 6.2f, -242f));
            yield return new WaitForSeconds(1f);
            float vertical = 6f;
            for (float t = 0f; t < 1.4f; t += Time.deltaTime)
            {
                vertical = body.isGrounded && t > 0.2f ? -2f : vertical - 18f * Time.deltaTime;
                body.Move(Vector3.up * vertical * Time.deltaTime);
                yield return null;
            }
            Check(Played(Sfx.Jump) >= 1, "A jump makes a sound");
            Check(Played(Sfx.FootDirt) + Played(Sfx.Land) >= 1, "Landing makes a sound");
            player.enabled = true;
        }

        private float lastWalkSpeed;

        private bool CountDuringWalk(CharacterController body, Vector3 start, Vector3 velocity, float seconds, out IEnumerator routine)
        {
            routine = Walk(body, start, velocity, seconds);
            return true;
        }

        private IEnumerator Walk(CharacterController body, Vector3 start, Vector3 velocity, float seconds)
        {
            Move(start);
            yield return new WaitForSeconds(0.8f); // Past the first-contact suppression, and settled.
            GameAudio.PlayLog.Clear();
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                body.Move((velocity + Vector3.down * 2f) * Time.deltaTime);
                lastWalkSpeed = Mathf.Max(lastWalkSpeed, new Vector2(body.velocity.x, body.velocity.z).magnitude);
                yield return null;
            }
        }

        private IEnumerator CheckInteractions()
        {
            GameObject actor = Player.gameObject;
            Mark("gathering");
            foreach (var pair in new[] { ("wood", Sfx.GatherWood), ("stone", Sfx.GatherStone), ("iron_ore", Sfx.GatherOre), ("scrap_metal", Sfx.GatherScrap), ("food_berries", Sfx.GatherLeaves) })
            {
                ResourceNode node = FindObjectsByType<ResourceNode>(FindObjectsSortMode.None).FirstOrDefault(n => n.resourceType != null && n.resourceType.id == pair.Item1 && n.remainingCharges > 0);
                Check(node != null, $"A {pair.Item1} node exists");
                if (node == null) continue;
                Move(node.transform.position + Vector3.back * 2f + Vector3.up * 0.2f);
                yield return new WaitForSeconds(0.2f);
                GameAudio.PlayLog.Clear();
                node.Interact(actor);
                Check(Played(pair.Item2) == 1, $"Gathering {pair.Item1} plays {pair.Item2}");
                yield return new WaitForSeconds(0.75f);
            }

            Mark("crate");
            LootContainer crate = FindObjectsByType<LootContainer>(FindObjectsSortMode.None).FirstOrDefault(c => !c.Searched);
            Check(crate != null, "A crate exists");
            if (crate != null)
            {
                GameAudio.PlayLog.Clear();
                crate.Interact(actor);
                Check(Played(Sfx.CrateOpen) == 1 && Played(Sfx.LootFound) + Played(Sfx.LootEmpty) == 1, "Searching a crate plays open and result");
                yield return new WaitForSeconds(1.1f);
            }

            Mark("door");
            InteractableDoor door = FindFirstObjectByType<InteractableDoor>();
            bool startedOpen = door.IsOpen;
            Move(door.PassageCenter + door.PassageNormal * 4f + Vector3.up * 0.2f);
            yield return new WaitForSeconds(0.2f);
            GameAudio.PlayLog.Clear();
            door.Interact(actor);
            Check(Played(door.IsOpen ? Sfx.DoorOpen : Sfx.DoorClose) == 1, "Door plays its open or close sound");
            yield return new WaitForSeconds(1.2f);
            GameAudio.PlayLog.Clear();
            door.RestoreState(false);
            Check(GameAudio.PlayLog.Count == 0, "Restoring door state on load is silent");
            yield return new WaitForSeconds(0.1f);
            door.ForceOpen();
            Check(Played(Sfx.DoorBash) == 1, "A raider forcing the door plays the bash");
            yield return new WaitForSeconds(1f);
            door.RestoreState(startedOpen);

            GameAudio.PlayLog.Clear();
            ResourceNode restoring = FindObjectsByType<ResourceNode>(FindObjectsSortMode.None).First();
            restoring.RestoreState(restoring.remainingCharges, restoring.IsDepleted);
            Check(GameAudio.PlayLog.Count == 0, "Restoring a node on load is silent");
        }

        private IEnumerator CheckPlayerBody()
        {
            SurvivalStats stats = Stats;
            Move(new Vector3(60f, 6.2f, -242f));
            yield return new WaitForSeconds(0.2f);

            Mark("player hurt");
            GameAudio.PlayLog.Clear();
            stats.TakeDamage(10f);
            yield return null;
            yield return null;
            Check(Played(Sfx.PlayerHurt) == 1 && Played(Sfx.HitFlesh) == 1, "Being hit plays impact and voice");
            yield return new WaitForSeconds(0.7f);

            Mark("eat, drink, heal");
            stats.hunger = stats.maxHunger * 0.5f;
            stats.thirst = stats.maxThirst * 0.5f;
            yield return null;
            yield return null;
            GameAudio.PlayLog.Clear();
            stats.Eat(10f);
            yield return null;
            yield return null;
            Check(Played(Sfx.Eat) == 1, "Eating plays");
            yield return new WaitForSeconds(0.9f);
            stats.Drink(10f);
            yield return null;
            yield return null;
            Check(Played(Sfx.Drink) == 1, "Drinking plays");
            yield return new WaitForSeconds(0.9f);
            stats.Heal(5f);
            yield return null;
            yield return null;
            Check(Played(Sfx.Heal) == 1, "Healing plays");
            yield return new WaitForSeconds(1f);

            Mark("low health heartbeat");
            GameAudio.PlayLog.Clear();
            stats.health = stats.maxHealth * 0.1f;
            yield return new WaitForSeconds(2.5f);
            Check(Played(Sfx.Heartbeat) >= 2, "Low health gives a repeating heartbeat");
            stats.health = stats.maxHealth;

            Mark("low vitals");
            yield return null;
            GameAudio.PlayLog.Clear();
            float hunger = stats.hunger;
            stats.hunger = stats.maxHunger * 0.1f;
            yield return new WaitForSeconds(0.3f);
            Check(Played(Sfx.WarnVital) == 1, "Hunger crossing low warns once");
            yield return new WaitForSeconds(0.6f);
            Check(Played(Sfx.WarnVital) == 1, "Low hunger doesn't nag every frame");
            stats.hunger = hunger;
            yield return null;
        }

        private IEnumerator CheckMessageCues()
        {
            Mark("message cues");
            foreach (var pair in new[] { ("Placed Wall.", Sfx.PlacePiece), ("Cannot afford 'Wall'.", Sfx.UiDeny), ("Game saved.", Sfx.GameSaved), ("Game loaded.", Sfx.GameLoaded), ("Discovered Test Site. Search for parts.", Sfx.Discovery), ("Installed Test: benefit.", Sfx.Upgrade) })
            {
                GameAudio.PlayLog.Clear();
                GameMessageBus.Post(pair.Item1);
                Check(Played(pair.Item2) == 1, $"\"{pair.Item1}\" cues {pair.Item2}");
                yield return new WaitForSeconds(0.6f);
            }
        }

        private IEnumerator CheckUi()
        {
            Mark("pause menu");
            PauseMenu pause = FindFirstObjectByType<PauseMenu>(FindObjectsInactive.Include);
            GameAudio.PlayLog.Clear();
            MonoBehaviour owner = this;
            Check(ModalUI.TryOpen(owner, () => ModalUI.Close(owner)), "Modal opens");
            Check(Played(Sfx.UiOpen) == 1, "Opening a modal plays");
            yield return WaitUnscaled(0.3f);

            Button down = pause.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name == "EffectsDown");
            Button up = pause.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name == "EffectsUp");
            Check(down != null && up != null, "Pause menu has volume controls");
            if (down != null && up != null)
            {
                float before = GameAudio.EffectsVolume;
                GameAudio.PlayLog.Clear();
                down.onClick.Invoke();
                Check(Mathf.Abs(GameAudio.EffectsVolume - (before - 0.1f)) < 0.001f, $"Effects - lowers the volume ({before:F2} to {GameAudio.EffectsVolume:F2})");
                Check(Played(Sfx.UiClick) == 1, "Button clicks play");
                up.onClick.Invoke();
                Check(Mathf.Abs(GameAudio.EffectsVolume - before) < 0.001f, "Effects + restores the volume");
                GameAudio.SetEffects(before);
                RectTransform card = pause.transform.Find("PauseCanvas/PausePanel/Card") as RectTransform;
                RectTransform row = down.transform as RectTransform;
                Check(card != null && row.anchoredPosition.y - row.sizeDelta.y >= -card.sizeDelta.y, "Volume row fits inside the pause card");
            }

            GameAudio.PlayLog.Clear();
            ModalUI.Close(owner);
            Check(Played(Sfx.UiClose) == 1, "Closing a modal plays");
            yield return new WaitForSeconds(0.3f);
        }

        private IEnumerator CheckRaiders()
        {
            Mark("raiders");
            SetRaidersAggressive(false); // Also covers any raider streamed in since the start.
            EnemyAI[] raiders = FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
            Check(raiders.Length > 0 && raiders.All(r => r.GetComponent<RaiderAudio>() != null), $"All {raiders.Length} raiders have audio attached");
            EnemyAI raider = raiders.OrderBy(r => r.name).First();

            // Patrols walk without having noticed anyone, and standing beside one isn't a fight.
            int chasersBefore = RaiderAudio.ChaserCount;
            CharacterController raiderBody = raider.GetComponent<CharacterController>();
            Move(raider.transform.position + Vector3.back * 2f + Vector3.up * 0.1f);
            GameAudio.PlayLog.Clear();
            for (float t = 0f; t < 1f; t += Time.deltaTime)
            {
                raiderBody.Move(Vector3.right * 1.1f * Time.deltaTime);
                yield return null;
            }
            Check(RaiderAudio.ChaserCount == chasersBefore && Played(Sfx.RaiderAlert) == 0, $"An unaware raider walking beside the player isn't a chaser ({chasersBefore} to {RaiderAudio.ChaserCount})");

            GameAudio.PlayLog.Clear();
            raider.RestoreState(raider.transform.position, raider.transform.rotation, raider.health * 0.5f);
            GameMessageBus.Post("Game loaded.");
            yield return null;
            yield return null;
            Check(Played(Sfx.RaiderHurt) == 0, "A load lowering a raider's health isn't a hit");
            yield return new WaitForSeconds(0.7f); // Past the load's quiet window.

            SetRaidersAggressive(true);
            Move(raider.transform.position + Vector3.back * 8f + Vector3.up * 0.1f);
            yield return null;
            GameAudio.PlayLog.Clear();
            raider.TakeDamage(1f);
            yield return null;
            yield return null;
            Check(Played(Sfx.RaiderHurt) == 1, "A hit raider cries out");
            Check(Played(Sfx.RaiderAlert) == 0, "and doesn't also shout over itself");

            float peakTension = 0f;
            int peakChasers = 0;
            for (float t = 0f; t < 4f; t += Time.deltaTime)
            {
                if (raider == null) break;
                peakTension = Mathf.Max(peakTension, GameAudio.Instance.TensionVolume);
                peakChasers = Mathf.Max(peakChasers, RaiderAudio.ChaserCount);
                yield return null;
            }
            Log($"chase: peak chasers {peakChasers}, peak tension volume {peakTension:F2}, log: {string.Join(" ", GameAudio.PlayLog.Distinct())}");
            Check(peakChasers >= 1, "A walking raider counts as a chaser");
            Check(peakTension > 0.1f, "Tension music comes in during a chase");
            Check(GameAudio.PlayLog.Any(k => k.StartsWith("foot_")), "A walking raider has footsteps");
            Check(Played(Sfx.RaiderSwing) >= 1, "A raider winding up is audible");

            Mark("raider death");
            GameAudio.PlayLog.Clear();
            Move(raider.transform.position + Vector3.back * 10f + Vector3.up * 0.1f);
            raider.TakeDamage(100000f);
            yield return null;
            yield return null;
            yield return null;
            Check(Played(Sfx.RaiderDeath) == 1, "A killed raider has a death cry");
            yield return new WaitForSeconds(1.2f);

            // Quiet the area, so the player survives for the rest of the checks and tension can fall.
            SetRaidersAggressive(false);
            Move(new Vector3(72f, 0.2f, -217.5f));
            FindFirstObjectByType<InteractableDoor>().RestoreState(false);
            Stats.RestoreVitals(Stats.maxHunger, Stats.maxThirst, Stats.maxHealth);
        }

        private IEnumerator CheckAtmosphere()
        {
            Mark("weather and night");
            ProceduralWorld world = ProceduralWorld.Active;
            Check(world != null, "Procedural world present");
            if (world == null) yield break;
            WorldAtmosphere atmosphere = world.GetComponent<WorldAtmosphere>();
            if (atmosphere == null) atmosphere = world.gameObject.AddComponent<WorldAtmosphere>();
            Move(new Vector3(60f, 6.2f, -242f));
            yield return WaitUnscaled(2.2f); // GameAudio looks for the simulation every 2 s.

            bool foundRain = false;
            for (int bucket = 0; bucket < 80 && !foundRain; bucket++)
            {
                atmosphere.RestoreTime(bucket * 180f + 90f);
                yield return null;
                yield return null;
                foundRain = atmosphere.WeatherName == "Rain" && atmosphere.WeatherIntensity > 0.9f;
            }
            Check(foundRain, "The simulation can produce rain here");
            if (foundRain)
            {
                yield return new WaitForSeconds(3.5f);
                Log($"rain: volume {GameAudio.Instance.RainVolume:F2}, hour {atmosphere.Hour:F1}");
                Check(GameAudio.Instance.RainVolume > 0.3f, "Rain is audible while it rains");
            }

            bool foundClearNight = false;
            for (int day = 0; day < 80 && !foundClearNight; day++)
            {
                atmosphere.RestoreTime(day * 1200f + 750f); // 23:00: the clock starts at 08:00, 1200 s per day.
                yield return null;
                yield return null;
                foundClearNight = atmosphere.WeatherName == "Clear" && (atmosphere.Hour > 22f || atmosphere.Hour < 3f);
            }
            Check(foundClearNight, "The simulation can produce a clear night");
            if (foundClearNight)
            {
                yield return new WaitForSeconds(3.5f);
                Log($"night: crickets {GameAudio.Instance.NightVolume:F2}, rain {GameAudio.Instance.RainVolume:F2}, hour {atmosphere.Hour:F1}");
                Check(GameAudio.Instance.NightVolume > 0.15f, "Crickets sing on a clear night");
                Check(GameAudio.Instance.RainVolume < 0.25f, "Rain fades out when it stops");
            }

            for (int x = 300; x < 6000; x += 16)
            {
                for (int z = 300; z < 3000; z += 16)
                {
                    if (WorldLayout.WaterDepthAt(x, z, world.Seed) > 0.2f)
                    {
                        Check(SurfaceAudio.FootstepAt(new Vector3(x, -500f, z)) == Sfx.FootWater, $"Shallow water at {x},{z} gives splashing footsteps");
                        yield break;
                    }
                }
            }
            Check(false, "Found some water to test footsteps on");
        }

        private IEnumerator CheckMusic()
        {
            Mark("waiting for explore music");
            float waited = 0f;
            GameAudio audio = GameAudio.Instance;
            while (!audio.ExploreMusicPlaying && waited < 45f)
            {
                waited += Time.deltaTime;
                yield return null;
            }
            Log($"explore music after {Time.unscaledTime:F0} s of play, chasers {RaiderAudio.ChaserCount}, tension {audio.TensionVolume:F2}");
            Check(audio.ExploreMusicPlaying, "Exploration music starts on its own");
            yield return new WaitForSeconds(3f);
        }

        private IEnumerator CheckApocalypse()
        {
            Mark("apocalypse");
            GameStateManager state = FindFirstObjectByType<GameStateManager>();
            state.RestorePhase(GamePhase.Building);
            state.warningSeconds = 2f;
            GameAudio.PlayLog.Clear();
            state.TriggerApocalypse();
            Check(Played(Sfx.Siren) == 1, "The siren starts the warning");
            yield return new WaitForSeconds(2.3f);
            Check(Played(Sfx.Quake) == 1, "The quake lands when the ground shakes");
            yield return new WaitForSeconds(3f);
        }

        private IEnumerator CheckDeathAndReload()
        {
            Mark("death");
            GameAudio.PlayLog.Clear();
            SurvivalStats stats = Stats;
            stats.TakeDamage(1000000f);
            yield return null;
            yield return null;
            Check(Played(Sfx.PlayerDeath) == 1 && Played(Sfx.StingerDeath) == 1, "Death plays the voice and the stinger");
            yield return WaitUnscaled(4f);
            Check(GameAudio.Instance.TensionVolume < 0.05f, "Chase music is gone on the death screen");

            Mark("reload after death");
            GameAudio previous = GameAudio.Instance;
            ModalUI.DismissCurrent();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            yield return null;
            yield return null;
            InstallTap();
            yield return WaitUnscaled(2f);
            Check(GameAudio.Instance != null && GameAudio.Instance != previous, "Restart recreates audio in the new scene");
            Check(FindObjectsByType<GameAudio>(FindObjectsSortMode.None).Length == 1, "Exactly one GameAudio after restart");
            Check(GameAudio.Instance != null && !string.IsNullOrEmpty(GameAudio.Instance.CurrentAmbience), "Ambience runs after restart");
            Check(RaiderAudio.ChaserCount == 0, "No stale chasers survive a restart");
        }

        // ------------------------------------------------------------------- helpers

        /// <summary>WaitForSecondsRealtime would use the wall clock, which outruns the capture clock.</summary>
        private static IEnumerator WaitUnscaled(float seconds)
        {
            float end = Time.unscaledTime + seconds;
            while (Time.unscaledTime < end) yield return null;
        }

        private int Played(string key) => GameAudio.PlayLog.Count(k => k == key);

        private void Move(Vector3 position)
        {
            FirstPersonController player = Player;
            CharacterController body = player.GetComponent<CharacterController>();
            body.enabled = false;
            player.transform.position = position;
            body.enabled = true;
        }

        private void WriteCapture()
        {
            float[] snapshot;
            lock (captured) snapshot = captured.ToArray();
            if (snapshot.Length == 0)
            {
                Check(false, "Captured some audio");
                return;
            }
            string path = Path.Combine(folder, "audio_probe_mix.wav");
            using (BinaryWriter writer = new BinaryWriter(File.Create(path)))
            {
                int rate = AudioSettings.outputSampleRate;
                int bytes = snapshot.Length * 2;
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + bytes);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)channels);
                writer.Write(rate);
                writer.Write(rate * channels * 2);
                writer.Write((short)(channels * 2));
                writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                writer.Write(bytes);
                foreach (float sample in snapshot) writer.Write((short)Mathf.Clamp(sample * 32767f, -32768f, 32767f));
            }
            File.WriteAllLines(Path.Combine(folder, "audio_probe_marks.txt"), marks);
            float peak = snapshot.Max(Mathf.Abs);
            Log($"captured {snapshot.Length / channels / (float)AudioSettings.outputSampleRate:F1} s, peak {peak:F3}");
            Check(peak > 0.05f, "The real mix is not silent");
            Check(peak <= 1.001f, $"The limiter keeps the real mix in range (peak {peak:F3})");
        }

        private static void Log(string message) => Debug.Log("[AudioRuntimeProbe] " + message);

        private void Check(bool condition, string message)
        {
            Debug.Log($"[AudioRuntimeProbe] {(condition ? "PASS" : "FAIL")}: {message}");
            if (!condition) failures++;
        }
    }
}
