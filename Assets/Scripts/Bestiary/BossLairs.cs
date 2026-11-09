using SheltreSim.Player;
using SheltreSim.UI;
using SheltreSim.World;
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
            Warning(root, layout.storeLocal, dark);
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
            lair.boss = boss;
            lair.bossName = layout.bossName;
            lair.arenaCenter = (feet + root.TransformPoint(layout.storeLocal)) * .5f;
            lair.arenaRadius = layout.arenaRadius;
            var site = root.gameObject.AddComponent<EncounterSite>();
            site.siteId = EncounterAtlas.LairId(c, seed);
            site.title = layout.bossName + "'s lair";
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
        static void Warning(Transform root, Vector3 storeLocal, Material dark)
        {
            Vector3 local = storeLocal + new Vector3(storeLocal.x < 0f ? 2.6f : -2.6f, 0f, -1.4f);
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
        private Transform player;

        public void Wake()
        {
            if (Woken || boss == null || boss.Defeated || boss.Actor == null) return;
            Woken = true;
            var actor = boss.Actor.GetComponent<SpeciesActor>();
            if (actor != null) SpeciesBehaviours.Wake(actor);
            GameMessageBus.Post($"The {bossName} wakes.");
        }

        private void Update()
        {
            if (Woken || boss == null || boss.Defeated || Time.timeScale <= 0f) return;
            if (player == null)
            {
                var controller = FindFirstObjectByType<FirstPersonController>();
                if (controller == null) return;
                player = controller.transform;
            }
            Vector3 d = player.position - arenaCenter;
            if (Mathf.Abs(d.y) < 4f && new Vector2(d.x, d.z).sqrMagnitude < arenaRadius * arenaRadius) Wake();
        }
    }
}
