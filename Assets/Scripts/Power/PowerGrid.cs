using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SheltreSim.Power
{
    /// <summary>Produces kW. Register in OnEnable, unregister in OnDisable. Fuel use belongs to the source's own Update.</summary>
    public interface IPowerSource
    {
        Vector3 PowerPosition { get; }
        float PowerOutput { get; }      // kW available right now, 0 when off or out of fuel
        float HeatOutput { get; }       // heat units per second while producing
        string PowerName { get; }
    }

    /// <summary>Draws kW. The grid calls SetPowered every tick. Gameplay must only advance while powered.</summary>
    public interface IPowerConsumer
    {
        Vector3 PowerPosition { get; }
        float PowerDemand { get; }      // kW wanted right now, 0 while idle
        int PowerPriority { get; }      // lower is served first: 0 defence, 1 life support, 2 production
        float HeatOutput { get; }       // heat units per second while powered
        float CoolingOutput { get; }    // heat units per second removed while powered, 0 for most devices
        string PowerName { get; }
        void SetPowered(bool powered);
    }

    /// <summary>Stores energy in kJ (1 kW for 1 second = 1 kJ).</summary>
    public interface IPowerStorage
    {
        Vector3 PowerPosition { get; }
        float StoredEnergy { get; }
        float EnergyCapacity { get; }
        float MaxFlow { get; }          // kW
        float Charge(float kilojoules);    // returns the amount actually stored
        float Discharge(float kilojoules); // returns the amount actually delivered
    }

    /// <summary>One connected group of devices, for status displays and heat-sensitive devices.</summary>
    public sealed class PowerNetwork
    {
        public readonly List<IPowerSource> Sources = new List<IPowerSource>();
        public readonly List<IPowerConsumer> Consumers = new List<IPowerConsumer>();
        public readonly List<IPowerStorage> Storage = new List<IPowerStorage>();
        public float Supply, Demand, Served, Stored, Capacity;
        public float Heat;              // 0..100
        public bool Overheated;
        public Vector3 Anchor;
        public bool IsPowered(IPowerConsumer consumer) => servedConsumers.Contains(consumer);
        internal readonly HashSet<IPowerConsumer> servedConsumers = new HashSet<IPowerConsumer>();
    }

    /// <summary>
    /// The one authority on electricity and heat. No cables: devices within LinkRange of each other form a network.
    /// Each device belongs to exactly one network and is counted once, so overlapping generators can't multiply
    /// output or fuel use. Consumers are served by priority: on a shortage, defence stays up and production stops.
    /// Runs only in game time. Creates itself after every scene load, no scene edit.
    /// </summary>
    public sealed class PowerGrid : MonoBehaviour
    {
        public const float LinkRange = 25f;
        public const float TickSeconds = 0.25f;
        public const float HeatCapacity = 100f;
        public const float PassiveCooling = 1f;     // heat units per second lost with no equipment at all
        public const float OverheatAt = 100f, RecoverAt = 70f;
        public const float OverheatOutputFactor = 0.5f;

        private static readonly List<IPowerSource> sources = new List<IPowerSource>();
        private static readonly List<IPowerConsumer> consumers = new List<IPowerConsumer>();
        private static readonly List<IPowerStorage> storage = new List<IPowerStorage>();
        private static readonly List<PowerNetwork> networks = new List<PowerNetwork>();
        private static readonly Dictionary<Vector3Int, float> heatByAnchor = new Dictionary<Vector3Int, float>();
        private static PowerGrid instance;
        private static bool hooked;
        private float timer;

        public static IReadOnlyList<PowerNetwork> Networks => networks;

        public static void Register(IPowerSource item) { if (!sources.Contains(item)) sources.Add(item); }
        public static void Register(IPowerConsumer item) { if (!consumers.Contains(item)) consumers.Add(item); }
        public static void Register(IPowerStorage item) { if (!storage.Contains(item)) storage.Add(item); }
        public static void Unregister(IPowerSource item) => sources.Remove(item);
        public static void Unregister(IPowerStorage item) => storage.Remove(item);
        public static void Unregister(IPowerConsumer item)
        {
            consumers.Remove(item);
            foreach (PowerNetwork network in networks) network.servedConsumers.Remove(item);
        }

        /// <summary>The network covering a position, or null if no device is within LinkRange.</summary>
        public static PowerNetwork NetworkAt(Vector3 position)
        {
            PowerNetwork best = null;
            float bestDistance = LinkRange * LinkRange;
            foreach (PowerNetwork network in networks)
            {
                foreach (IPowerSource s in network.Sources) Closer(network, s.PowerPosition, position, ref best, ref bestDistance);
                foreach (IPowerConsumer c in network.Consumers) Closer(network, c.PowerPosition, position, ref best, ref bestDistance);
                foreach (IPowerStorage b in network.Storage) Closer(network, b.PowerPosition, position, ref best, ref bestDistance);
            }
            return best;
        }

        private static void Closer(PowerNetwork network, Vector3 node, Vector3 position, ref PowerNetwork best, ref float bestDistance)
        {
            float d = (node - position).sqrMagnitude;
            if (d <= bestDistance) { bestDistance = d; best = network; }
        }

        /// <summary>Probe only: advances the grid without waiting for real time.</summary>
        public static void DebugTick(float seconds) { if (instance != null) instance.Tick(seconds); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            sources.Clear(); consumers.Clear(); storage.Clear(); networks.Clear(); heatByAnchor.Clear();
            instance = null; hooked = false;
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
            if (instance == null) new GameObject("PowerGrid").AddComponent<PowerGrid>();
        }

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
        }

        private void OnDestroy()
        {
            if (instance != this) return;
            instance = null;
            networks.Clear();
            heatByAnchor.Clear();
        }

        private void Update()
        {
            if (Time.timeScale <= 0f) return;
            timer += Time.deltaTime;
            if (timer < TickSeconds) return;
            float seconds = Mathf.Min(timer, 1f); // a long frame can't drain a battery in one gulp
            timer = 0f;
            Tick(seconds);
        }

        private readonly List<Vector3> nodePositions = new List<Vector3>();
        private readonly List<object> nodes = new List<object>();
        private int[] parent = new int[64];

        private int Find(int i)
        {
            while (parent[i] != i) { parent[i] = parent[parent[i]]; i = parent[i]; }
            return i;
        }

        private void Tick(float seconds)
        {
            nodes.Clear(); nodePositions.Clear();
            foreach (IPowerSource s in sources) { nodes.Add(s); nodePositions.Add(s.PowerPosition); }
            foreach (IPowerConsumer c in consumers) { nodes.Add(c); nodePositions.Add(c.PowerPosition); }
            foreach (IPowerStorage b in storage) { nodes.Add(b); nodePositions.Add(b.PowerPosition); }
            if (parent.Length < nodes.Count) parent = new int[nodes.Count * 2];
            for (int i = 0; i < nodes.Count; i++) parent[i] = i;
            float link = LinkRange * LinkRange;
            for (int i = 0; i < nodes.Count; i++)
                for (int j = i + 1; j < nodes.Count; j++)
                    if ((nodePositions[i] - nodePositions[j]).sqrMagnitude <= link) parent[Find(i)] = Find(j);

            var groups = new Dictionary<int, PowerNetwork>();
            networks.Clear();
            for (int i = 0; i < nodes.Count; i++)
            {
                int root = Find(i);
                if (!groups.TryGetValue(root, out PowerNetwork network))
                {
                    network = new PowerNetwork { Anchor = nodePositions[root] };
                    groups[root] = network;
                    networks.Add(network);
                }
                if (nodes[i] is IPowerSource source && i < sources.Count) network.Sources.Add(source);
                else if (nodes[i] is IPowerConsumer consumer && i < sources.Count + consumers.Count) network.Consumers.Add(consumer);
                else if (nodes[i] is IPowerStorage store) network.Storage.Add(store);
            }

            var nextHeat = new Dictionary<Vector3Int, float>();
            foreach (PowerNetwork network in networks) Serve(network, seconds, nextHeat);
            heatByAnchor.Clear();
            foreach (var pair in nextHeat) heatByAnchor[pair.Key] = pair.Value;
        }

        // Networks are rebuilt every tick, so heat is remembered by a coarse position of the network's area.
        private static Vector3Int HeatCell(Vector3 p) => new Vector3Int(Mathf.RoundToInt(p.x / 200f), 0, Mathf.RoundToInt(p.z / 200f));

        private static void Serve(PowerNetwork network, float seconds, Dictionary<Vector3Int, float> nextHeat)
        {
            Vector3Int cell = HeatCell(network.Anchor);
            heatByAnchor.TryGetValue(cell, out float heatStored);
            bool wasOver = heatStored >= OverheatAt;
            float recoverLatch = wasOver ? 0f : heatStored;
            network.Heat = heatStored;
            network.Overheated = overheatedCells.Contains(cell);

            float factor = network.Overheated ? OverheatOutputFactor : 1f;
            float heatIn = 0f, coolingIn = PassiveCooling;
            float supply = 0f;
            foreach (IPowerSource source in network.Sources)
            {
                float output = Mathf.Max(0f, source.PowerOutput);
                supply += output * factor;
                if (output > 0f) heatIn += Mathf.Max(0f, source.HeatOutput);
            }

            network.Consumers.Sort((a, b) => a.PowerPriority.CompareTo(b.PowerPriority));
            float flow = 0f, stored = 0f, capacity = 0f;
            foreach (IPowerStorage store in network.Storage)
            {
                flow += Mathf.Max(0f, store.MaxFlow);
                stored += Mathf.Max(0f, store.StoredEnergy);
                capacity += Mathf.Max(0f, store.EnergyCapacity);
            }
            float fromBattery = seconds > 0f ? Mathf.Min(flow, stored / seconds) : 0f;
            float available = supply + fromBattery;

            float demand = 0f, served = 0f;
            network.servedConsumers.Clear();
            foreach (IPowerConsumer consumer in network.Consumers)
            {
                float want = Mathf.Max(0f, consumer.PowerDemand);
                demand += want;
                bool on = want <= 0f ? supply > 0f || stored > 0f : served + want <= available + 0.0001f;
                consumer.SetPowered(on);
                if (!on) continue;
                served += want;
                network.servedConsumers.Add(consumer);
                heatIn += Mathf.Max(0f, consumer.HeatOutput);
                coolingIn += Mathf.Max(0f, consumer.CoolingOutput);
            }

            float balance = (supply - served) * seconds;
            foreach (IPowerStorage store in network.Storage)
            {
                if (balance > 0f) balance -= store.Charge(balance);
                else if (balance < 0f) balance += store.Discharge(-balance);
            }

            float heat = Mathf.Clamp(heatStored + (heatIn - coolingIn) * seconds, 0f, HeatCapacity);
            if (heat >= OverheatAt) overheatedCells.Add(cell);
            else if (heat <= RecoverAt) overheatedCells.Remove(cell);
            network.Heat = heat;
            network.Overheated = overheatedCells.Contains(cell);
            network.Supply = supply; network.Demand = demand; network.Served = served;
            network.Stored = 0f; network.Capacity = capacity;
            foreach (IPowerStorage store in network.Storage) network.Stored += store.StoredEnergy;
            nextHeat[cell] = heat;
        }

        private static readonly HashSet<Vector3Int> overheatedCells = new HashSet<Vector3Int>();

        /// <summary>Saved heat for the network covering the bunker. One number is enough: only the base stays resident for long.</summary>
        public static float CaptureHeat(Vector3 position)
        {
            return heatByAnchor.TryGetValue(HeatCell(position), out float heat) ? heat : 0f;
        }

        public static void RestoreHeat(Vector3 position, float heat)
        {
            heatByAnchor.Clear();
            overheatedCells.Clear();
            if (heat > 0f && !float.IsNaN(heat) && !float.IsInfinity(heat))
            {
                heat = Mathf.Min(heat, HeatCapacity);
                heatByAnchor[HeatCell(position)] = heat;
                if (heat >= OverheatAt) overheatedCells.Add(HeatCell(position));
            }
        }
    }
}
