using System;
using System.Collections;
using System.IO;
using SheltreSim.Enemies;
using SheltreSim.Player;
using SheltreSim.World;
using UnityEngine;

namespace SheltreSim.Diagnostics
{
    /// <summary>
    /// Opt-in check of the weather presentation in the built player: `--weather-probe &lt;folder&gt;` with
    /// -batchmode but without -nographics. Sets each state through the debug seam, waits real seconds for
    /// particles to fill, writes real PNGs of the player's camera and logs PASS/FAIL checks.
    /// Normal launches never create this component.
    /// </summary>
    public sealed class WeatherProbe : MonoBehaviour
    {
        private const int W = 1280, H = 720;
        private string folder;
        private int failures;
        private int maxParticles;
        private int errors;
        private Transform player;
        private Camera camera;
        private Vector3 surfacePoint;
        private float yaw = 200f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "--weather-probe");
            if (index < 0 || index + 1 >= args.Length) return;
            new GameObject("WeatherProbe").AddComponent<WeatherProbe>().folder = args[index + 1];
        }

        private void OnEnable() { Application.logMessageReceived += OnLog; }
        private void OnDisable() { Application.logMessageReceived -= OnLog; }

        private void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Exception || (type == LogType.Error && message.Contains("WeatherPresentation"))) { errors++; Debug.Log("[WeatherProbe] captured error: " + message); }
        }

        private IEnumerator Start()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Directory.CreateDirectory(folder);
            yield return null; yield return null; yield return null;

            var fps = FindFirstObjectByType<FirstPersonController>();
            player = fps.transform;
            camera = fps.GetComponentInChildren<Camera>();
            foreach (var raider in FindObjectsByType<EnemyAI>()) raider.gameObject.SetActive(false);
            Check(FindFirstObjectByType<WorldAtmosphere>() != null, "WorldAtmosphere exists");

            // A spot outside the bunker lot with open ground in front.
            Vector3 probe = new Vector3(72f, 100f, -190f);
            Physics.Raycast(probe, Vector3.down, out RaycastHit hit, 200f, ~0, QueryTriggerInteraction.Ignore);
            surfacePoint = hit.collider != null ? hit.point + Vector3.up * 0.15f : new Vector3(72f, 6.2f, -190f);
            Debug.Log($"[WeatherProbe] surface point {surfacePoint}");
            Move(surfacePoint);
            yield return new WaitForSecondsRealtime(0.5f);

            // The scene's own camera far clip decides the dome size.
            Debug.Log($"[WeatherProbe] camera far={camera.farClipPlane} clearFlags={camera.clearFlags}");

            // ---- noon clear ----
            WeatherPresentation.DebugOverride("Clear", 0f, 12f);
            yield return Settle(2.5f);
            int clearParticles = WeatherPresentation.ParticleCount;
            float noonAmbient = Luma(RenderSettings.ambientSkyColor);
            float clearFog = RenderSettings.fogEndDistance;
            float noonLum = Shot("01_noon_clear");
            Check(clearParticles == 0, $"no particles when Clear ({clearParticles})");
            Check(WeatherPresentation.ClockText.StartsWith("12:00") && WeatherPresentation.ClockText.Contains("Clear"), $"ClockText '{WeatherPresentation.ClockText}'");

            WeatherPresentation.DebugOverride("Clear", 0f, 6.5f);
            yield return Settle(1f);
            Shot("02_dawn");
            WeatherPresentation.DebugOverride("Clear", 0f, 19.6f);
            yield return Settle(1f);
            Shot("03_dusk");
            WeatherPresentation.DebugOverride("Clear", 0f, 1f);
            yield return Settle(1f);
            float nightLum = Shot("04_night_clear");
            Check(nightLum < noonLum * 0.5f, $"night luminance {nightLum:F3} < 0.5 x noon {noonLum:F3}");
            Check(nightLum > 0.02f, $"night still playable, luminance {nightLum:F3} > 0.02");

            WeatherPresentation.DebugOverride("Rain", 1f, 12f);
            yield return Settle(4f);
            int rainCount = WeatherPresentation.ParticleCount;
            float rainFogD = RenderSettings.fogEndDistance;
            float rainLum = Shot("05_noon_rain");
            Check(rainCount > 200, $"rain particles at intensity 1: {rainCount}");
            Check(rainLum < noonLum, $"rain noon darker than clear noon ({rainLum:F3} < {noonLum:F3})");

            WeatherPresentation.DebugOverride("Fog", 1f, 12f);
            yield return Settle(4f);
            float fogDensity = RenderSettings.fogEndDistance;
            Shot("06_noon_fog");
            Check(clearFog > fogDensity * 3f, $"fog visibility in Fog {fogDensity:F0} m < Clear {clearFog:F0} m / 3 (ie fog density > 3x)");
            Check(fogDensity >= 35f && fogDensity <= 70f, $"Fog visibility {fogDensity:F0} m in 40-60 m band");
            Check(WeatherPresentation.ParticleCount == 0, $"no rain left in Fog ({WeatherPresentation.ParticleCount})");
            Check(rainFogD < clearFog && rainFogD > fogDensity, $"rain visibility {rainFogD:F0} m between fog and clear");

            WeatherPresentation.DebugOverride("Snow", 1f, 12f);
            yield return Settle(10f);
            int snowCount = WeatherPresentation.ParticleCount;
            Shot("07_noon_snow");
            Check(snowCount > 200, $"snow particles at intensity 1: {snowCount}");

            WeatherPresentation.DebugOverride("Rain", 1f, 1f);
            yield return Settle(4f);
            float nightRainLum = Shot("08_night_rain");

            // Lightning, same night rain, mid flash.
            yield return null; yield return null; yield return null;
            WeatherPresentation.DebugLightning(0.03f);
            yield return null;
            float flashLum = Shot("10_lightning");
            Check(flashLum > nightRainLum * 1.5f, $"lightning frame {flashLum:F3} brighter than night rain {nightRainLum:F3}");
            yield return Settle(0.6f);

            WeatherPresentation.ApocalypseAmount = 1f;
            WeatherPresentation.DebugOverride("Clear", 0f, 12f);
            yield return Settle(3f);
            Color ap = RenderSettings.ambientSkyColor;
            float apLum = Shot("09_apocalypse_noon");
            Check(ap.r > ap.b * 1.4f, $"apocalypse ambient is orange-brown ({ap.r:F2},{ap.g:F2},{ap.b:F2})");
            Check(apLum > noonLum * 0.3f && apLum < noonLum, $"apocalypse noon readable but dimmer: {apLum:F3} vs {noonLum:F3}");
            WeatherPresentation.ApocalypseAmount = 0f;

            // ---- underground ----
            WeatherPresentation.DebugOverride("Rain", 1f, 12f);
            yield return Settle(2f);
            // The probe owns the player for this test: no controller, so nothing pulls it back up through the ground.
            fps.enabled = false;
            player.GetComponent<CharacterController>().enabled = false;
            Vector3 bunker = WorldLayout.BunkerCenter;
            // Walk down "the stairs" in 5 cm steps: the ambient must change smoothly with no pop.
            float prev = -1f, maxJump = 0f; int steps = 0;
            for (float y = WorldLayout.HomeGroundY + 0.5f; y >= 0.3f; y -= 0.05f)
            {
                player.position = new Vector3(bunker.x, y, bunker.z);
                yield return null; yield return null;
                float l = Luma(RenderSettings.ambientSkyColor);
                if (prev >= 0f) maxJump = Mathf.Max(maxJump, Mathf.Abs(l - prev));
                prev = l; steps++;
            }
            Debug.Log($"[WeatherProbe] stair walk: {steps} steps, largest ambient step {maxJump:F4}");
            Check(maxJump < 0.025f, $"ambient smooth on the stairs (largest per-step change {maxJump:F4})");
            yield return Settle(1f);
            float und = WeatherPresentation.UndergroundNow;
            float underAmbient = Luma(RenderSettings.ambientSkyColor);
            Debug.Log($"[WeatherProbe] underground amount {und:F2} at camera {camera.transform.position}, fog={RenderSettings.fog}, particles={WeatherPresentation.ParticleCount}");
            Check(und > 0.95f, $"underground amount {und:F2}");
            Check(underAmbient < noonAmbient * 0.3f, $"underground ambient {underAmbient:F3} < 0.3 x surface noon {noonAmbient:F3}");
            Check(!RenderSettings.fog, "fog off underground");
            Check(WeatherPresentation.ParticleCount == 0, $"no particles underground ({WeatherPresentation.ParticleCount})");
            Shot("11_underground", false);

            yield return CheckLiveSimulation(fps);

            Check(maxParticles < 3000, $"max alive particles {maxParticles} < 3000");
            Check(errors == 0, $"no exceptions ({errors})");
            WeatherPresentation.ClearDebugOverride();
            Debug.Log(failures == 0 ? "[WeatherProbe] ALL CHECKS PASSED" : $"[WeatherProbe] {failures} CHECK(S) FAILED");
            Application.Quit(failures == 0 ? 0 : 1);
        }

        // Every check above uses the debug override, which is how live weather shipped without ever being seen to
        // change. This one runs the real clock through the real presentation.
        private IEnumerator CheckLiveSimulation(FirstPersonController fps)
        {
            WeatherPresentation.ClearDebugOverride();
            WeatherPresentation.ApocalypseAmount = 1f;
            var atmosphere = FindFirstObjectByType<WorldAtmosphere>();
            int seed = ProceduralWorld.Active != null ? ProceduralWorld.Active.Seed : 20261108;
            int homeBiome = WorldLayout.BiomeIndexAt(WorldLayout.BunkerCenter.x, WorldLayout.BunkerCenter.z, seed);
            Debug.Log($"[WeatherProbe] home biome {homeBiome} ({WorldLayout.BiomeNames[homeBiome]})");
            for (int biome = 0; biome < 6; biome++)
            {
                var sequence = new System.Text.StringBuilder();
                int changes = 0, longest = 1, run = 1, clear = 0;
                string previous = null;
                const int periods = 48; // two hours of play
                for (int k = 0; k < periods; k++)
                {
                    string w = WorldAtmosphere.WeatherFor(k, biome, seed);
                    sequence.Append(w[0]);
                    if (w == "Clear") clear++;
                    if (previous != null && w != previous) { changes++; run = 1; }
                    else if (previous != null) { run++; longest = Mathf.Max(longest, run); }
                    previous = w;
                }
                Debug.Log($"[WeatherProbe] biome {biome} {sequence}: {changes} changes, longest run {longest}, clear {clear}/{periods}");
                Check(changes >= periods / 2, $"biome {biome} changes weather often ({changes} of {periods} periods)");
                Check(longest <= 4, $"biome {biome} never holds one weather for more than 10 minutes (longest {longest} periods)");
                Check(clear >= periods / 8 && clear <= periods * 3 / 5, $"biome {biome} has a sensible share of clear sky ({clear}/{periods})");
            }

            // Stand outside, on the real clock, in the middle of a rainy or snowy period for this biome.
            Vector3 p = player.position;
            int here = WorldLayout.BiomeIndexAt(p.x, p.z, seed);
            int wet = -1;
            for (int k = 0; k < 200 && wet < 0; k++)
            {
                string w = WorldAtmosphere.WeatherFor(k, here, seed);
                if (w == "Rain" || w == "Snow") wet = k;
            }
            if (here == 2) { Check(wet < 0, "no rain in the red desert"); yield break; }
            Check(wet >= 0, "a wet period exists here");
            fps.enabled = true;
            player.GetComponent<CharacterController>().enabled = true;
            Move(surfacePoint);
            atmosphere.RestoreTime((wet + 0.5f) * WorldAtmosphere.WeatherPeriod);
            yield return Settle(12f);
            Check(!atmosphere.IsSheltered && atmosphere.ExposureStatus.StartsWith("EXPOSED"),
                "open weather reports exposed state and names the needs drain");
            GameObject testRoof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            testRoof.name = "WeatherProbeRoof";
            testRoof.GetComponent<Renderer>().enabled = false;
            testRoof.transform.position = player.position + Vector3.up * 4f;
            testRoof.transform.localScale = new Vector3(8f, .25f, 8f);
            Physics.SyncTransforms();
            yield return new WaitForSecondsRealtime(.65f);
            Check(atmosphere.IsSheltered && atmosphere.ExposureStatus.StartsWith("SHELTERED"),
                "a roof changes the status to sheltered and blocks weather drain");
            Destroy(testRoof);
            yield return new WaitForSecondsRealtime(.65f);
            Check(!atmosphere.IsSheltered && atmosphere.ExposureStatus.StartsWith("EXPOSED"),
                "leaving the roof returns the status to exposed");
            Check(atmosphere.WeatherName == "Rain" || atmosphere.WeatherName == "Snow", $"live weather after the clock moved: {atmosphere.WeatherName}");
            Check(atmosphere.WeatherIntensity >= 0.65f, $"live intensity {atmosphere.WeatherIntensity:F2}");
            Check(WeatherPresentation.ParticleCount > 100, $"live particles from the real clock: {WeatherPresentation.ParticleCount}");
            Color ap = RenderSettings.ambientSkyColor;
            float wetLum = Shot("12_live_wet_after_bombs");
            Check(WeatherPresentation.Overcast > 0.5f, $"sky goes overcast through the bomb dust ({WeatherPresentation.Overcast:F2})");
            Check(ap.r < ap.b * 1.4f, $"ambient reads as weather, not only as dust ({ap.r:F2},{ap.g:F2},{ap.b:F2})");

            // A real change: walk the clock to the next period's fade and see the first weather end by itself.
            int clearAfter = -1;
            for (int k = wet + 1; k < wet + 20 && clearAfter < 0; k++)
                if (WorldAtmosphere.WeatherFor(k, here, seed) == "Clear") clearAfter = k;
            atmosphere.RestoreTime((clearAfter + 0.5f) * WorldAtmosphere.WeatherPeriod);
            yield return Settle(14f);
            Check(atmosphere.WeatherName == "Clear" && WeatherPresentation.ParticleCount == 0, $"it clears by itself ({atmosphere.WeatherName}, {WeatherPresentation.ParticleCount} particles)");
            float dryLum = Shot("13_live_clear_after_bombs");
            Debug.Log($"[WeatherProbe] after the bombs: wet luminance {wetLum:F3}, clear {dryLum:F3}");
            Check(Mathf.Abs(dryLum - wetLum) > 0.02f, $"the change is visible on screen ({wetLum:F3} vs {dryLum:F3})");
            WeatherPresentation.ApocalypseAmount = 0f;
        }

        private IEnumerator Settle(float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                maxParticles = Mathf.Max(maxParticles, WeatherPresentation.ParticleCount);
                yield return null;
            }
        }

        private void Move(Vector3 position)
        {
            var body = player.GetComponent<CharacterController>();
            body.enabled = false;
            player.position = position;
            body.enabled = true;
        }

        private static float Luma(Color c) { return 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b; }

        private float Shot(string name, bool surfaceView = true)
        {
            if (surfaceView)
            {
                player.rotation = Quaternion.Euler(0f, yaw, 0f);
                camera.transform.rotation = Quaternion.Euler(-7f, yaw, 0f);
            }
            var target = new RenderTexture(W, H, 24);
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(W, H, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            camera.targetTexture = null;
            RenderTexture.active = null;
            Color32[] px = image.GetPixels32();
            double sum = 0;
            for (int i = 0; i < px.Length; i += 7) sum += 0.2126 * px[i].r + 0.7152 * px[i].g + 0.0722 * px[i].b;
            float mean = (float)(sum / (px.Length / 7 + 1) / 255.0);
            File.WriteAllBytes(Path.Combine(folder, name + ".png"), image.EncodeToPNG());
            Destroy(image);
            Destroy(target);
            Debug.Log($"[WeatherProbe] {name}: mean luminance {mean:F3}, particles {WeatherPresentation.ParticleCount}, fog end {RenderSettings.fogEndDistance:F0}");
            return mean;
        }

        private void Check(bool condition, string message)
        {
            Debug.Log($"[WeatherProbe] {(condition ? "PASS" : "FAIL")}: {message}");
            if (!condition) failures++;
        }
    }
}
