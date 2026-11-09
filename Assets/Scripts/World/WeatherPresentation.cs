using System;
using SheltreSim.Audio;
using SheltreSim.Player;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SheltreSim.World
{
    /// <summary>
    /// Makes the simulated weather and clock visible: sun/moon, sky, clouds, rain, snow, fog, lightning
    /// and the underground fade. It only reads WorldAtmosphere (polling) and never changes its clock or
    /// exposure rules. Creates itself after every scene load, no scene edit, and does nothing at all
    /// unless a WorldAtmosphere exists. Every change is smoothed over a few seconds except lightning.
    /// </summary>
    public sealed class WeatherPresentation : MonoBehaviour
    {
        private const float Tau = 2.5f;           // smoothing time constant, seconds
        private const float RainRate = 2100f;     // particles per second at intensity 1
        private const float RainLife = 1.1f;
        private const float SnowRate = 150f;
        private const float SnowLife = 10f;

        private static WeatherPresentation instance;
        private static bool sceneHookInstalled;
        private static float apocalypseTarget;
        private static bool dbgOn;
        private static string dbgWeather = "Clear";
        private static float dbgIntensity, dbgHour = 12f;
        private static bool dbgSnap;
        private static string clockText = "";

        /// <summary>0..1, dusty orange-brown world after the bomb. Smoothed towards the set value.</summary>
        public static float ApocalypseAmount
        {
            get => apocalypseTarget;
            set => apocalypseTarget = float.IsNaN(value) ? 0f : Mathf.Clamp01(value);
        }

        /// <summary>For the HUD, like "14:32  Rain".</summary>
        public static string ClockText => clockText;

        public static void Flash(float seconds)
        {
            if (instance != null) instance.StartFlash(seconds);
        }

        /// <summary>Probe only: snaps instantly and ignores the simulation until cleared.</summary>
        public static void DebugOverride(string weather, float intensity, float hour)
        {
            dbgOn = true;
            dbgWeather = string.IsNullOrEmpty(weather) ? "Clear" : weather;
            dbgIntensity = Mathf.Clamp01(intensity);
            dbgHour = Mathf.Repeat(hour, 24f);
            dbgSnap = true;
        }

        public static void ClearDebugOverride() { dbgOn = false; }
        public static void DebugLightning(float atSeconds = 0f) { if (instance != null) instance.lightningT = atSeconds; }

        // Read by the probe.
        public static int ParticleCount => instance == null ? 0 : instance.RainCount + instance.SnowCount;
        public static float UndergroundNow => instance == null ? 0f : instance.und;
        public static float Overcast => instance == null ? 0f : instance.overcast;
        public int RainCount => rain != null ? rain.particleCount : 0;
        public int SnowCount => snow != null ? snow.particleCount : 0;
        public static Camera ActiveCamera => instance == null ? null : instance.cam;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
            sceneHookInstalled = false;
            apocalypseTarget = 0f;
            dbgOn = false;
            clockText = "";
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
            new GameObject("WeatherPresentation").AddComponent<WeatherPresentation>();
        }

        // ---------- state ----------
        private WorldAtmosphere atmosphere;
        private Camera cam;
        private Light sunLight;
        private Vector3 lastCamPos;
        private float nextSearch;
        private bool first = true;

        private float rainAmt, snowAmt, fogAmt, overcast, apoc, shelter, cover;
        private float und;
        private Color skyHorizon, skyZenith;
        private float windX, windY;
        private float lightningT = 99f;
        private int lastMinute = -1;
        private string lastWeather;

        private Material skyMat, cloudMat, rainMat, snowMat;
        private Mesh domeMesh;
        private Transform dome;
        private ParticleSystem rain, snow;
        private Material originalSkybox;
        private bool skyboxApplied;

        private Canvas flashCanvas;
        private Image flashImage;
        private float flashDuration, flashTime = 99f;

        private static readonly int ZenithId = Shader.PropertyToID("_Zenith");
        private static readonly int HorizonId = Shader.PropertyToID("_Horizon");
        private static readonly int GroundId = Shader.PropertyToID("_Ground");
        private static readonly int SunDirId = Shader.PropertyToID("_SunDir");
        private static readonly int SunColorId = Shader.PropertyToID("_SunColor");
        private static readonly int MoonDirId = Shader.PropertyToID("_MoonDir");
        private static readonly int ParamsId = Shader.PropertyToID("_Params");
        private static readonly int FlashId = Shader.PropertyToID("_Flash");
        private static readonly int LitId = Shader.PropertyToID("_LitColor");
        private static readonly int DarkId = Shader.PropertyToID("_DarkColor");
        private static readonly int CoverId = Shader.PropertyToID("_Cover");
        private static readonly int WindId = Shader.PropertyToID("_Wind");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            originalSkybox = RenderSettings.skybox;
            BuildSky();
        }

        private void OnEnable() { GameAudio.ThunderStruck += OnThunder; }
        private void OnDisable() { GameAudio.ThunderStruck -= OnThunder; }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
            if (skyboxApplied && RenderSettings.skybox == skyMat) RenderSettings.skybox = originalSkybox;
            if (skyMat != null) Destroy(skyMat);
            if (cloudMat != null) Destroy(cloudMat);
            if (rainMat != null) Destroy(rainMat);
            if (snowMat != null) Destroy(snowMat);
            if (domeMesh != null) Destroy(domeMesh);
        }

        private void OnThunder()
        {
            // Indoors/underground is handled by the underground scale in Apply.
            lightningT = 0f;
        }

        // ---------- construction ----------
        private void BuildSky()
        {
            Shader sky = Resources.Load<Shader>("Shaders/WeatherSky");
            Shader clouds = Resources.Load<Shader>("Shaders/WeatherClouds");
            Shader particle = Resources.Load<Shader>("Shaders/WeatherParticle");
            if (sky != null) skyMat = new Material(sky) { name = "WeatherSky (runtime)" };
            else Debug.LogWarning("[WeatherPresentation] WeatherSky shader missing; sky left as is.");

            if (clouds != null)
            {
                cloudMat = new Material(clouds) { name = "WeatherClouds (runtime)" };
                domeMesh = BuildDome();
                var go = new GameObject("CloudDome");
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = domeMesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = cloudMat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                dome = go.transform;
            }

            if (particle != null)
            {
                rainMat = new Material(particle) { name = "WeatherRain (runtime)" };
                rainMat.SetFloat("_Shape", 0f);
                snowMat = new Material(particle) { name = "WeatherSnow (runtime)" };
                snowMat.SetFloat("_Shape", 1f);
                rain = BuildParticles("Rain", rainMat, true);
                snow = BuildParticles("Snow", snowMat, false);
            }
        }

        private static Mesh BuildDome()
        {
            const int lon = 48, lat = 16;
            var verts = new Vector3[(lon + 1) * (lat + 1)];
            var tris = new int[lon * lat * 6];
            for (int y = 0; y <= lat; y++)
            {
                // From slightly below the horizon to the zenith.
                float e = Mathf.Lerp(-0.12f, Mathf.PI * 0.5f, y / (float)lat);
                for (int x = 0; x <= lon; x++)
                {
                    float a = x / (float)lon * Mathf.PI * 2f;
                    verts[y * (lon + 1) + x] = new Vector3(Mathf.Cos(a) * Mathf.Cos(e), Mathf.Sin(e), Mathf.Sin(a) * Mathf.Cos(e));
                }
            }
            int t = 0;
            for (int y = 0; y < lat; y++)
                for (int x = 0; x < lon; x++)
                {
                    int i = y * (lon + 1) + x;
                    tris[t++] = i; tris[t++] = i + lon + 1; tris[t++] = i + 1;
                    tris[t++] = i + 1; tris[t++] = i + lon + 1; tris[t++] = i + lon + 2;
                }
            var mesh = new Mesh { name = "CloudDome" };
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 4f);
            return mesh;
        }

        private ParticleSystem BuildParticles(string name, Material material, bool isRain)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // shape +Z points down
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = isRain ? 2500 : 1700;
            main.startLifetime = isRain ? RainLife : SnowLife;
            main.startSpeed = isRain ? 24f : 1.5f;
            main.startSize = isRain ? new ParticleSystem.MinMaxCurve(0.022f, 0.036f) : new ParticleSystem.MinMaxCurve(0.07f, 0.15f);
            main.gravityModifier = 0f;
            main.startColor = Color.white;

            var emission = ps.emission;
            emission.rateOverTime = 0f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = isRain ? new Vector3(26f, 26f, 0.1f) : new Vector3(28f, 28f, 0.1f);

            if (isRain)
            {
                var vel = ps.velocityOverLifetime;
                vel.enabled = true;
                vel.space = ParticleSystemSimulationSpace.World;
                vel.x = 2.5f; vel.y = 0f; vel.z = 0f;
            }
            else
            {
                var noise = ps.noise;
                noise.enabled = true;
                noise.strength = 1.1f;
                noise.frequency = 0.25f;
                noise.scrollSpeed = 0.4f;
                noise.damping = true;
                noise.quality = ParticleSystemNoiseQuality.Low;
                var vel = ps.velocityOverLifetime;
                vel.enabled = true;
                vel.space = ParticleSystemSimulationSpace.World;
                vel.x = 0.6f;
            }

            // Drops and flakes stop at roofs and ground, which is also what stops them from piling through a pause glitch.
            var col = ps.collision;
            col.enabled = true;
            col.type = ParticleSystemCollisionType.World;
            col.mode = ParticleSystemCollisionMode.Collision3D;
            col.quality = ParticleSystemCollisionQuality.Low;
            col.collidesWith = ~0;
            col.enableDynamicColliders = false;
            col.lifetimeLoss = 1f;
            col.bounce = 0f;
            col.dampen = 0f;
            col.radiusScale = 0.5f;
            col.maxCollisionShapes = 64;
            col.colliderForce = 0f;
            col.sendCollisionMessages = false;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.maxParticleSize = 1f;
            if (isRain)
            {
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.velocityScale = 0.05f;
                renderer.lengthScale = 1.6f;
                renderer.cameraVelocityScale = 0f;
            }
            else renderer.renderMode = ParticleSystemRenderMode.Billboard;

            ps.Play();
            return ps;
        }

        // ---------- flash overlay ----------
        private void StartFlash(float seconds)
        {
            if (seconds <= 0f) return;
            if (flashCanvas == null)
            {
                var go = new GameObject("WeatherFlash");
                go.transform.SetParent(transform, false);
                flashCanvas = go.AddComponent<Canvas>();
                flashCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                flashCanvas.sortingOrder = 30000;
                var imgGo = new GameObject("White");
                imgGo.transform.SetParent(go.transform, false);
                flashImage = imgGo.AddComponent<Image>();
                flashImage.color = Color.white;
                flashImage.raycastTarget = false;
                var rt = flashImage.rectTransform;
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            }
            flashDuration = seconds;
            flashTime = 0f;
            flashCanvas.gameObject.SetActive(true);
            flashImage.color = Color.white;
        }

        private void UpdateFlash()
        {
            if (flashCanvas == null || flashTime > 90f) return;
            flashTime += Time.unscaledDeltaTime;
            float t = flashTime / Mathf.Max(0.01f, flashDuration);
            if (t >= 1f)
            {
                flashTime = 99f;
                flashCanvas.gameObject.SetActive(false);
                return;
            }
            flashImage.color = new Color(1f, 1f, 1f, Mathf.Pow(1f - t, 1.4f));
        }

        // ---------- per frame ----------
        private void Update()
        {
            UpdateFlash();
            if (atmosphere == null && !dbgOn)
            {
                if (Time.unscaledTime >= nextSearch)
                {
                    nextSearch = Time.unscaledTime + 1f;
                    atmosphere = FindFirstObjectByType<WorldAtmosphere>();
                }
                if (atmosphere == null) { SetVisible(false); return; }
            }
            if (atmosphere == null && !dbgOn) return;

            if (cam == null && Time.unscaledTime >= nextSearch)
            {
                nextSearch = Time.unscaledTime + 0.5f;
                FindCamera();
            }
            if (cam != null) lastCamPos = cam.transform.position;
            else if (first && !dbgOn) return; // nothing to anchor to yet

            string weather; float intensity, hour; bool sheltered;
            if (dbgOn)
            {
                weather = dbgWeather; intensity = dbgIntensity; hour = dbgHour; sheltered = false;
            }
            else
            {
                weather = atmosphere.WeatherName; intensity = atmosphere.WeatherIntensity;
                hour = atmosphere.Hour; sheltered = atmosphere.IsSheltered;
            }
            SetVisible(true);
            UpdateClockText(hour, weather);

            bool snap = first || dbgSnap;
            dbgSnap = false;
            first = false;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            float k = snap ? 1f : 1f - Mathf.Exp(-dt / Tau);

            und = Mathf.Clamp01(WorldLayout.UndergroundAmount(lastCamPos));
            float tRain = weather == "Rain" ? intensity : 0f;
            float tSnow = weather == "Snow" ? intensity : 0f;
            float tFog = weather == "Fog" ? intensity : 0f;
            rainAmt = Mathf.Lerp(rainAmt, tRain, k);
            snowAmt = Mathf.Lerp(snowAmt, tSnow, k);
            fogAmt = Mathf.Lerp(fogAmt, tFog, k);
            float tOver = Mathf.Clamp01(tRain + tSnow * 0.9f + tFog * 0.7f);
            overcast = Mathf.Lerp(overcast, tOver, k);
            float tCover = Mathf.Clamp01(0.28f + 0.72f * Mathf.Max(tRain, tSnow) + 0.3f * tFog);
            cover = Mathf.Lerp(cover, tCover, k);
            apoc = Mathf.Lerp(apoc, apocalypseTarget, k);
            shelter = Mathf.Lerp(shelter, sheltered ? 1f : 0f, snap ? 1f : 1f - Mathf.Exp(-dt / 1.2f));

            Apply(hour);
        }

        private void SetVisible(bool on)
        {
            if (dome != null && dome.gameObject.activeSelf != on) dome.gameObject.SetActive(on);
            if (!on)
            {
                if (rain != null) { var e = rain.emission; e.rateOverTime = 0f; }
                if (snow != null) { var e = snow.emission; e.rateOverTime = 0f; }
            }
        }

        private void FindCamera()
        {
            var fps = FindFirstObjectByType<FirstPersonController>();
            if (fps != null) cam = fps.GetComponentInChildren<Camera>();
            if (cam == null) cam = Camera.main;
            if (cam != null && cam.clearFlags != CameraClearFlags.Skybox && skyMat != null) cam.clearFlags = CameraClearFlags.Skybox;
        }

        private void UpdateClockText(float hour, string weather)
        {
            int total = Mathf.FloorToInt(hour * 60f) % 1440;
            if (total == lastMinute && weather == lastWeather) return;
            // Underground you can't see the sky, so say it. Not on the first frame, not from the probe's override.
            if (lastWeather != null && weather != lastWeather && !dbgOn && atmosphere != null && Time.frameCount - atmosphere.RestoredAtFrame > 3)
                SheltreSim.UI.GameMessageBus.Post(ChangeMessage(weather));
            lastMinute = total; lastWeather = weather;
            clockText = (total / 60).ToString("00") + ":" + (total % 60).ToString("00") + "  " + weather;
        }

        private string ChangeMessage(string weather)
        {
            bool under = und > 0.5f;
            switch (weather)
            {
                case "Rain": return under ? "You can hear rain on the hatch above." : "It starts to rain.";
                case "Snow": return under ? "The air from the vents turns bitterly cold. Snow outside." : "It starts to snow.";
                case "Fog": return under ? "The vents smell of damp. Fog outside." : "Fog rolls in.";
                default: return under ? "The sound of the weather above fades." : "The weather clears.";
            }
        }

        private static Color C(float r, float g, float b) { return new Color(r, g, b, 1f); }

        private void Apply(float hour)
        {
            // ---- sun and moon ----
            float phi = hour >= 6f && hour <= 20f
                ? Mathf.PI * (hour - 6f) / 14f
                : Mathf.PI + Mathf.PI * (Mathf.Repeat(hour - 20f, 24f) / 10f);
            Vector3 sunDir = new Vector3(Mathf.Cos(phi), Mathf.Sin(phi) * 0.9f, 0.35f).normalized;
            Vector3 moonDir = new Vector3(-sunDir.x, -sunDir.y, -0.35f).normalized;
            float sinSun = sunDir.y;
            float moonUp = Mathf.Max(0f, -sinSun);
            float dayF = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.26f, 0.12f, sinSun));
            float nightW = 1f - dayF;
            float glow = Mathf.Exp(-Mathf.Pow(sinSun / 0.22f, 2f));      // dawn and dusk band
            float surface = 1f - und;

            // ---- palette ----
            float sumAmt = rainAmt + snowAmt + fogAmt;
            Color grey = sumAmt > 0.001f
                ? (C(0.40f, 0.42f, 0.45f) * rainAmt + C(0.68f, 0.70f, 0.74f) * snowAmt + C(0.68f, 0.69f, 0.69f) * fogAmt) / sumAmt
                : C(0.6f, 0.62f, 0.65f);
            grey.a = 1f;
            Color dayZen = Color.Lerp(C(0.24f, 0.45f, 0.80f), grey * 0.92f, overcast);
            Color dayHor = Color.Lerp(C(0.74f, 0.82f, 0.90f), grey * 1.04f, overcast);
            float warm = glow * (1f - 0.65f * overcast);
            dayHor = Color.Lerp(dayHor, C(1.0f, 0.52f, 0.24f), warm * 0.85f);
            dayZen = Color.Lerp(dayZen, C(0.22f, 0.27f, 0.52f), warm * 0.55f);
            Color nightZen = Color.Lerp(C(0.012f, 0.02f, 0.06f), C(0.03f, 0.033f, 0.04f), overcast);
            Color nightHor = Color.Lerp(C(0.035f, 0.05f, 0.10f), C(0.04f, 0.044f, 0.052f), overcast);
            Color zen = Color.Lerp(dayZen, nightZen, nightW);
            Color hor = Color.Lerp(dayHor, nightHor, nightW);
            // The bomb dust tints the sky, but weather must still read through it: rain and snow push it back.
            float dust = apoc * 0.85f * (1f - 0.7f * overcast);
            float apBright = Mathf.Lerp(0.30f, 1f, dayF);
            zen = Color.Lerp(zen, C(0.44f, 0.27f, 0.15f) * apBright, dust);
            hor = Color.Lerp(hor, C(0.70f, 0.45f, 0.25f) * apBright, dust);
            zen.a = hor.a = 1f;
            skyZenith = zen; skyHorizon = hor;

            // ---- lightning ----
            float flash = 0f;
            if (lightningT < 0.3f)
            {
                lightningT += Mathf.Min(Time.unscaledDeltaTime, 0.033f); // a hitch must not skip the flash
                float t = lightningT;
                flash = t < 0.04f ? t / 0.04f
                      : t < 0.08f ? 1f - (t - 0.04f) / 0.04f * 0.85f
                      : t < 0.12f ? 0.15f + (t - 0.08f) / 0.04f * 0.85f
                      : t < 0.28f ? 1f - (t - 0.12f) / 0.16f : 0f;
                flash = Mathf.Clamp01(flash);
            }
            flash *= surface;

            // ---- sky material ----
            if (skyMat != null)
            {
                if (!skyboxApplied) { RenderSettings.skybox = skyMat; skyboxApplied = true; }
                else if (RenderSettings.skybox != skyMat) RenderSettings.skybox = skyMat;
                Color sunCol = Color.Lerp(C(1.0f, 0.50f, 0.22f), C(1.0f, 0.93f, 0.80f), Mathf.SmoothStep(0f, 1f, sinSun * 2.5f));
                sunCol = Color.Lerp(sunCol, C(1.0f, 0.55f, 0.28f), apoc * 0.7f);
                skyMat.SetColor(ZenithId, zen * surface + C(.05f, .05f, .05f) * und);
                skyMat.SetColor(HorizonId, hor * surface + C(.05f, .05f, .05f) * und);
                skyMat.SetColor(GroundId, hor * 0.55f * surface);
                skyMat.SetVector(SunDirId, sunDir);
                skyMat.SetColor(SunColorId, sunCol);
                skyMat.SetVector(MoonDirId, moonDir);
                float sunVis = (1f - overcast) * (1f - apoc * 0.6f);
                float stars = nightW * nightW * (1f - overcast) * (1f - apoc);
                skyMat.SetVector(ParamsId, new Vector4(sunVis, (1f - overcast) * nightW * (1f - apoc), stars, (1f - overcast * 0.8f) * glow));
                skyMat.SetFloat(FlashId, flash * 0.7f);
            }

            // ---- directional light ----
            if (sunLight == null) FindSunLight();
            float sunI = 1.25f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.14f, sinSun))
                * Mathf.Lerp(1f, 0.30f, overcast) * Mathf.Lerp(1f, 0.55f, apoc);
            float moonI = 0.4f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.3f, moonUp))
                * Mathf.Lerp(1f, 0.45f, overcast) * (1f - apoc * 0.4f);
            if (sunLight != null)
            {
                bool sunUp = sinSun >= 0f;
                Vector3 toLight = sunUp ? sunDir : moonDir;
                sunLight.transform.rotation = Quaternion.LookRotation(-toLight);
                Color lc = sunUp
                    ? Color.Lerp(C(1.0f, 0.55f, 0.28f), C(1.0f, 0.93f, 0.80f), Mathf.SmoothStep(0f, 1f, sinSun * 2.5f))
                    : C(0.52f, 0.62f, 1.0f);
                lc = Color.Lerp(lc, C(1.0f, 0.62f, 0.34f), apoc * 0.5f);
                sunLight.color = lc;
                sunLight.intensity = (sunUp ? sunI : moonI) * surface;
                sunLight.shadows = LightShadows.Soft;
                sunLight.shadowStrength = Mathf.Lerp(1f, 0.3f, overcast) * (sunUp ? 1f : 0.7f);
                if (flash > 0f)
                {
                    sunLight.color = Color.Lerp(lc, C(0.8f, 0.85f, 1f), flash);
                    sunLight.intensity += flash * 0.7f * surface;
                }
            }

            // ---- ambient ----
            Color aSky = Color.Lerp(C(0.58f, 0.64f, 0.74f), grey * 0.86f, overcast);
            Color aEq = Color.Lerp(C(0.50f, 0.50f, 0.48f), grey * 0.74f, overcast);
            Color aGr = Color.Lerp(C(0.30f, 0.27f, 0.22f), grey * 0.42f, overcast);
            float twiScale = Mathf.Lerp(1f, 0.6f, glow);
            aSky = Color.Lerp(aSky, C(0.62f, 0.50f, 0.50f), glow * 0.4f * (1f - overcast)) * twiScale;
            aEq = Color.Lerp(aEq, C(0.60f, 0.42f, 0.30f), glow * 0.5f * (1f - overcast)) * twiScale;
            aGr *= twiScale;
            Color nSky = Color.Lerp(C(0.075f, 0.10f, 0.20f), C(0.07f, 0.078f, 0.095f), overcast);
            Color nEq = Color.Lerp(C(0.06f, 0.075f, 0.14f), C(0.055f, 0.06f, 0.075f), overcast);
            Color nGr = C(0.03f, 0.033f, 0.048f);
            aSky = Color.Lerp(aSky, nSky, nightW);
            aEq = Color.Lerp(aEq, nEq, nightW);
            aGr = Color.Lerp(aGr, nGr, nightW);
            float apAmb = Mathf.Lerp(0.38f, 0.95f, dayF);
            float apAmbMix = apoc * 0.8f * (1f - 0.6f * overcast); // weather reads through the dust
            aSky = Color.Lerp(aSky, C(0.55f, 0.38f, 0.24f) * apAmb, apAmbMix);
            aEq = Color.Lerp(aEq, C(0.48f, 0.32f, 0.20f) * apAmb, apAmbMix);
            aGr = Color.Lerp(aGr, C(0.26f, 0.17f, 0.10f) * apAmb, apAmbMix);
            Color fl = C(0.55f, 0.6f, 0.8f) * (flash * 1.1f);
            aSky += fl; aEq += fl; aGr += fl * 0.6f;
            Color bunker = C(0.115f, 0.105f, 0.095f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Color.Lerp(aSky, bunker, und);
            RenderSettings.ambientEquatorColor = Color.Lerp(aEq, bunker * 0.9f, und);
            RenderSettings.ambientGroundColor = Color.Lerp(aGr, bunker * 0.7f, und);
            RenderSettings.reflectionIntensity = Mathf.Lerp(0.15f, 1f, dayF * (1f - 0.4f * overcast)) * surface + 0.15f * und;

            // ---- fog ----
            float density = 0.0035f
                + rainAmt * (0.010f - 0.0035f)
                + snowAmt * (0.013f - 0.0035f)
                + fogAmt * (0.043f - 0.0035f);
            density *= 1f + apoc * 1.4f;
            density += apoc * 0.0035f;
            Color fogCol = hor * 0.95f + C(0.6f, 0.65f, 0.85f) * (flash * 0.6f);
            fogCol.a = 1f;
            // Linear on purpose: the scene was saved with linear fog, and Graphics "Fog Modes: Automatic" strips
            // the other modes' shader variants from a build, so exp2 silently draws no fog at all in the player.
            float visibility = 2.4f / Mathf.Max(density * surface, 0.00001f);
            RenderSettings.fog = und < 0.985f;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = visibility * 0.08f;
            RenderSettings.fogEndDistance = visibility;
            RenderSettings.fogColor = fogCol;

            // ---- clouds ----
            if (cloudMat != null && dome != null)
            {
                float wind = Time.deltaTime;
                windX = Mathf.Repeat(windX + wind * 0.012f * (1f + 2.5f * rainAmt), 500f);
                windY = Mathf.Repeat(windY + wind * 0.005f * (1f + 2.5f * rainAmt), 500f);
                Color lit = Color.Lerp(C(1f, 0.99f, 0.96f), grey * 1.1f, overcast);
                lit = Color.Lerp(lit, C(1f, 0.62f, 0.42f), glow * 0.7f * (1f - overcast * 0.6f));
                Color litNight = Color.Lerp(C(0.10f, 0.115f, 0.17f), C(0.05f, 0.055f, 0.065f), overcast);
                lit = Color.Lerp(lit, litNight, nightW);
                lit = Color.Lerp(lit, C(0.62f, 0.40f, 0.24f) * Mathf.Lerp(0.25f, 1f, dayF), dust);
                Color dark = lit * Mathf.Lerp(0.72f, 0.42f, Mathf.Clamp01(rainAmt + snowAmt * 0.6f));
                dark.a = lit.a = 1f;
                cloudMat.SetColor(LitId, lit);
                cloudMat.SetColor(DarkId, dark);
                cloudMat.SetFloat(CoverId, Mathf.Clamp01(cover + apoc * 0.35f));
                cloudMat.SetVector(WindId, new Vector4(windX, windY, 0f, 0f));
                cloudMat.SetFloat(FlashId, flash);
                cloudMat.SetFloat("_Opacity", Mathf.Lerp(0.92f, 1f, overcast) * surface);
            }

            // ---- particles ----
            float rainBright = Mathf.Lerp(0.32f, 1f, dayF) + flash * 0.8f;
            float snowBright = Mathf.Lerp(0.40f, 1f, dayF);
            float shelterScale = Mathf.Lerp(1f, 0.25f, shelter) * surface;
            DriveParticles(rain, rainMat, rainAmt * shelterScale, RainRate, C(0.70f, 0.78f, 0.90f) * rainBright, 0.72f + flash * 0.28f, 14f);
            DriveParticles(snow, snowMat, snowAmt * shelterScale, SnowRate, C(0.95f, 0.97f, 1f) * snowBright, 0.9f, 12f);
        }

        private void DriveParticles(ParticleSystem ps, Material mat, float amount, float rate, Color color, float alpha, float height)
        {
            if (ps == null) return;
            var em = ps.emission;
            em.rateOverTime = rate * amount;
            if (und > 0.98f && ps.particleCount > 0) ps.Clear();
            ps.transform.position = lastCamPos + Vector3.up * height;
            color.a = alpha * Mathf.Clamp01(amount * 4f); // leftover flakes fade as the weather ends
            if (amount < 0.005f && ps.particleCount > 0) ps.Clear();
            mat.SetColor(ColorId, color);
            if (!ps.isPlaying && Time.timeScale > 0f) ps.Play();
        }

        private void LateUpdate()
        {
            if (dome != null && cam != null)
            {
                float far = cam.farClipPlane;
                dome.position = cam.transform.position;
                float r = Mathf.Clamp(far * 0.9f, 30f, 1500f);
                dome.localScale = Vector3.one * r;
            }
        }

        private void FindSunLight()
        {
            if (RenderSettings.sun != null && RenderSettings.sun.type == LightType.Directional) { sunLight = RenderSettings.sun; return; }
            foreach (Light l in FindObjectsByType<Light>(FindObjectsInactive.Exclude))
                if (l.type == LightType.Directional) { sunLight = l; return; }
        }
    }
}
