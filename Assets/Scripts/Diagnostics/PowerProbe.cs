using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using SheltreSim.Audio;
using SheltreSim.Building;
using SheltreSim.Items;
using SheltreSim.Player;
using SheltreSim.Power;
using SheltreSim.Storage;
using SheltreSim.UI;
using SheltreSim.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace SheltreSim.Diagnostics
{
    /// <summary>
    /// `--power-probe`: builds real devices at runtime far from the map and runs electricity, heat, fuel, automation and saving
    /// through the real grid. Advance is the same code Update calls, stepped by hand so results are exact. Every scenario has its own
    /// spot, more than one heat cell (200 m) from the others. Saves nothing and touches no PlayerPrefs.
    /// </summary>
    public sealed class PowerProbe : MonoBehaviour
    {
        private const string Tag = "[PowerProbe]";

        private int failures;
        private int exceptions;
        private int site;
        private GameObject player;
        private Inventory inventory;
        private readonly List<GameObject> spawned = new List<GameObject>();
        private readonly List<TestCrate> crates = new List<TestCrate>();
        private readonly List<TestConsumer> loads = new List<TestConsumer>();
        private readonly List<string> messages = new List<string>();

        /// <summary>A crate without a model, so this probe doesn't depend on the real one.</summary>
        private sealed class TestCrate : IItemStore
        {
            private readonly Dictionary<string, int> items = new Dictionary<string, int>();
            public bool RefuseTakes;
            public Vector3 StorePosition { get; set; }
            public IReadOnlyDictionary<string, int> Contents => items;
            public int GetCount(string id) => items.TryGetValue(id, out int count) ? count : 0;
            public void Add(string id, int amount) => items[id] = GetCount(id) + amount;
            public bool TryTake(string id, int amount)
            {
                if (RefuseTakes || GetCount(id) < amount) return false;
                items[id] = GetCount(id) - amount;
                return true;
            }
        }

        /// <summary>Stands in for a device from another system, such as a turret.</summary>
        private sealed class TestConsumer : IPowerConsumer
        {
            public Vector3 PowerPosition { get; set; }
            public float PowerDemand { get; set; }
            public int PowerPriority { get; set; }
            public float HeatOutput => 0f;
            public float CoolingOutput => 0f;
            public string PowerName => "Test Load";
            public bool Powered;
            public void SetPowered(bool powered) => Powered = powered;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--power-probe") < 0) return;
            new GameObject("PowerProbe").AddComponent<PowerProbe>();
        }

        private IEnumerator Start()
        {
            StartCoroutine(Watchdog());
            Application.logMessageReceived += OnLog;
            GameMessageBus.OnMessage += OnMessage;
            yield return null;
            yield return null;
            FirstPersonController controller = FindFirstObjectByType<FirstPersonController>();
            Check(controller != null, "The player exists");
            if (controller == null) { Finish(); yield break; }
            player = controller.gameObject;
            inventory = player.GetComponent<Inventory>();
            Check(inventory != null && !ModalUI.IsOpen && Time.timeScale > 0f, "The game is running with no menu open");

            yield return Run(GeneratorNeedsFuel());
            yield return Run(FuelBurnsOnlyInGameTime());
            yield return Run(TwoGeneratorsDoubleExactly());
            yield return Run(RunningOutStopsConsumers());
            yield return Run(PriorityShedding());
            yield return Run(BatteryBehaviour());
            yield return Run(HeatAndOverheat());
            yield return Run(HeatCellsNote());
            yield return Run(SolarOutput());
            yield return Run(ChargerAccumulator());
            yield return Run(PumpAndCrates());
            yield return Run(FabricatorJobs());
            yield return Run(RepairStation());
            yield return Run(FarmAndIrrigator());
            yield return Run(SaveRoundTrips());
            yield return Run(GhostLifecycle());
            yield return Run(ConsolePanel());
            yield return Run(EscapeClosesPanel());
            Clean();
            yield return new WaitForSecondsRealtime(0.3f);
            Check(exceptions == 0, $"No exception was logged during the probe ({exceptions})");
            Finish();
        }

        private void Finish()
        {
            Time.timeScale = 1f;
            Debug.Log(failures == 0 ? $"{Tag} ALL CHECKS PASSED" : $"{Tag} {failures} CHECKS FAILED");
            Application.Quit(failures == 0 ? 0 : 1);
        }

        private void OnLog(string text, string stack, LogType type)
        {
            if (type == LogType.Exception) { exceptions++; Debug.Log($"{Tag} exception seen: {text}"); }
        }

        private void OnMessage(string message) => messages.Add(message);

        private IEnumerator Run(IEnumerator routine)
        {
            while (true)
            {
                object current;
                try
                {
                    if (!routine.MoveNext()) break;
                    current = routine.Current;
                }
                catch (Exception error)
                {
                    Check(false, "Exception: " + error);
                    break;
                }
                yield return current;
            }
            Time.timeScale = 1f;
            Clean();
        }

        private IEnumerator Watchdog()
        {
            yield return new WaitForSecondsRealtime(170f);
            Debug.LogError($"{Tag} FAIL: timed out");
            Application.Quit(1);
        }

        private void Check(bool ok, string text)
        {
            if (!ok) failures++;
            if (ok) Debug.Log($"{Tag} PASS: {text}");
            else Debug.LogError($"{Tag} FAIL: {text}");
        }

        private static bool Near(float a, float b, float tolerance = 0.01f) => Mathf.Abs(a - b) <= tolerance;

        // ------------------------------------------------------------- helpers

        private Vector3 NextSite()
        {
            site++;
            return new Vector3(4000f + site * 1000f, 100f, 4000f);
        }

        private T Make<T>(Vector3 position) where T : Component
        {
            GameObject item = new GameObject(typeof(T).Name);
            item.transform.position = position;
            spawned.Add(item);
            return item.AddComponent<T>();
        }

        /// <summary>The way SaveManager does it: state goes into an inactive instance before Awake and OnEnable.</summary>
        private T MakeRestored<T>(Vector3 position, string state) where T : PowerDevice
        {
            GameObject item = new GameObject(typeof(T).Name);
            item.SetActive(false);
            item.transform.position = position;
            spawned.Add(item);
            T device = item.AddComponent<T>();
            device.RestorePieceState(state);
            item.SetActive(true);
            return device;
        }

        private GeneratorPiece MakeGenerator(Vector3 position, float fuel)
        {
            GeneratorPiece generator = MakeRestored<GeneratorPiece>(position, $"{{\"fuel\":{fuel.ToString(System.Globalization.CultureInfo.InvariantCulture)},\"running\":true}}");
            return generator;
        }

        private TestCrate MakeCrate(Vector3 position, params object[] contents)
        {
            var crate = new TestCrate { StorePosition = position };
            for (int i = 0; i + 1 < contents.Length; i += 2) crate.Add((string)contents[i], (int)contents[i + 1]);
            StorageRegistry.Register(crate);
            crates.Add(crate);
            return crate;
        }

        private TestConsumer MakeLoad(Vector3 position, float demand, int priority)
        {
            var load = new TestConsumer { PowerPosition = position, PowerDemand = demand, PowerPriority = priority };
            PowerGrid.Register(load);
            loads.Add(load);
            return load;
        }

        private void Clean()
        {
            foreach (GameObject item in spawned)
            {
                if (item == null) continue;
                item.SetActive(false);
                Destroy(item);
            }
            spawned.Clear();
            foreach (TestCrate crate in crates) StorageRegistry.Unregister(crate);
            crates.Clear();
            foreach (TestConsumer load in loads) PowerGrid.Unregister(load);
            loads.Clear();
        }

        /// <summary>Grid ticks and device updates in alternation, inside one frame, in fixed steps.</summary>
        private static void Step(float total, float step, params PowerDevice[] devices)
        {
            int count = Mathf.RoundToInt(total / step);
            for (int i = 0; i < count; i++)
            {
                PowerGrid.DebugTick(step);
                foreach (PowerDevice device in devices) device.Advance(step);
            }
        }

        private static void Tick(float seconds = 0.25f) => PowerGrid.DebugTick(seconds);

        private static bool Click(Component device, string buttonName)
        {
            Button button = device.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name == buttonName);
            if (button == null || !button.interactable) return false;
            button.onClick.Invoke();
            return true;
        }

        private static string PanelText(Component device) =>
            string.Join("\n", device.GetComponentsInChildren<Text>(false).Select(t => t.text));

        private int MessagesWith(string text) => messages.Count(m => m.Contains(text));

        // ------------------------------------------------------------- scenarios

        private IEnumerator GeneratorNeedsFuel()
        {
            Vector3 here = NextSite();
            GeneratorPiece generator = Make<GeneratorPiece>(here);
            Tick();
            Check(generator.FuelUnits == 0f && !generator.IsRunning && generator.PowerOutput == 0f, "A new generator is empty and off");
            Check(!generator.TryStart() && !generator.IsRunning, "It refuses to start without fuel");
            Check(PowerGrid.NetworkAt(here).Supply == 0f, "It supplies nothing without fuel");

            inventory.SetCount(GeneratorPiece.FuelId, 0);
            generator.Interact(player);
            yield return null;
            Check(generator.IsPanelOpen && ModalUI.IsOpen && Time.timeScale == 0f, "E opens its panel and pauses the game");
            Check(!Click(generator, "StartStop") && !generator.IsRunning, "START can't be pressed with an empty tank");
            Check(!Click(generator, "AddFuel") && generator.FuelUnits == 0f, "ADD FUEL can't be pressed with no fuel carried");

            inventory.SetCount(GeneratorPiece.FuelId, 14);
            generator.ClosePanel();
            generator.Interact(player);
            Check(Click(generator, "AddFuel") && generator.FuelUnits == 1f && inventory.GetCount(GeneratorPiece.FuelId) == 13, "ADD FUEL moves exactly one unit from the player to the tank");
            while (Click(generator, "AddFuel")) { }
            Check(generator.FuelUnits == generator.fuelCapacity && inventory.GetCount(GeneratorPiece.FuelId) == 4, $"The tank stops at its cap and keeps the rest with the player (tank {generator.FuelUnits}, carried {inventory.GetCount(GeneratorPiece.FuelId)})");
            Check(Click(generator, "StartStop") && generator.IsRunning, "START starts it");
            Check(PanelText(generator).Contains("20:00"), "The panel shows the running time left");
            Click(generator, "Close");
            yield return null;
            Check(!generator.IsPanelOpen && !ModalUI.IsOpen && Time.timeScale == 1f, "CLOSE resumes the game");
            Tick();
            Check(Near(PowerGrid.NetworkAt(here).Supply, generator.outputKw), "Running, it supplies its full output");
            generator.Stop();
            Tick();
            Check(!generator.IsRunning && PowerGrid.NetworkAt(here).Supply == 0f, "Stopped, it supplies nothing");
            inventory.SetCount(GeneratorPiece.FuelId, 0);
        }

        private IEnumerator FuelBurnsOnlyInGameTime()
        {
            Vector3 here = NextSite();
            GeneratorPiece generator = MakeGenerator(here, 3f);
            yield return null;
            Time.timeScale = 0f;
            yield return null;
            float frozen = generator.FuelUnits;
            yield return new WaitForSecondsRealtime(1.2f);
            Check(generator.FuelUnits == frozen, $"Paused for 1.2 s of real time, the tank doesn't change ({frozen} to {generator.FuelUnits})");
            Time.timeScale = 1f;
            float gameStart = Time.time, fuelStart = generator.FuelUnits;
            yield return new WaitForSeconds(1f);
            float burned = fuelStart - generator.FuelUnits, expected = (Time.time - gameStart) / generator.secondsPerFuelUnit;
            Check(burned > 0f && Near(burned, expected, expected * 0.25f), $"In game time it burns at the set rate ({burned:F5} vs {expected:F5})");

            GeneratorPiece stopped = MakeGenerator(here + Vector3.right, 3f);
            stopped.Stop();
            yield return new WaitForSeconds(0.3f);
            Check(stopped.FuelUnits == 3f, "A stopped generator burns nothing");
        }

        private IEnumerator TwoGeneratorsDoubleExactly()
        {
            Vector3 here = NextSite();
            GeneratorPiece a = MakeGenerator(here, 5f);
            GeneratorPiece b = MakeGenerator(here + Vector3.right * 2f, 2f);
            Tick();
            PowerNetwork network = PowerGrid.NetworkAt(here);
            Check(network.Sources.Count == 2 && Near(network.Supply, 2f * a.outputKw), $"Two generators side by side give exactly double ({network.Supply} kW)");
            Check(PowerGrid.Networks.Count(n => n.Sources.Contains(a) || n.Sources.Contains(b)) == 1, "They are one network, each counted once");
            for (int i = 0; i < 4; i++) Tick();
            Check(Near(PowerGrid.NetworkAt(here).Supply, 2f * a.outputKw), "Still double after more ticks");
            Step(60f, 0.25f, a);
            Check(Near(a.FuelUnits, 4.5f) && b.FuelUnits == 2f, $"Each tank burns on its own ({a.FuelUnits}, {b.FuelUnits})");
            Step(60f, 0.25f, a, b);
            Check(Near(a.FuelUnits, 4f) && Near(b.FuelUnits, 1.5f), $"Sharing a network doesn't change the burn rate ({a.FuelUnits}, {b.FuelUnits})");
            b.Stop();
            Tick();
            Check(PowerGrid.NetworkAt(here).Sources.Count(s => s.PowerOutput > 0f) == 1, "Stopping one removes its share");
            yield break;
        }

        private IEnumerator RunningOutStopsConsumers()
        {
            Vector3 here = NextSite();
            GeneratorPiece generator = MakeGenerator(here, 0.1f); // 12 s
            CellChargerPiece charger = Make<CellChargerPiece>(here + Vector3.right);
            int before = MessagesWith("ran out of fuel");
            Step(10f, 0.25f, generator, charger);
            float progress = charger.Progress;
            Check(progress > 9f && charger.PowerRunning, $"With fuel, the charger works ({progress:F2} s)");
            Check(generator.IsRunning && MessagesWith("ran out of fuel") == before, "No warning while there's fuel");
            Step(3f, 0.25f, generator, charger);
            Check(!generator.IsRunning && generator.FuelUnits == 0f && PowerGrid.NetworkAt(here).Supply == 0f, "Running out stops the generator and the power");
            Check(MessagesWith("ran out of fuel") == before + 1, "Running out posts one message");
            float atStop = charger.Progress;
            Step(30f, 0.25f, generator, charger);
            Check(charger.Progress == atStop && !charger.PowerRunning, "The consumer stops advancing");
            Check(MessagesWith("ran out of fuel") == before + 1, "The message doesn't repeat");
            Check(charger.PowerStatus.Contains("no power"), "The charger says why: " + charger.PowerStatus);
            yield break;
        }

        private IEnumerator PriorityShedding()
        {
            Vector3 here = NextSite();
            GeneratorPiece generator = MakeGenerator(here, 5f);
            TestCrate crate = MakeCrate(here + Vector3.forward, "wood", 10);
            FabricatorPiece fabricator = Make<FabricatorPiece>(here + Vector3.right); // Registered before the defence, on purpose.
            Check(fabricator.SelectRecipe("planks"), "The fabricator accepts a Hand recipe");
            TestConsumer defence = MakeLoad(here + Vector3.left, 5f, 0);
            Step(2f, 0.25f, fabricator);
            Check(defence.Powered && !fabricator.PowerRunning, "On a shortage the defence stays on and the fabricator stops");
            Check(fabricator.JobRecipeId == null && crate.GetCount("wood") == 10, "A stopped fabricator takes nothing");
            Check(fabricator.PowerStatus.Contains("not enough power"), "It says why: " + fabricator.PowerStatus);
            defence.PowerDemand = 3f;
            Step(2f, 0.25f, fabricator);
            Check(defence.Powered && fabricator.PowerRunning && fabricator.JobRecipeId == "planks" && crate.GetCount("wood") == 8, "With enough power both run, and the job takes its inputs once");
            float progress = fabricator.JobProgress;
            Check(progress > 0f, "The job advances");
            defence.PowerDemand = 5f;
            Step(5f, 0.25f, fabricator);
            Check(fabricator.JobProgress == progress && fabricator.JobRecipeId == "planks" && crate.GetCount("wood") == 8, "Shed mid-job, progress pauses and isn't lost");
            yield break;
        }

        private IEnumerator BatteryBehaviour()
        {
            Vector3 here = NextSite();
            GeneratorPiece generator = MakeGenerator(here, 5f);
            BatteryPiece battery = Make<BatteryPiece>(here + Vector3.right);
            Check(battery.StoredEnergy == 0f, "A new battery is empty");
            for (int i = 0; i < 40; i++) Tick();
            Check(Near(battery.StoredEnergy, generator.outputKw * 10f, 0.5f), $"Surplus charges it ({battery.StoredEnergy:F1} kJ after 10 s)");
            float highest = 0f;
            for (int i = 0; i < 300; i++) { Tick(1f); highest = Mathf.Max(highest, battery.StoredEnergy); }
            Check(Near(battery.StoredEnergy, battery.EnergyCapacity) && highest <= battery.EnergyCapacity, $"It fills and never exceeds capacity (peak {highest})");
            Check(PowerGrid.NetworkAt(here).Stored <= PowerGrid.NetworkAt(here).Capacity, "The network reports the same");

            generator.Stop();
            TestConsumer load = MakeLoad(here + Vector3.left, 5f, 1);
            Tick();
            Check(!load.Powered, "A load above the battery's max flow isn't served");
            float full = battery.StoredEnergy;
            Check(Near(full, battery.EnergyCapacity), "Nothing was drawn for it");
            load.PowerDemand = 3f;
            for (int i = 0; i < 40; i++) Tick();
            Check(load.Powered && Near(battery.StoredEnergy, full - 30f, 0.5f), $"It carries a load with no source running ({battery.StoredEnergy:F1} kJ left)");

            BatteryPiece small = MakeRestored<BatteryPiece>(NextSite(), "{\"stored\":10}");
            TestConsumer drain = MakeLoad(small.PowerPosition + Vector3.left, 3f, 1);
            int served = 0;
            float lowest = small.StoredEnergy;
            for (int i = 0; i < 80; i++)
            {
                Tick();
                if (drain.Powered) served++;
                lowest = Mathf.Min(lowest, small.StoredEnergy);
            }
            Check(served * 0.25f * 3f <= 10f + 0.01f && served >= 12, $"It can't deliver more than it holds ({served * 0.25f * 3f:F2} of 10 kJ)");
            Check(lowest >= 0f && !drain.Powered, $"Empty, the load stops and it never goes negative (lowest {lowest:F3})");
            yield break;
        }

        private IEnumerator HeatAndOverheat()
        {
            Vector3 here = NextSite();
            GeneratorPiece generator = MakeGenerator(here, 9f);
            generator.heatPerSecond = 6f; // a busy base: one stock generator alone stays below the passive cooling
            int hot = MessagesWith("overheated"), cool = MessagesWith("cooled down");
            for (int i = 0; i < 20; i++) Tick(1f);
            PowerNetwork network = PowerGrid.NetworkAt(here);
            Check(Near(network.Heat, 60f, 0.5f) && !network.Overheated && Near(network.Supply, generator.outputKw), $"Heat rises at the generator's rate less passive cooling ({network.Heat:F1})");
            for (int i = 0; i < 20; i++) Tick(1f);
            network = PowerGrid.NetworkAt(here);
            Check(network.Overheated && network.Heat == PowerGrid.HeatCapacity, "Heat reaches the top and the network overheats");
            Check(Near(network.Supply, generator.outputKw * 0.5f), $"Overheating halves the output ({network.Supply} kW)");
            yield return new WaitForSecondsRealtime(0.7f);
            Check(MessagesWith("overheated") == hot + 1, "One overheat message");
            yield return new WaitForSecondsRealtime(0.7f);
            Check(MessagesWith("overheated") == hot + 1, "The overheat message doesn't repeat");

            CoolingUnitPiece cooler = Make<CoolingUnitPiece>(here + Vector3.right);
            for (int i = 0; i < 3; i++) Tick(1f);
            network = PowerGrid.NetworkAt(here);
            Check(cooler.PowerRunning && network.Heat < 100f && network.Heat > PowerGrid.RecoverAt && network.Overheated, $"A cooling unit brings heat down, still in overheat until the recovery point ({network.Heat:F1})");
            for (int i = 0; i < 6; i++) Tick(1f);
            network = PowerGrid.NetworkAt(here);
            Check(!network.Overheated && network.Heat <= PowerGrid.RecoverAt && Near(network.Supply, generator.outputKw), $"Below the recovery point, full output returns ({network.Heat:F1})");
            yield return new WaitForSecondsRealtime(0.7f);
            Check(MessagesWith("cooled down") == cool + 1, "One recovery message");
            yield return new WaitForSecondsRealtime(0.7f);
            Check(MessagesWith("cooled down") == cool + 1 && MessagesWith("overheated") == hot + 1, "No further messages");

            // With no power the fan does nothing: only passive cooling is left.
            for (int i = 0; i < 2; i++) Tick(1f);
            generator.Stop();
            MakeLoad(here + Vector3.left, 0f, 0);
            PowerGrid.RestoreHeat(here, 50f);
            Tick(1f);
            network = PowerGrid.NetworkAt(here);
            Check(Near(network.Heat, 50f - PowerGrid.PassiveCooling, 0.01f) && !cooler.PowerRunning, $"An unpowered cooling unit cools nothing ({network.Heat:F2})");
            Check(cooler.PowerStatus.Contains("no power"), "It says why: " + cooler.PowerStatus);

            // Solar and battery with no heat: it waits rather than drain the battery.
            Vector3 quiet = NextSite();
            BatteryPiece battery = MakeRestored<BatteryPiece>(quiet, "{\"stored\":100}");
            CoolingUnitPiece idle = Make<CoolingUnitPiece>(quiet + Vector3.right);
            for (int i = 0; i < 8; i++) Tick();
            Check(battery.StoredEnergy == 100f && idle.PowerDemand == 0f && idle.PowerStatus.Contains("idle"), "With no heat and no hot source a cooling unit draws nothing");
        }

        /// <summary>Not a pass or fail of the devices: measures a grid limitation for the coordinator's report.</summary>
        private IEnumerator HeatCellsNote()
        {
            Vector3 here = NextSite();
            GeneratorPiece generator = MakeGenerator(here, 5f);
            MakeRestored<BatteryPiece>(here + new Vector3(60f, 0f, 0f), null); // Same 200 m heat cell, out of link range.
            for (int i = 0; i < 10; i++) Tick(1f);
            PowerNetwork network = PowerGrid.NetworkAt(here);
            Debug.Log($"{Tag} NOTE: generator network with a second cold network 60 m away: heat after 10 s = {network.Heat:F1} (30.0 expected when alone). Networks sharing a heat cell overwrite each other.");
            yield break;
        }

        private IEnumerator SolarOutput()
        {
            Check(SolarPanelPiece.SkyFactor(0f, "Clear", 0f) == 0f && SolarPanelPiece.SkyFactor(22f, "Clear", 0f) == 0f, "No sun at night");
            Check(Near(SolarPanelPiece.SkyFactor(12f, "Clear", 0f), 1f), "Full sun at noon");
            Check(Near(SolarPanelPiece.SkyFactor(12f, "Rain", 0.8f), 0.4f) && Near(SolarPanelPiece.SkyFactor(12f, "Snow", 1f), 0.4f) && Near(SolarPanelPiece.SkyFactor(12f, "Fog", 0.7f), 0.4f), "Rain, snow and fog cut it to about 0.4");
            Check(SolarPanelPiece.SkyFactor(8f, "Clear", 0f) < SolarPanelPiece.SkyFactor(12f, "Clear", 0f), "A low sun gives less than noon");

            WorldAtmosphere atmosphere = ProceduralWorld.Active != null ? ProceduralWorld.Active.GetComponent<WorldAtmosphere>() : null;
            Check(atmosphere != null, "The world's clock is there to read");
            if (atmosphere == null) yield break;

            SolarPanelPiece panel = Make<SolarPanelPiece>(NextSite());
            atmosphere.RestoreTime(800f); // Midnight.
            Check(atmosphere.Hour < 1f && panel.PowerOutput == 0f && panel.PowerStatus.Contains("night"), "At the real midnight it gives nothing and says why: " + panel.PowerStatus);
            atmosphere.RestoreTime(200f); // Noon.
            float expected = panel.peakKw * SolarPanelPiece.SkyFactor(atmosphere.Hour, atmosphere.WeatherName, atmosphere.WeatherIntensity);
            Check(panel.PowerOutput > 0f && Near(panel.PowerOutput, expected), $"At the real noon it follows clock and weather ({panel.PowerOutput:F2} kW, {atmosphere.WeatherName})");
            Tick();
            Check(Near(PowerGrid.NetworkAt(panel.PowerPosition).Supply, panel.PowerOutput), "The grid receives it");
            Check(((IPowerSource)panel).HeatOutput == 0f, "It makes no heat");

            SolarPanelPiece below = Make<SolarPanelPiece>(WorldLayout.BunkerCenter + Vector3.up);
            Check(below.PowerOutput == 0f && below.PowerStatus.Contains("underground"), "In the bunker it gives nothing and says why: " + below.PowerStatus);
        }

        private IEnumerator ChargerAccumulator()
        {
            Vector3 here = NextSite();
            CellChargerPiece charger = Make<CellChargerPiece>(here);
            Step(120f, 0.25f, charger);
            Check(charger.Stored == 0 && charger.Progress == 0f, "With no power nothing accumulates");
            inventory.SetCount(CellChargerPiece.CellId, 0);
            charger.Interact(player);
            Check(inventory.GetCount(CellChargerPiece.CellId) == 0, "Collecting from an empty charger gives nothing");

            GeneratorPiece generator = MakeGenerator(here + Vector3.right, 9f);
            Step(59f, 0.25f, charger);
            Check(charger.Stored == 0, "No cell before the time is up");
            Step(2f, 0.25f, charger);
            Check(charger.Stored == 1 && charger.Progress < 2f, $"One cell after 60 s of power ({charger.Stored}, {charger.Progress:F2})");
            Tick();
            charger.Advance(1000f);
            Check(charger.Stored == 2 && charger.Progress == 0f, "A huge step pays out one cell, never a burst");
            for (int i = 0; i < 20; i++) { Tick(); charger.Advance(100f); }
            Check(charger.Stored == charger.maxStored && charger.Progress == 0f && charger.PowerDemand == 0f, "It stops at its limit and draws nothing");
            Tick();
            Check(!charger.PowerRunning, "A full charger isn't counted as running");
            charger.Advance(100f);
            Check(charger.Stored == charger.maxStored && charger.Progress == 0f, "Nothing accumulates while full");
            charger.Interact(player);
            Check(inventory.GetCount(CellChargerPiece.CellId) == charger.maxStored && charger.Stored == 0, "E moves the cells to the inventory");
            charger.Interact(player);
            Check(inventory.GetCount(CellChargerPiece.CellId) == charger.maxStored, "Pressing E again gives nothing more");
            inventory.SetCount(CellChargerPiece.CellId, 0);
            generator.Stop();
            yield break;
        }

        private IEnumerator PumpAndCrates()
        {
            Vector3 here = NextSite();
            WaterPumpPiece pump = Make<WaterPumpPiece>(here);
            Step(100f, 0.25f, pump);
            Check(pump.Stored == 0 && pump.Progress == 0f, "A pump with no power does nothing");

            MakeGenerator(here + Vector3.right, 9f);
            TestCrate far = MakeCrate(here + Vector3.forward * 8.5f);
            Step(26f, 0.25f, pump);
            Check(pump.Stored == 1 && far.GetCount("water") == 0, "A crate 8.5 m away is out of reach, so it holds the water");
            inventory.SetCount(WaterPumpPiece.WaterId, 0);
            pump.Interact(player);
            Check(pump.Stored == 0 && inventory.GetCount(WaterPumpPiece.WaterId) == 1, "E collects into the inventory");
            Step(25f * 12f, 0.25f, pump);
            Check(pump.Stored == pump.maxStored && pump.PowerDemand == 0f, "It stops when full, with no demand");

            TestCrate near = MakeCrate(here + Vector3.forward * 7f);
            Step(1f, 0.25f, pump);
            Check(pump.Stored == 0 && near.GetCount("water") == pump.maxStored, "A crate built later receives what it was holding");
            Step(26f, 0.25f, pump);
            Check(pump.Stored == 0 && near.GetCount("water") == pump.maxStored + 1 && far.GetCount("water") == 0, "New water goes straight to the crate in reach");
            inventory.SetCount(WaterPumpPiece.WaterId, 0);
            yield break;
        }

        private IEnumerator FabricatorJobs()
        {
            Vector3 here = NextSite();
            FabricatorPiece fabricator = Make<FabricatorPiece>(here);
            Check(fabricator.PowerDemand == 0f && fabricator.PowerStatus.Contains("no recipe"), "With no recipe it draws nothing and says so");
            Check(!fabricator.SelectRecipe("laser_pistol") && !fabricator.SelectRecipe("nonsense") && fabricator.SelectedRecipeId == null, "It refuses recipes of other stations");
            Check(fabricator.SelectRecipe("ammo_9mm"), "It accepts an Armory recipe");
            Check(fabricator.SelectRecipe("weapon_upgrade"), "It accepts a Workbench recipe");
            Check(fabricator.PowerDemand == 0f && fabricator.PowerStatus.Contains("no crate"), "With no crate it draws nothing and says so: " + fabricator.PowerStatus);

            // weapon_upgrade: 2 scrap, 2 wood, 1 plant fiber, split across two crates with the fiber missing.
            TestCrate a = MakeCrate(here + Vector3.forward * 2f, "scrap_metal", 2);
            TestCrate b = MakeCrate(here + Vector3.back * 2f, "wood", 2);
            GeneratorPiece generator = MakeGenerator(here + Vector3.right, 9f);
            Step(5f, 0.25f, fabricator);
            Check(a.GetCount("scrap_metal") == 2 && b.GetCount("wood") == 2 && fabricator.JobRecipeId == null, "One missing input takes nothing");
            Check(fabricator.PowerDemand == 0f && fabricator.PowerStatus.Contains("Plant Fiber"), "It names what is missing: " + fabricator.PowerStatus);

            TestCrate outOfReach = MakeCrate(here + Vector3.right * 8.5f, "plant_fiber", 5);
            Step(1f, 0.25f, fabricator);
            Check(fabricator.JobRecipeId == null && outOfReach.GetCount("plant_fiber") == 5, "Inputs beyond 8 m don't count");

            // Scrap comes first and is taken from a, then b refuses the wood.
            b.Add("plant_fiber", 1);
            b.RefuseTakes = true;
            Step(1f, 0.25f, fabricator);
            Check(fabricator.JobRecipeId == null && a.GetCount("scrap_metal") + b.GetCount("scrap_metal") == 2 && b.GetCount("wood") == 2 && b.GetCount("plant_fiber") == 1,
                "If a crate refuses midway, everything taken goes back");
            b.RefuseTakes = false;

            Step(1f, 0.25f, fabricator);
            Check(fabricator.JobRecipeId == "weapon_upgrade" && a.GetCount("scrap_metal") + b.GetCount("scrap_metal") == 0 && b.GetCount("wood") == 0 && b.GetCount("plant_fiber") == 0, "With everything there, the job takes all inputs at once");
            float progress = fabricator.JobProgress;

            generator.Stop();
            Step(10f, 0.25f, fabricator);
            Check(fabricator.JobProgress == progress && fabricator.JobRecipeId == "weapon_upgrade", "Without power the job pauses and keeps its progress");
            Check(fabricator.PowerStatus.Contains("no power"), "It says so: " + fabricator.PowerStatus);
            generator.TryStart();
            Tick();
            Check(fabricator.PowerRunning, "Power back, it resumes");

            // Save mid-job into a fresh instance, the way a load does it.
            string saved = fabricator.CapturePieceState();
            fabricator.gameObject.SetActive(false);
            FabricatorPiece restored = MakeRestored<FabricatorPiece>(here, saved);
            Check(restored.JobRecipeId == "weapon_upgrade" && restored.SelectedRecipeId == "weapon_upgrade" && restored.JobProgress == progress, "The job, recipe and progress survive a save");
            Step(21f, 0.25f, restored);
            int made = a.GetCount("weapon_upgrade") + b.GetCount("weapon_upgrade");
            Check(made == 1 && restored.JobRecipeId == null, "It finishes with one item in the crate, spending no further inputs");
            Step(25f, 0.25f, restored);
            Check(a.GetCount("weapon_upgrade") + b.GetCount("weapon_upgrade") == 1 && restored.PowerDemand == 0f, "With inputs gone, no second job and no demand");

            // Change of mind and a lost crate.
            a.Add("wood", 4);
            restored.SelectRecipe("planks");
            Step(1f, 0.25f, restored);
            Check(restored.JobRecipeId == "planks", "A new job starts on the new recipe");
            restored.SelectRecipe("weapon_upgrade");
            Check(restored.JobRecipeId == "planks", "Changing the selection leaves the running job alone");
            restored.SelectRecipe(null);
            float kept = restored.JobProgress;
            foreach (TestCrate crate in crates) StorageRegistry.Unregister(crate);
            Step(40f, 0.25f, restored);
            Check(restored.JobRecipeId == "planks" && restored.JobProgress == kept && restored.PowerDemand == 0f, "With no crate left, the job waits and nothing is lost");
            foreach (TestCrate crate in crates) StorageRegistry.Register(crate);
            Step(21f, 0.25f, restored);
            Check(restored.JobRecipeId == null && a.GetCount("planks") + b.GetCount("planks") == 3, "With a crate back, it finishes. With no recipe selected, no new job");

            // Corrupt or old state behaves like a new device.
            FabricatorPiece bad = MakeRestored<FabricatorPiece>(NextSite(), "{\"selected\":\"nonsense\",\"job\":\"also_nonsense\",\"progress\":5}");
            Check(bad.SelectedRecipeId == null && bad.JobRecipeId == null && bad.JobProgress == 0f, "A saved recipe that doesn't exist is ignored");

            // The real panel.
            FabricatorPiece panel = Make<FabricatorPiece>(NextSite());
            panel.Interact(player);
            yield return null;
            Check(panel.IsPanelOpen && Time.timeScale == 0f, "E opens the recipe panel");
            Check(Click(panel, "Recipe_planks") && panel.SelectedRecipeId == "planks", "Clicking a row selects it");
            Check(panel.GetComponentsInChildren<Button>().Count(b2 => b2.name.StartsWith("Recipe_")) == RecipeCount(), "Every Hand, Workbench and Armory recipe is listed");
            Check(PanelText(panel).Contains("> 3 Planks"), "The selected row is marked");
            Check(Click(panel, "ClearRecipe") && panel.SelectedRecipeId == null, "STOP NEW JOBS clears it");
            Click(panel, "Close");
            yield return null;
            Check(!ModalUI.IsOpen && Time.timeScale == 1f, "Closing resumes the game");
        }

        private static int RecipeCount() => SheltreSim.Crafting.RecipeBook.All.Count(FabricatorPiece.CanMake);

        private IEnumerator RepairStation()
        {
            Vector3 here = NextSite();
            RepairStationPiece station = Make<RepairStationPiece>(here);
            StructureHealth wall = Make<StructureHealth>(here + Vector3.forward * 3f);
            GeneratorPiece generator = MakeGenerator(here + Vector3.right, 9f);
            Step(1f, 0.25f, station);
            Check(station.PowerDemand == 0f && !station.PowerRunning && station.PowerStatus.Contains("nothing damaged"), "With nothing damaged it draws nothing");

            wall.TakeDamage(80f);
            Step(10f, 0.25f, station);
            Check(wall.Health == 20f && station.PowerDemand == 0f && station.PowerStatus.Contains("no Scrap Metal"), "With no scrap it repairs nothing and says so: " + station.PowerStatus);

            TestCrate crate = MakeCrate(here + Vector3.left * 3f, "scrap_metal", 1);
            generator.Stop();
            Step(10f, 0.25f, station);
            Check(wall.Health == 20f && crate.GetCount("scrap_metal") == 1, "Without power it repairs nothing and spends nothing");

            generator.TryStart();
            Step(1f, 0.25f, station);
            Check(wall.Health > 20f && wall.Health <= 23f && crate.GetCount("scrap_metal") == 0, $"With both it repairs at its rate and pays up front ({wall.Health:F2} HP)");
            Step(60f, 0.25f, station);
            Check(Near(wall.Health, 60f) && station.PaidHp == 0f, $"One scrap pays for exactly 40 HP, then it stops ({wall.Health:F2} HP)");
            Check(station.PowerDemand == 0f && station.PowerStatus.Contains("no Scrap Metal"), "It stops drawing power and says why");
            Check(MessagesWith("Repair Station stopped") >= 1, "It posts a message about it");

            crate.Add("scrap_metal", 5);
            Step(20f, 0.25f, station);
            Check(wall.Health == wall.maxHealth && crate.GetCount("scrap_metal") == 4, $"More scrap finishes the job, spending what it needs ({crate.GetCount("scrap_metal")} left)");
            Check(station.PowerDemand == 0f, "Finished, it goes idle");

            StructureHealth a = Make<StructureHealth>(here + Vector3.forward * 2f);
            StructureHealth b = Make<StructureHealth>(here + Vector3.forward * 4f);
            StructureHealth tooFar = Make<StructureHealth>(here + Vector3.forward * 13f);
            a.TakeDamage(10f); b.TakeDamage(10f); tooFar.TakeDamage(10f);
            Step(1f, 0.25f, station);
            Check(a.Health > 90f && b.Health == 90f, "It repairs one at a time, nearest first");
            Step(10f, 0.25f, station);
            Check(a.Health == 100f && b.Health == 100f && tooFar.Health == 90f, "It moves on when done and leaves what is out of reach");

            float paid = station.PaidHp;
            RepairStationPiece restored = MakeRestored<RepairStationPiece>(NextSite(), station.CapturePieceState());
            Check(Near(restored.PaidHp, paid), "Paid HP survives a save, so a load neither loses nor mints it");
            RepairStationPiece forged = MakeRestored<RepairStationPiece>(NextSite(), "{\"paidHp\":99999}");
            Check(forged.PaidHp <= forged.hpPerScrap, "A bad save can't hold more than one scrap's worth");
            yield break;
        }

        private IEnumerator FarmAndIrrigator()
        {
            Vector3 here = NextSite();
            FarmPlot plot = Make<FarmPlot>(here);
            plot.growSeconds = 100f;
            inventory.SetCount("seeds", 1);
            inventory.SetCount("fertilizer", 3);
            plot.Interact(player);
            Check(plot.State == FarmPlot.Growing && inventory.GetCount("seeds") == 0, "E plants a seed");
            Check(inventory.GetCount("fertilizer") == 3, "Planting doesn't spend fertilizer");

            IrrigatorPiece irrigator = Make<IrrigatorPiece>(here + Vector3.right * 3f);
            Step(1f, 0.25f, irrigator);
            Check(plot.SpeedMultiplier == 1f && !irrigator.PowerRunning, "An unpowered irrigator does nothing");
            GeneratorPiece generator = MakeGenerator(here + Vector3.left * 3f, 9f);
            Step(0.5f, 0.25f, irrigator);
            Check(plot.SpeedMultiplier == 2f && irrigator.PowerRunning, "Powered, it doubles growth");
            float before = plot.GrowElapsed;
            plot.AdvanceGrowth(1f);
            Check(Near(plot.GrowElapsed - before, 2f), "Growth really runs at that speed");

            plot.Interact(player);
            Check(plot.Fertilized && inventory.GetCount("fertilizer") == 2 && plot.SpeedMultiplier == 4f, "E with fertilizer spends one and doubles it again");
            plot.Interact(player);
            Check(inventory.GetCount("fertilizer") == 2 && plot.SpeedMultiplier == 4f, "A second press spends nothing");
            IrrigatorPiece second = Make<IrrigatorPiece>(here + Vector3.forward * 3f);
            Step(0.5f, 0.25f, irrigator, second);
            Check(plot.SpeedMultiplier == FarmPlot.MaxSpeedMultiplier, "A second irrigator doesn't stack past 4x");
            second.gameObject.SetActive(false);

            FarmPlot saved = MakeSavedPlot(here + Vector3.back * 30f, plot);
            Check(saved.Fertilized && saved.State == FarmPlot.Growing && Near(saved.GrowElapsed, plot.GrowElapsed), "Fertilizer and growth survive a save");
            saved.gameObject.SetActive(false);

            generator.Stop();
            Step(0.5f, 0.25f, irrigator);
            Check(plot.SpeedMultiplier == 2f, "Power gone, only the fertilizer is left");

            plot.AdvanceGrowth(100f);
            Check(plot.State == FarmPlot.Ready && !plot.Fertilized, "A finished crop has used up its fertilizer");
            generator.TryStart();
            Step(1f, 0.25f, irrigator);
            Check(plot.State == FarmPlot.Ready && irrigator.PowerDemand == 0f, "With no crate, a ready plot stays ready and it draws nothing");
            TestCrate far = MakeCrate(here + Vector3.right * 11.5f);
            Step(1f, 0.25f, irrigator);
            Check(plot.State == FarmPlot.Ready && far.GetCount("vegetables") == 0, "A crate out of reach isn't used");
            TestCrate crate = MakeCrate(here + Vector3.right * 5f);
            generator.Stop();
            Step(1f, 0.25f, irrigator);
            Check(plot.State == FarmPlot.Ready && crate.GetCount("vegetables") == 0, "With no power there's no harvest");
            generator.TryStart();
            Step(1f, 0.25f, irrigator);
            Check(plot.State == FarmPlot.Empty && crate.GetCount("vegetables") == plot.vegetableYield && crate.GetCount("seeds") == plot.seedYield, "Powered with a crate, it harvests into the crate");
            int vegetables = inventory.GetCount("vegetables");
            Step(5f, 0.25f, irrigator);
            Check(crate.GetCount("vegetables") == plot.vegetableYield && inventory.GetCount("vegetables") == vegetables && irrigator.PowerDemand == 0f, "No double harvest, and an empty plot needs nothing");

            FarmPlot old = Make<FarmPlot>(NextSite());
            old.RestoreState(FarmPlot.Growing, 10f);
            Check(!old.Fertilized && old.SpeedMultiplier == 1f, "An older save's crop has no fertilizer");
            inventory.SetCount("seeds", 0);
            inventory.SetCount("fertilizer", 0);
            yield break;
        }

        private FarmPlot MakeSavedPlot(Vector3 position, FarmPlot source)
        {
            GameObject item = new GameObject("SavedPlot");
            item.SetActive(false);
            item.transform.position = position;
            spawned.Add(item);
            FarmPlot plot = item.AddComponent<FarmPlot>();
            source.Capture(out int state, out float elapsed);
            plot.growSeconds = source.growSeconds;
            plot.RestoreState(state, elapsed);
            plot.RestorePieceState(source.CapturePieceState());
            item.SetActive(true);
            return plot;
        }

        private IEnumerator SaveRoundTrips()
        {
            Vector3 here = NextSite();
            GeneratorPiece generator = MakeGenerator(here, 4.5f);
            GeneratorPiece restoredGenerator = MakeRestored<GeneratorPiece>(NextSite(), generator.CapturePieceState());
            Check(Near(restoredGenerator.FuelUnits, 4.5f) && restoredGenerator.IsRunning, "A generator keeps its tank and switch");
            generator.Stop();
            GeneratorPiece restoredOff = MakeRestored<GeneratorPiece>(NextSite(), generator.CapturePieceState());
            Check(Near(restoredOff.FuelUnits, 4.5f) && !restoredOff.IsRunning, "A stopped generator stays stopped");

            GeneratorPiece old = MakeRestored<GeneratorPiece>(NextSite(), null);
            Check(old.FuelUnits == 0f && !old.IsRunning, "An older save's generator comes back empty and off");
            GeneratorPiece noFuel = MakeRestored<GeneratorPiece>(NextSite(), "{\"fuel\":0,\"running\":true}");
            Check(!noFuel.IsRunning, "Running with an empty tank is corrected to off");
            GeneratorPiece garbage = MakeRestored<GeneratorPiece>(NextSite(), "this is not json");
            Check(garbage.FuelUnits == 0f && !garbage.IsRunning, "A damaged state behaves like a new device");
            GeneratorPiece overfull = MakeRestored<GeneratorPiece>(NextSite(), "{\"fuel\":9999,\"running\":false}");
            Check(overfull.FuelUnits == overfull.fuelCapacity, "A tank can't load above its cap");

            BatteryPiece battery = MakeRestored<BatteryPiece>(NextSite(), "{\"stored\":321}");
            BatteryPiece batteryCopy = MakeRestored<BatteryPiece>(NextSite(), battery.CapturePieceState());
            Check(batteryCopy.StoredEnergy == 321f, "A battery keeps its charge");
            Check(MakeRestored<BatteryPiece>(NextSite(), "{\"stored\":99999}").StoredEnergy == 600f, "A battery can't load above capacity");

            CellChargerPiece charger = MakeRestored<CellChargerPiece>(NextSite(), "{\"stored\":3,\"progress\":12.5}");
            Check(charger.Stored == 3 && charger.Progress == 12.5f, "A charger keeps its cells and progress");
            Check(MakeRestored<CellChargerPiece>(NextSite(), "{\"stored\":99,\"progress\":99}").Stored == 5, "A charger can't load above its limit");

            WaterPumpPiece pump = MakeRestored<WaterPumpPiece>(NextSite(), "{\"stored\":4,\"progress\":7.5}");
            Check(pump.Stored == 4 && pump.Progress == 7.5f, "A pump keeps its water and progress");

            PowerHeatSaver saver = FindAnyObjectByType<PowerHeatSaver>();
            Check(saver != null && saver.SaveKey == "power", "The heat saver created itself");
            if (saver != null)
            {
                PowerGrid.RestoreHeat(WorldLayout.BunkerCenter, 55f);
                string saved = saver.CaptureState();
                saver.RestoreState(null);
                Check(PowerGrid.CaptureHeat(WorldLayout.BunkerCenter) == 0f, "A save with no power section means no heat");
                saver.RestoreState(saved);
                Check(PowerGrid.CaptureHeat(WorldLayout.BunkerCenter) == 55f, "Heat survives a save");
                saver.RestoreState(null);
            }
            yield break;
        }

        private IEnumerator GhostLifecycle()
        {
            Vector3 here = NextSite();
            Type[] types =
            {
                typeof(GeneratorPiece), typeof(SolarPanelPiece), typeof(BatteryPiece), typeof(CoolingUnitPiece), typeof(CellChargerPiece),
                typeof(WaterPumpPiece), typeof(IrrigatorPiece), typeof(FabricatorPiece), typeof(RepairStationPiece), typeof(PowerConsolePiece), typeof(FarmPlot),
            };
            int messagesBefore = messages.Count;
            GameAudio.PlayLog = new List<string>();
            var ghosts = new List<GameObject>();
            foreach (Type type in types)
            {
                GameObject ghost = new GameObject("Ghost_" + type.Name);
                ghost.transform.position = here;
                spawned.Add(ghost);
                ghosts.Add(ghost);
                ghost.AddComponent(type);
                foreach (MonoBehaviour behaviour in ghost.GetComponents<MonoBehaviour>()) behaviour.enabled = false;
            }
            Tick();
            Check(PowerGrid.NetworkAt(here) == null, "Build ghosts leave nothing on the grid");
            Check(!FarmPlot.All.Any(p => ghosts.Contains(p.gameObject)), "A ghost plot isn't listed for automation");
            Check(messages.Count == messagesBefore && GameAudio.PlayLog.Count == 0, $"A ghost makes no sound and no message ({GameAudio.PlayLog.Count} sounds)");
            GameAudio.PlayLog = null;

            // Real placement afterwards: enabling again joins the grid.
            ghosts[0].GetComponent<GeneratorPiece>().enabled = true;
            Tick();
            Check(PowerGrid.NetworkAt(here) != null, "Enabled again, the device joins");
            ghosts[0].SetActive(false);
            Tick();
            Check(PowerGrid.NetworkAt(here) == null, "Disabled, it leaves");
            yield break;
        }

        private IEnumerator ConsolePanel()
        {
            Vector3 here = NextSite();
            PowerConsolePiece console = Make<PowerConsolePiece>(here);
            console.Interact(player);
            yield return null;
            Check(console.IsPanelOpen && PanelText(console).Contains("No powered devices"), "With no devices near, the console says so");
            console.ClosePanel();
            yield return null;

            GeneratorPiece empty = Make<GeneratorPiece>(here + Vector3.right);
            GeneratorPiece running = MakeGenerator(here + Vector3.right * 2f, 2f);
            BatteryPiece battery = MakeRestored<BatteryPiece>(here + Vector3.right * 3f, "{\"stored\":300}");
            FabricatorPiece fabricator = Make<FabricatorPiece>(here + Vector3.left);
            CellChargerPiece charger = Make<CellChargerPiece>(here + Vector3.left * 2f);
            TestConsumer big = MakeLoad(here + Vector3.left * 3f, 50f, 0);
            TestConsumer idle = MakeLoad(here + Vector3.left * 4f, 0f, 0);
            Tick();
            console.Interact(player);
            yield return null;
            string text = PanelText(console);
            Check(text.Contains("Supply 6.0 kW"), "It shows supply");
            Check(text.Contains("Battery 30") && text.Contains("of 600 kJ"), "It shows the battery");
            Check(text.Contains("out of fuel") && text.Contains("running, 2.0 fuel"), "It shows each generator's fuel and why one stands");
            Check(text.Contains("Fabricator") && text.Contains("no recipe selected"), "It explains an idle fabricator");
            Check(text.Contains("Cell Charger") && text.Contains("charging"), "It shows a device that runs");
            Check(text.Contains("Test Load") && text.Contains("no power") && text.Contains("idle, nothing to do"), "It describes another system's devices from the grid's numbers");
            Check(text.Contains("NOT ENOUGH POWER"), "It warns of a shortage");

            // Paused: the grid can't rebuild, so its lists now hold a destroyed device.
            spawned.Remove(charger.gameObject);
            charger.gameObject.SetActive(false);
            Destroy(charger.gameObject);
            yield return null;
            yield return new WaitForSecondsRealtime(0.4f);
            Check(exceptions == 0 && !PanelText(console).Contains("Cell Charger"), "A device destroyed while paused doesn't break the open panel");
            Check(empty.PowerPosition == here + Vector3.right, "Other devices are unaffected");

            Check(Near(Time.timeScale, 0f), "The console pauses the game");
            console.ClosePanel();
            yield return null;
            Check(!ModalUI.IsOpen && Time.timeScale == 1f, "Closing resumes the game");

            GeneratorPiece hot = MakeGenerator(NextSite(), 5f);
            PowerConsolePiece hotConsole = Make<PowerConsolePiece>(hot.PowerPosition + Vector3.right);
            for (int i = 0; i < 40; i++) Tick(1f);
            hotConsole.Interact(player);
            yield return null;
            Check(PanelText(hotConsole).Contains("OVERHEATED") && PanelText(hotConsole).Contains("Heat 100 of 100"), "It warns of overheating and shows the heat");
            hotConsole.ClosePanel();
            yield return null;
        }

        /// <summary>Only meaningful if the input system takes events here, so a closed window just skips it.</summary>
        private IEnumerator EscapeClosesPanel()
        {
            PowerConsolePiece console = Make<PowerConsolePiece>(NextSite());
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                console.Interact(player);
            }
            catch
            {
                InputSystem.RemoveDevice(keyboard);
                throw;
            }
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
            for (int i = 0; i < 10 && console.IsPanelOpen; i++) yield return null;
            bool closed = !console.IsPanelOpen;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            if (closed)
            {
                Check(!ModalUI.IsOpen && Time.timeScale == 1f, "Escape closes the panel without opening a pause menu");
            }
            else
            {
                Debug.Log($"{Tag} NOTE: simulated Escape didn't arrive in this environment, closing by hand.");
                console.ClosePanel();
            }
            InputSystem.RemoveDevice(keyboard);
        }
    }
}
