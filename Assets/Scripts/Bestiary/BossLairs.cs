using SheltreSim.Player;
using SheltreSim.Items;
using SheltreSim.UI;
using SheltreSim.World;
using System.Linq;
using UnityEngine;

namespace SheltreSim.Bestiary
{
    /// <summary>
    /// "A boss guards something": factory, farm and military landmarks each get a boss beside a marked store, on free deck
    /// space next to the building (deck 42 x 36 m, centred on the chunk, rooms open towards -Z). Ruined world only. The
    /// boss is dormant until the player touches the store or steps into the arena. The store opens once the boss is down.
    /// Built after the landmark, in its own objects: it reads the landmark's place and kind, never its random draws.
    /// </summary>
    public static class BossLairs
    {
        public const string RootName = "BossLair";

        public struct Layout
        {
            public SpeciesId boss;
            public string store, storeTitle, bossName;
            public Vector3 storeLocal, bossLocal;
            public float arenaRadius;
            public (string id, int amount)[] contents;
        }

        public static bool TryLayout(int kind, out Layout layout)
        {
            layout = default;
            switch (kind)
            {
                case ProceduralLandmarks.KindFactory:
                    // The open service yard west of the hall, in front of the smokestacks.
                    layout = new Layout
                    {
                        boss = SpeciesId.Gatekeeper, bossName = "Gatekeeper", store = "SealedStoreroom", storeTitle = "Sealed storeroom",
                        storeLocal = new Vector3(-17.5f, 0f, -13.5f), bossLocal = new Vector3(-14.5f, 0f, -8.5f), arenaRadius = 6f,
                        contents = new[] { ("metal_plate", 8), ("copper_wire", 8), ("electric_motor", 2) },
                    };
                    return true;
                case ProceduralLandmarks.KindFarm:
                    // Front-left, between the entrance path and the west growing beds.
                    layout = new Layout
                    {
                        boss = SpeciesId.RootColossus, bossName = "Root Colossus", store = "Greenhouse", storeTitle = "Greenhouse",
                        storeLocal = new Vector3(-6.5f, 0f, -14.5f), bossLocal = new Vector3(-7f, 0f, -9.5f), arenaRadius = 5f,
                        contents = new[] { ("seeds", 12), ("fertilizer", 4), ("chemicals", 4) },
                    };
                    return true;
                case ProceduralLandmarks.KindMilitary:
                    // West yard inside the fence, beside the command hut.
                    layout = new Layout
                    {
                        boss = SpeciesId.ShiftCommander, bossName = "Shift Commander", store = "ArmoryLocker", storeTitle = "Armory locker",
                        storeLocal = new Vector3(-15.5f, 0f, 12f), bossLocal = new Vector3(-14f, 0f, 6f), arenaRadius = 6f,
                        contents = new[] { ("assault_rifle", 1), ("ammo_rifle", 60) },
                    };
                    return true;
            }
            return false;
        }

        /// <summary>A lair's place on the ground plane, from the seed alone (for the atlas). False when the landmark has no lair.</summary>
        public static bool TryPosition(Vector2Int c, int seed, out Vector3 position, out string bossName)
        {
            position = default; bossName = "";
            if (!ProceduralLandmarks.TryGetKind(c, seed, out int kind) || !TryLayout(kind, out Layout layout)) return false;
            position = ProceduralLandmarks.WorldCenter(c) + layout.bossLocal;
            bossName = layout.bossName;
            return !EncounterPlacement.NearBunker(position);
        }

        public static void Build(Transform chunk, Vector2Int c, int seed, ProceduralWorldAssets assets)
        {
            if (!ProceduralLandmarks.TryGetKind(c, seed, out int kind) || !TryLayout(kind, out Layout layout)) return;
            Transform landmark = chunk.Find("Landmark_" + ProceduralLandmarks.KindName(kind));
            if (landmark == null) return;
            if (EncounterPlacement.NearBunker(landmark.position + layout.bossLocal)) return;
            var root = new GameObject(RootName).transform;
            root.SetParent(chunk, false);
            root.SetPositionAndRotation(landmark.position, Quaternion.identity); // y = the deck top
            Material wall = assets.structureMaterial, dark = assets.roadMaterial;

            GameObject store;
            switch (kind)
            {
                case ProceduralLandmarks.KindFactory: store = Storeroom(root, layout.storeLocal, wall, dark); break;
                case ProceduralLandmarks.KindFarm: store = Greenhouse(root, layout.storeLocal, wall, dark); break;
                default: store = Locker(root, layout.storeLocal, wall, dark); break;
            }
            DressStore(kind, store.transform);
            Warning(root, layout.storeLocal, dark, assets);
            Physics.SyncTransforms();

            SpeciesSpec spec = SpeciesSpec.Of(layout.boss);
            Vector3 p = root.TransformPoint(layout.bossLocal);
            if (!EncounterPlacement.TryStand(p.x, p.z, spec.Radius, spec.Height, seed, out Vector3 feet, root.position.y - WorldLayout.HeightAt(p.x, p.z, seed) + 0.6f))
                feet = p;
            // Back to the store, facing whoever comes for it.
            Vector3 away = feet - root.TransformPoint(layout.storeLocal);
            away.y = 0f;
            float yaw = away.sqrMagnitude > 0.01f ? Quaternion.LookRotation(away).eulerAngles.y : 180f;
            ChunkEncounterSlot boss = EncounterPlacement.SpawnSlot(root, chunk, "Boss", layout.boss, feet, yaw, c, seed, feet, layout.arenaRadius);

            var chest = store.AddComponent<EncounterChest>();
            chest.guard = boss;
            chest.title = layout.storeTitle;
            chest.lockedMessage = $"The {layout.storeTitle.ToLowerInvariant()} won't open while the {layout.bossName} guards it.";
            foreach (var row in layout.contents) EncounterPlacement.AddLoot(chest, row.id, row.amount);
            chest.hasLoot = chest.entries.Count > 0;

            var lair = root.gameObject.AddComponent<BossLair>();
            Vector3 arenaCenter = (feet + root.TransformPoint(layout.storeLocal)) * .5f;
            CreatePhaseAdds(root, chunk, layout, boss, arenaCenter, seed, c);
            // Configure gathers the phase slots from the hierarchy, including the inactive ones.
            lair.Configure(boss, layout.bossName, arenaCenter, layout.arenaRadius, layout.boss, c, seed, root, chunk);
            var site = root.gameObject.AddComponent<EncounterSite>();
            site.siteId = EncounterAtlas.LairId(c, seed);
            site.title = layout.bossName + "'s lair";
        }

        private static void CreatePhaseAdds(Transform root, Transform chunk, Layout layout, ChunkEncounterSlot boss,
            Vector3 arenaCenter, int seed, Vector2Int coordinate)
        {
            SpeciesId[] species = layout.boss == SpeciesId.RootColossus
                ? new[] { SpeciesId.Rootwalker, SpeciesId.Wanderer }
                : layout.boss == SpeciesId.Gatekeeper
                    ? new[] { SpeciesId.Plated, SpeciesId.Runner }
                    : new[] { SpeciesId.Scavenger, SpeciesId.Wanderer };
            SpeciesSpec leadSpec = SpeciesSpec.Of(layout.boss);
            Vector3 bossPosition = boss != null ? boss.transform.position : arenaCenter;
            for (int i = 0; i < species.Length; i++)
            {
                SpeciesSpec addSpec = SpeciesSpec.Of(species[i]);
                Vector3 feet = default;
                bool found = false;
                for (int attempt = 0; attempt < 8 && !found; attempt++)
                {
                    float angle = (i * 180f + attempt * 45f) * Mathf.Deg2Rad;
                    float distance = Mathf.Min(layout.arenaRadius - 1.2f, leadSpec.Radius + addSpec.Radius + 1.7f + attempt * .15f);
                    float x = bossPosition.x + Mathf.Cos(angle) * distance;
                    float z = bossPosition.z + Mathf.Sin(angle) * distance;
                    float lift = root.position.y - WorldLayout.HeightAt(x, z, seed) + .6f;
                    if (EncounterPlacement.TryStand(x, z, addSpec.Radius, addSpec.Height, seed, out feet, lift)) found = true;
                }
                if (!found) continue;
                ChunkEncounterSlot slot = EncounterPlacement.SpawnSlot(root, chunk, "PhaseAdd_" + i, species[i], feet,
                    (float)(WorldLayout.Hash(coordinate.x, coordinate.y, seed + 1193 + i) % 360), coordinate, seed,
                    bossPosition, layout.arenaRadius + 3f);
                // The deterministic slots exist before any damage, so their health/death state can
                // be restored if the landmark chunk unloads after the phase has started.
                if (slot != null) slot.gameObject.SetActive(false);
            }
        }

        static void DressStore(int kind, Transform store)
        {
            switch (kind)
            {
                case ProceduralLandmarks.KindFactory:
                    // The sealed store and aggregate blocker stay intact; gate art follows the existing +X doorway.
                    HideRenderers(store, "ShutterDoor");
                    HideRenderers(store, "Stripe");
                    GameObject gate = PlaceVisual(store, "lmk_boss_gate", new Vector3(1.62f, 0f, 0f), new Vector3(0f, 90f, 0f));
                    if (gate != null) gate.transform.localScale = Vector3.one * .8f;
                    break;
                case ProceduralLandmarks.KindFarm:
                    // Root mass dresses the planter footprint; the store's loot and collider remain on the root.
                    HideRenderers(store, "Planter");
                    HideRenderers(store, "Sprout");
                    PlaceVisual(store, "lmk_boss_roots", Vector3.zero);
                    break;
                case ProceduralLandmarks.KindMilitary:
                    HideRenderers(store, "Cabinet");
                    HideRenderers(store, "Doors");
                    HideRenderers(store, "Stencil");
                    PlaceVisual(store, "lmk_boss_vault", Vector3.zero);
                    break;
            }
        }

        static GameObject PlaceVisual(Transform parent, string id, Vector3 position, Vector3 euler = default)
        {
            GameObject prefab = Resources.Load<GameObject>("Props/" + id);
            if (prefab == null) return null;
            GameObject visual = Object.Instantiate(prefab, parent, false);
            visual.name = "Visual_" + id;
            visual.transform.localPosition = position;
            visual.transform.localRotation = Quaternion.Euler(euler);
            return visual;
        }

        static void HideRenderers(Transform parent, string objectName)
        {
            foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
            {
                if (child.name != objectName) continue;
                Renderer renderer = child.GetComponent<Renderer>();
                if (renderer != null) renderer.enabled = false;
            }
        }

        // ---- the marked stores. The store object carries the collider and the loot. ----

        static GameObject Storeroom(Transform root, Vector3 local, Material wall, Material dark)
        {
            var store = new GameObject("SealedStoreroom");
            store.transform.SetParent(root, false);
            store.transform.localPosition = local;
            EncounterPlacement.Prop(store.transform, "Walls", PrimitiveType.Cube, new Vector3(0f, 1.4f, 0f), new Vector3(3.2f, 2.8f, 3f), Quaternion.identity, wall, false);
            EncounterPlacement.Prop(store.transform, "Roof", PrimitiveType.Cube, new Vector3(0f, 2.9f, 0f), new Vector3(3.6f, .2f, 3.4f), Quaternion.identity, dark, false);
            EncounterPlacement.Prop(store.transform, "ShutterDoor", PrimitiveType.Cube, new Vector3(1.62f, 1.1f, 0f), new Vector3(.06f, 2.1f, 1.8f), Quaternion.identity, dark, false, new Color(.45f, .4f, .32f));
            for (int i = 0; i < 4; i++)
                EncounterPlacement.Prop(store.transform, "Stripe", PrimitiveType.Cube, new Vector3(1.66f, .4f + i * .5f, 0f), new Vector3(.03f, .14f, 1.9f),
                    Quaternion.Euler(30f, 0f, 0f), null, false, new Color(.95f, .75f, .1f));
            var box = store.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 1.4f, 0f);
            box.size = new Vector3(3.3f, 2.8f, 3.1f);
            return store;
        }

        static GameObject Greenhouse(Transform root, Vector3 local, Material wall, Material dark)
        {
            var store = new GameObject("Greenhouse");
            store.transform.SetParent(root, false);
            store.transform.localPosition = local;
            Color frame = new Color(.75f, .78f, .72f), glass = new Color(.55f, .75f, .62f);
            for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                    EncounterPlacement.Prop(store.transform, "Post", PrimitiveType.Cube, new Vector3(x * 2.2f, 1.2f, z * 1.6f), new Vector3(.1f, 2.4f, .1f), Quaternion.identity, wall, false, frame);
            for (int side = -1; side <= 1; side += 2)
                EncounterPlacement.Prop(store.transform, "RoofPane", PrimitiveType.Cube, new Vector3(0f, 2.65f, side * .85f), new Vector3(4.6f, .05f, 1.9f),
                    Quaternion.Euler(side * -22f, 0f, 0f), wall, false, glass);
            EncounterPlacement.Prop(store.transform, "SidePane", PrimitiveType.Cube, new Vector3(0f, 1.2f, 1.6f), new Vector3(4.4f, 2.3f, .04f), Quaternion.identity, wall, false, glass);
            EncounterPlacement.Prop(store.transform, "Planter", PrimitiveType.Cube, new Vector3(0f, .4f, .3f), new Vector3(3.6f, .8f, 1.4f), Quaternion.identity, dark, false);
            for (int i = 0; i < 5; i++)
                EncounterPlacement.Prop(store.transform, "Sprout", PrimitiveType.Sphere, new Vector3(-1.4f + i * .7f, .95f, .3f), new Vector3(.4f, .45f, .4f), Quaternion.identity, wall, false, new Color(.3f, .55f, .2f));
            var box = store.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, .45f, .3f);
            box.size = new Vector3(3.7f, .9f, 1.5f);
            return store;
        }

        static GameObject Locker(Transform root, Vector3 local, Material wall, Material dark)
        {
            var store = new GameObject("ArmoryLocker");
            store.transform.SetParent(root, false);
            store.transform.localPosition = local;
            EncounterPlacement.Prop(store.transform, "Cabinet", PrimitiveType.Cube, new Vector3(0f, 1.05f, 0f), new Vector3(1.6f, 2.1f, .7f), Quaternion.identity, wall, false, new Color(.32f, .38f, .28f));
            EncounterPlacement.Prop(store.transform, "Doors", PrimitiveType.Cube, new Vector3(0f, 1.05f, -.37f), new Vector3(1.5f, 2f, .04f), Quaternion.identity, dark, false);
            EncounterPlacement.Prop(store.transform, "Stencil", PrimitiveType.Cube, new Vector3(0f, 1.6f, -.4f), new Vector3(.9f, .25f, .02f), Quaternion.identity, null, false, new Color(.9f, .85f, .7f));
            var box = store.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 1.05f, 0f);
            box.size = new Vector3(1.6f, 2.1f, .75f);
            return store;
        }

        // A red lamp on a post: the store reads as "the thing" from across the deck.
        static void Warning(Transform root, Vector3 storeLocal, Material dark, ProceduralWorldAssets assets)
        {
            Vector3 local = storeLocal + new Vector3(storeLocal.x < 0f ? 2.6f : -2.6f, 0f, -1.4f);
            if (assets.lairWarningPost != null)
            {
                GameObject visual = Object.Instantiate(assets.lairWarningPost, root, false);
                visual.name = "Visual_WarningPost";
                visual.transform.localPosition = local;
                Transform lampPart = null;
                foreach (Transform child in visual.GetComponentsInChildren<Transform>(true))
                    if (child.name == "Lamp") { lampPart = child; break; }
                if (lampPart != null)
                {
                    var warningLight = lampPart.gameObject.AddComponent<Light>();
                    warningLight.type = LightType.Point;
                    warningLight.color = new Color(1f, .25f, .15f);
                    warningLight.range = 9f;
                    warningLight.intensity = 1.6f;
                    warningLight.shadows = LightShadows.None;
                }
                return;
            }
            EncounterPlacement.Prop(root, "WarningPost", PrimitiveType.Cylinder, local + Vector3.up * 1.5f, new Vector3(.12f, 1.5f, .12f), Quaternion.identity, dark, false);
            var lamp = EncounterPlacement.Prop(root, "WarningLamp", PrimitiveType.Sphere, local + Vector3.up * 3.1f, Vector3.one * .3f, Quaternion.identity, null, false, new Color(1f, .2f, .1f));
            var light = lamp.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, .25f, .15f);
            light.range = 9f;
            light.intensity = 1.6f;
            light.shadows = LightShadows.None;
        }
    }

    /// <summary>Wakes the lair's boss when the player steps into the arena.</summary>
    public sealed class BossLair : MonoBehaviour
    {
        public ChunkEncounterSlot boss;
        public string bossName;
        public Vector3 arenaCenter;
        public float arenaRadius;
        public bool Woken { get; private set; }
        public bool ArenaLocked { get; private set; }
        public bool PhaseTriggered => phaseMarker != null && phaseMarker.Searched;
        public int PhaseAddCount => phaseSlots != null ? phaseSlots.Length : 0;
        private Transform player;
        private Transform lairRoot, chunkRoot, gate;
        private Vector2Int coordinate;
        private int seed;
        private SpeciesId bossSpecies;
        private LootContainer wakeMarker, phaseMarker;
        private ChunkEncounterSlot[] phaseSlots = System.Array.Empty<ChunkEncounterSlot>();
        private bool phaseSlotsActivated, gateOpened;
        private float phaseAddsAt = -1f;
        private Vector3 gateClosedPosition, gateOpenPosition;

        public void Configure(ChunkEncounterSlot bossSlot, string displayName, Vector3 center, float radius,
            SpeciesId species, Vector2Int chunkCoordinate, int worldSeed, Transform root, Transform chunk)
        {
            boss = bossSlot;
            bossName = displayName;
            arenaCenter = center;
            arenaRadius = radius;
            bossSpecies = species;
            coordinate = chunkCoordinate;
            seed = worldSeed;
            lairRoot = root;
            chunkRoot = chunk;
            wakeMarker = CreateMarker("BossWakeMarker");
            phaseMarker = CreateMarker("BossPhaseMarker");
            phaseSlots = lairRoot.GetComponentsInChildren<ChunkEncounterSlot>(true)
                .Where(slot => slot.name.StartsWith("PhaseAdd_", System.StringComparison.Ordinal)).ToArray();
        }

        private LootContainer CreateMarker(string markerName)
        {
            GameObject markerObject = new GameObject(markerName);
            markerObject.transform.SetParent(transform, false);
            LootContainer marker = markerObject.AddComponent<LootContainer>();
            marker.entries.Clear();
            marker.hasLoot = false;
            return marker;
        }

        public void Wake()
        {
            if (Woken || boss == null || boss.Defeated || boss.Actor == null) return;
            Woken = true;
            if (wakeMarker != null && !wakeMarker.Searched) wakeMarker.RestoreSearchedState(true);
            LockArena();
            var actor = boss.Actor.GetComponent<SpeciesActor>();
            if (actor != null) SpeciesBehaviours.Wake(actor);
            GameMessageBus.Post($"The {bossName} wakes. The arena gates lock behind you.");
        }

        private void Update()
        {
            if (boss == null || Time.timeScale <= 0f) return;
            if (boss.Defeated || boss.Actor == null)
            {
                if (ArenaLocked) OpenArena();
                return;
            }
            if (wakeMarker != null && wakeMarker.Searched && !Woken) Wake();
            SpeciesBrain bossBrain = boss.Actor.GetComponent<SpeciesBrain>();
            if (!Woken && (boss.Actor.health < boss.Actor.maxHealth - .01f || (bossBrain != null && !bossBrain.IsDormant))) Wake();
            if (PhaseTriggered && !phaseSlotsActivated && phaseAddsAt < 0f) phaseAddsAt = Time.time + 1.35f;
            if (PhaseTriggered && !phaseSlotsActivated && phaseAddsAt >= 0f && Time.time >= phaseAddsAt) ActivatePhaseAdds();
            if (Woken && !PhaseTriggered && boss.Actor.health <= boss.Actor.maxHealth * .5f)
                TriggerPhase();
            if (gateOpened && gate != null)
            {
                gate.localPosition = Vector3.MoveTowards(gate.localPosition, gateOpenPosition,
                    5f * Time.deltaTime);
                if (Vector3.Distance(gate.localPosition, gateOpenPosition) < .02f)
                {
                    Collider collider = gate.GetComponent<Collider>();
                    if (collider != null) collider.enabled = false;
                }
            }
            if (Woken || boss.Defeated) return;
            if (player == null)
            {
                var controller = FindFirstObjectByType<FirstPersonController>();
                if (controller == null) return;
                player = controller.transform;
            }
            Vector3 d = player.position - arenaCenter;
            if (Mathf.Abs(d.y) < 4f && new Vector2(d.x, d.z).sqrMagnitude < arenaRadius * arenaRadius) Wake();
        }

        private void TriggerPhase()
        {
            if (phaseMarker == null) return;
            phaseMarker.RestoreSearchedState(true);
            phaseAddsAt = Time.time + 1.35f;
            GameMessageBus.Post($"The {bossName} roars and braces for a second assault! Reinforcements are approaching.");
        }

        private void ActivatePhaseAdds()
        {
            phaseSlotsActivated = true;
            int awakened = 0;
            foreach (ChunkEncounterSlot slot in phaseSlots)
            {
                if (slot == null || slot.Defeated || slot.Actor == null) continue;
                slot.gameObject.SetActive(true);
                SpeciesActor actor = slot.Actor.GetComponent<SpeciesActor>();
                if (actor != null) SpeciesBehaviours.Wake(actor);
                awakened++;
            }
            if (awakened > 0) GameMessageBus.Post($"{awakened} reinforcements joined the fight.");
        }

        private void LockArena()
        {
            if (ArenaLocked || lairRoot == null) return;
            ArenaLocked = true;
            float half = arenaRadius + 1.25f;
            float y = 1.35f;
            Color gateColor = new Color(.38f, .16f, .12f);
            // Fixed perimeter pieces leave no path through the lockdown; the south shutter is the
            // single animated exit and only rises after the guardian falls.
            Barrier("NorthBarrier", arenaCenter + Vector3.forward * half, new Vector3(half * 2f, 2.7f, .28f), gateColor);
            Barrier("EastBarrier", arenaCenter + Vector3.right * half, new Vector3(.28f, 2.7f, half * 2f), gateColor);
            Barrier("WestBarrier", arenaCenter - Vector3.right * half, new Vector3(.28f, 2.7f, half * 2f), gateColor);
            float sideWidth = Mathf.Max(.5f, (half * 2f - 2.8f) * .5f);
            Vector3 south = arenaCenter - Vector3.forward * half;
            Barrier("SouthBarrierLeft", south - Vector3.right * (1.4f + sideWidth * .5f), new Vector3(sideWidth, 2.7f, .28f), gateColor);
            Barrier("SouthBarrierRight", south + Vector3.right * (1.4f + sideWidth * .5f), new Vector3(sideWidth, 2.7f, .28f), gateColor);
            GameObject shutter = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shutter.name = "ArenaShutter";
            shutter.transform.SetParent(lairRoot, true);
            shutter.transform.position = arenaCenter - Vector3.forward * half + Vector3.up * y;
            shutter.transform.localScale = new Vector3(2.4f, 2.7f, .4f);
            Renderer renderer = shutter.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = ProceduralWorld.Active != null ? ProceduralWorld.Active.assets.roadMaterial : null;
            gate = shutter.transform;
            gateClosedPosition = gate.localPosition;
            gateOpenPosition = gateClosedPosition + Vector3.up * 3.2f;
        }

        private readonly System.Collections.Generic.List<GameObject> arenaBarriers = new System.Collections.Generic.List<GameObject>();

        private void Barrier(string barrierName, Vector3 position, Vector3 scale, Color tint)
        {
            GameObject barrier = GameObject.CreatePrimitive(PrimitiveType.Cube);
            barrier.name = barrierName;
            barrier.transform.SetParent(lairRoot, true);
            barrier.transform.position = position + Vector3.up * 1.35f;
            barrier.transform.localScale = scale;
            arenaBarriers.Add(barrier);
            Renderer renderer = barrier.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = ProceduralWorld.Active != null ? ProceduralWorld.Active.assets.roadMaterial : null;
                Material material = renderer.material;
                if (material.HasProperty("_Color")) material.color = tint;
            }
        }

        private void OpenArena()
        {
            ArenaLocked = false;
            // The perimeter walls must go as soon as the guardian falls: only the south shutter used to rise, and its animation sat
            // behind an early return, so after the kill the player stayed walled in. Remove every barrier and the shutter now.
            foreach (GameObject barrier in arenaBarriers) if (barrier != null) Destroy(barrier);
            arenaBarriers.Clear();
            if (gate != null) { Destroy(gate.gameObject); gate = null; }
            if (gate == null && !gateOpened) { gateOpened = true; }
            GameMessageBus.Post($"The {bossName}'s arena gate opens. The guarded cache is clear.");
        }
    }
}
