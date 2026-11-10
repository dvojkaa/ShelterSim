using System.Collections.Generic;
using System.Linq;
using SheltreSim.Core;
using SheltreSim.Interaction;
using SheltreSim.Saving;
using SheltreSim.Storage;
using SheltreSim.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SheltreSim.EditorTools
{
    /// <summary>
    /// Replaces the old starting region with the real start of the game: a construction site on a
    /// suburban lot on the west edge of the first town, with the bunker under it and stairs going down.
    /// Runs after every older milestone, because those build what this removes and reuses (the player,
    /// the UI, the raider prefab), and because the older tools depend on each other too much to unpick.
    ///
    /// Ground level is WorldLayout.HomeGroundY (6 m), bunker floor is y=0, so 3 m of earth lie over the roof.
    /// </summary>
    public static class M20HomeSetup
    {
        private const float Ground = WorldLayout.HomeGroundY;
        private const float RoomMinX = 65f, RoomMaxX = 79f, RoomMinZ = -223.6f, RoomMaxZ = -211.6f, RoomHeight = 3f;
        private const float StairX = 72f, StairHalfWidth = 1.4f, StairTopZ = -238f, StairBottomZ = -227.5f;

        private static Material atlas, concrete, concreteFloor, road, rust;

        [MenuItem("Tools/SheltreSim/Setup M20 Home Site")]
        public static void Setup()
        {
            atlas = Mat("SS_Atlas");
            concrete = Mat("SS_Concrete");
            concreteFloor = Mat("SS_ConcreteFloor");
            road = Mat("SS_RoofTar");
            rust = Mat("SS_CorrugatedRust");

            RemoveOldRegion();
            WireWorldModels();

            Transform site = new GameObject("HomeSite").transform;
            HomeBase home = site.gameObject.AddComponent<HomeBase>();
            home.gameState = Object.FindAnyObjectByType<GameStateManager>();
            GameObject player = GameObject.Find("Player");
            home.inventory = player.GetComponent<SheltreSim.Items.Inventory>();
            site.position = new Vector3(StairX, Ground, StairTopZ);

            BuildYard(site, home);
            BuildStairs(site);
            BuildBunker(site, home);
            MoveFurniture();
            WireSupplyCage(home);
            PlacePlayer(player);

            EditorUtility.SetDirty(home);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("[M20] Home site built: suburban lot, underground bunker, savings and stages wired.");
        }

        private static void RemoveOldRegion()
        {
            string[] names = { "OutdoorArea", "Floor", "Roof", "DoorHinge", "ArtDressing", "ApocalypseButton", "ApocalypseButton_Panel",
                "ApocalypseButton_HazardStripe", "DoorTrim_Lintel", "DoorTrim_PostLeft", "DoorTrim_PostRight",
                "Wall_North", "Wall_East", "Wall_West", "Wall_South_A", "Wall_South_B", "HomeSite" };
            foreach (GameObject root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
                if (names.Contains(root.name)) Object.DestroyImmediate(root);
        }

        private static void WireWorldModels()
        {
            ProceduralWorld world = Object.FindFirstObjectByType<ProceduralWorld>();
            ProceduralWorldAssets a = world.assets;
            a.houseRoofs = Models("house_roof_a", "house_roof_b");
            a.carsClean = Models("car_clean_a", "car_clean_b");
            a.rubble = Models("rubble_a", "rubble_b", "rubble_c");
            a.scrapPiles = Models("scrap_pile_a", "scrap_pile_b");
            a.houseWindow = Model("house_window");
            a.houseWindowBroken = Model("house_window_broken");
            a.houseDoor = Model("house_door");
            a.fenceWood = Model("fence_wood");
            a.hedge = Model("hedge");
            a.streetLamp = Model("street_lamp");
            a.powerPole = Model("power_pole");
            a.mailbox = Model("mailbox");
            a.gardenShed = Model("garden_shed");
            a.busStop = Model("bus_stop");
            a.dumpster = Model("dumpster");
            a.plasterMaterial = Mat("SS_Plaster");
            a.houseCleanMaterials = new[]
            {
                PaintMaterial("HousePaint_Cream", new Color(0.86f, 0.80f, 0.66f)),
                PaintMaterial("HousePaint_White", new Color(0.88f, 0.87f, 0.83f)),
                PaintMaterial("HousePaint_Sage", new Color(0.62f, 0.68f, 0.56f)),
                PaintMaterial("HousePaint_Blue", new Color(0.60f, 0.67f, 0.74f)),
            };
            EditorUtility.SetDirty(world);
        }

        private static Material PaintMaterial(string name, Color color)
        {
            string path = "Assets/Data/World/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Standard")) { color = color };
                material.SetFloat("_Glossiness", 0.15f);
                AssetDatabase.CreateAsset(material, path);
            }
            return material;
        }

        // ---------- Surface ----------

        private static void BuildYard(Transform site, HomeBase home)
        {
            // Covers the terrain cell left open for the stairs, all but the stair opening itself.
            const float slabTop = Ground + 0.02f;
            float[] west = { 64f, 70.6f }, east = { 73.4f, 80f };
            Slab(site, "Yard_W", west[0], west[1], -240f, -224f, slabTop);
            Slab(site, "Yard_E", east[0], east[1], -240f, -224f, slabTop);
            Slab(site, "Yard_S", 70.6f, 73.4f, -240f, StairTopZ, slabTop);
            Slab(site, "Yard_N", 70.6f, 73.4f, -230f, -224f, slabTop);
            home.entranceCover = Slab(site, "EntranceCover", 70.6f, 73.4f, StairTopZ, -230f, slabTop + 0.02f);

            // Gravel drive from the road to the stairs, no collider.
            Box(site, "Driveway", new Vector3(StairX, Ground + 0.03f, -244f), new Vector3(6f, 0.03f, 12f), road, false);

            // The fence: a site boundary, with a gate towards the road. Bombs knock down every fourth section.
            List<GameObject> fallen = new List<GameObject>();
            int counter = 0;
            for (float x = 49.5f; x < 96f; x += 3f)
            {
                if (x < 66f || x > 78f) FenceSection(site, new Vector3(x, Ground, -246f), 0f, fallen, ref counter);
                FenceSection(site, new Vector3(x, Ground, -204f), 0f, fallen, ref counter);
            }
            for (float z = -244.5f; z < -204f; z += 3f)
            {
                FenceSection(site, new Vector3(48f, Ground, z), 90f, fallen, ref counter);
                FenceSection(site, new Vector3(96f, Ground, z), 90f, fallen, ref counter);
            }

            GameObject terminal = Prop(site, "site_terminal", "SiteTerminal", new Vector3(60f, Ground, -239.5f), 0f, new Vector3(0.9f, 1.9f, 0.7f));
            terminal.AddComponent<SiteTerminal>();
            GameObject trailer = Prop(site, "site_trailer", "SiteTrailer", new Vector3(60f, Ground, -235f), 0f, new Vector3(6f, 3f, 2.5f));
            GameObject excavator = Prop(site, "excavator", "Excavator", new Vector3(88f, Ground, -238f), -25f, new Vector3(2.6f, 3.4f, 5f));
            fallen.Add(trailer);

            Transform excavated = Group(site, "Stage_Excavation");
            Prop(excavated, "dirt_pile", "DirtPile", new Vector3(86f, Ground, -224f), 30f, new Vector3(4f, 1.8f, 4f));
            Prop(excavated, "dirt_pile", "DirtPile2", new Vector3(56f, Ground, -220f), 130f, new Vector3(4f, 1.8f, 4f));

            Transform shell = Group(site, "Stage_Shell");
            GameObject mixer = Prop(shell, "concrete_mixer", "Mixer", new Vector3(79f, Ground, -241f), 40f, new Vector3(1.3f, 1.6f, 1.3f));
            GameObject scaffold = Prop(shell, "scaffold", "Scaffold", new Vector3(66f, Ground, -232f), 90f, new Vector3(3f, 4f, 1.2f));

            // The way down: walls and roof of the stair head, up on the surface.
            Transform stairs = Group(site, "Stage_Stairs");
            bool hasHut = Model("stair_hut") != null;
            GameObject hut = hasHut ? Visual(stairs, "stair_hut", new Vector3(StairX, Ground, -231f), 0f) : null;
            if (hut != null) hut.name = "StairHut";
            foreach (int side in new[] { -1, 1 })
                Box(stairs, "HutWall", new Vector3(StairX + side * 1.8f, Ground + 1.55f, -234f), new Vector3(0.8f, 3.1f, 8f), concrete, true, !hasHut);
            Box(stairs, "HutBack", new Vector3(StairX, Ground + 1.55f, -230.15f), new Vector3(4.4f, 3.1f, 0.3f), concrete, true, !hasHut);
            Box(stairs, "HutRoof", new Vector3(StairX, Ground + 2.8f, -231.5f), new Vector3(4.4f, 0.6f, 3f), concrete, true, !hasHut);

            Transform lifeSupport = Group(site, "Stage_LifeSupport");
            Visual(lifeSupport, "bunker_vent", new Vector3(77f, Ground, -216f), 0f);
            Visual(lifeSupport, "bunker_vent", new Vector3(67f, Ground, -216f), 90f);

            home.stageVisuals = new[] { excavated.gameObject, shell.gameObject, stairs.gameObject, lifeSupport.gameObject };
            home.removedWhenComplete = new[] { mixer, scaffold, excavator };
            home.removedByBombs = fallen.ToArray();
        }

        private static void FenceSection(Transform site, Vector3 position, float yaw, List<GameObject> fallen, ref int counter)
        {
            GameObject section = new GameObject("FenceSection");
            section.transform.SetParent(site, true);
            section.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            if (Model("fence_chain") != null) Attach(section.transform, "fence_chain");
            else Box(section.transform, "Mesh", position + Vector3.up * 1.1f, new Vector3(yaw == 0f ? 3f : 0.05f, 2.2f, yaw == 0f ? 0.05f : 3f), rust, false);
            BoxCollider box = section.AddComponent<BoxCollider>();
            box.center = Vector3.up * 1.1f;
            box.size = new Vector3(3f, 2.2f, 0.1f);
            if (counter++ % 4 == 0) fallen.Add(section);
        }

        // ---------- Stairs ----------

        private static void BuildStairs(Transform site)
        {
            Transform shaft = Group(site, "StairShaft");
            float run = StairBottomZ - StairTopZ;
            float top = Ground + 0.02f;
            float length = Mathf.Sqrt(run * run + top * top);
            float angle = Mathf.Atan2(top, run) * Mathf.Rad2Deg;

            // Movement rides one smooth ramp: steps that deep would catch a CharacterController, and raiders.
            GameObject ramp = new GameObject("StairRamp");
            ramp.transform.SetParent(shaft, true);
            ramp.transform.SetPositionAndRotation(
                new Vector3(StairX, top * 0.5f, (StairTopZ + StairBottomZ) * 0.5f) - Quaternion.Euler(angle, 0f, 0f) * Vector3.up * 0.15f,
                Quaternion.Euler(angle, 0f, 0f));
            BoxCollider rampCollider = ramp.AddComponent<BoxCollider>();
            rampCollider.size = new Vector3(StairHalfWidth * 2f, 0.3f, length);

            const int steps = 35;
            bool hasStep = Model("bunker_stair_step") != null;
            for (int i = 0; i < steps; i++)
            {
                float z = StairTopZ + run * (i + 0.5f) / steps;
                float y = top - top * (i + 1f) / steps;
                if (hasStep) Visual(shaft, "bunker_stair_step", new Vector3(StairX, y, z), 0f);
                else Box(shaft, "Step", new Vector3(StairX, y - 0.09f, z), new Vector3(2.8f, 0.18f, run / steps), concrete, false);
            }

            foreach (int side in new[] { -1, 1 })
                Box(shaft, "ShaftWall", new Vector3(StairX + side * (StairHalfWidth + 0.2f), Ground * 0.5f - 0.1f, (StairTopZ - 223.6f) * 0.5f),
                    new Vector3(0.4f, Ground + 0.2f, -223.6f - StairTopZ), concrete);
            // The tunnel at the bottom has a roof; the stair head above it opens to the sky.
            Box(shaft, "TunnelRoof", new Vector3(StairX, 4.15f, -227f), new Vector3(3.6f, 0.3f, 6.2f), concrete);
            // Closes the gap between the tunnel roof and the yard, behind the stair head.
            Box(shaft, "ShaftEnd", new Vector3(StairX, 5.1f, -230.1f), new Vector3(2.8f, 1.8f, 0.2f), concrete);
            Box(shaft, "Landing", new Vector3(StairX, -0.1f, (StairBottomZ - 223.6f) * 0.5f), new Vector3(2.8f, 0.2f, -223.6f - StairBottomZ), concreteFloor);

            AddLight(shaft, new Vector3(StairX, 3.2f, -226f), 10f, 2f, false);
            Attached(shaft, "bunker_bulkhead_lamp", new Vector3(StairX - StairHalfWidth, 2.6f, -225.5f), 90f);
            AddLight(shaft, new Vector3(StairX, 3.6f, -232f), 8f, 1.2f, false);
        }

        // ---------- Bunker ----------

        private static void BuildBunker(Transform site, HomeBase home)
        {
            Transform room = Group(site, "Bunker");
            float cx = (RoomMinX + RoomMaxX) * 0.5f, cz = (RoomMinZ + RoomMaxZ) * 0.5f;
            float w = RoomMaxX - RoomMinX, d = RoomMaxZ - RoomMinZ;

            Box(room, "Floor", new Vector3(cx, -0.2f, cz), new Vector3(w + 0.8f, 0.4f, d + 0.8f), concreteFloor);
            Box(room, "Ceiling", new Vector3(cx, RoomHeight + 0.2f, cz), new Vector3(w + 0.8f, 0.4f, d + 0.8f), concrete);
            // Leave an 1.8 m north-wing opening; its plug stays in place until a wing fit-out is bought.
            const float wingDoorX = 68f, wingDoorWidth = 1.8f;
            float northWallZ = RoomMaxZ + 0.2f;
            Box(room, "Wall_N_West", new Vector3((RoomMinX + wingDoorX - wingDoorWidth * 0.5f) * 0.5f,
                RoomHeight * 0.5f, northWallZ), new Vector3(wingDoorX - wingDoorWidth * 0.5f - RoomMinX, RoomHeight, 0.4f), concrete);
            Box(room, "Wall_N_East", new Vector3((RoomMaxX + wingDoorX + wingDoorWidth * 0.5f) * 0.5f,
                RoomHeight * 0.5f, northWallZ), new Vector3(RoomMaxX - wingDoorX - wingDoorWidth * 0.5f, RoomHeight, 0.4f), concrete);
            Box(room, "Wall_N_Lintel", new Vector3(wingDoorX, 2.65f, northWallZ), new Vector3(wingDoorWidth, 0.7f, 0.4f), concrete);
            home.northWingPlug = Box(room, "NorthWingPlug", new Vector3(wingDoorX, 1.15f, northWallZ),
                new Vector3(wingDoorWidth, 2.3f, 0.4f), concrete);
            Box(room, "Wall_W", new Vector3(RoomMinX - 0.2f, RoomHeight * 0.5f, cz), new Vector3(0.4f, RoomHeight, d), concrete);

            // South wall: the 2.4 m blast door at the foot of the stairs.
            float segment = (w + 0.8f - 2.4f) * 0.5f;
            foreach (int side in new[] { -1, 1 })
                Box(room, "Wall_S", new Vector3(StairX + side * (1.2f + segment * 0.5f), RoomHeight * 0.5f, RoomMinZ - 0.2f), new Vector3(segment, RoomHeight, 0.4f), concrete);
            Box(room, "DoorLintel", new Vector3(StairX, 2.65f, RoomMinZ - 0.2f), new Vector3(2.4f, 0.7f, 0.4f), concrete);
            Visual(room, "blast_door_frame", new Vector3(StairX, 0f, RoomMinZ - 0.2f), 0f);
            BuildDoor(room, home);

            // East wall: a gap for the optional annex, plugged until bought.
            const float gapZ = -217.5f;
            float northSpan = RoomMaxZ - (gapZ + 1.2f), southSpan = (gapZ - 1.2f) - RoomMinZ;
            Box(room, "Wall_E", new Vector3(RoomMaxX + 0.2f, RoomHeight * 0.5f, gapZ + 1.2f + northSpan * 0.5f), new Vector3(0.4f, RoomHeight, northSpan), concrete);
            Box(room, "Wall_E", new Vector3(RoomMaxX + 0.2f, RoomHeight * 0.5f, RoomMinZ + southSpan * 0.5f), new Vector3(0.4f, RoomHeight, southSpan), concrete);
            Box(room, "AnnexLintel", new Vector3(RoomMaxX + 0.2f, 2.65f, gapZ), new Vector3(0.4f, 0.7f, 2.4f), concrete);
            home.annexPlug = Box(room, "AnnexPlug", new Vector3(RoomMaxX + 0.2f, 1.15f, gapZ), new Vector3(0.4f, 2.3f, 2.4f), concrete);

            Transform annex = Group(room, "Annex");
            const float ax0 = RoomMaxX + 0.4f, ax1 = ax0 + 8f, az0 = gapZ - 4f, az1 = gapZ + 4f;
            float acx = (ax0 + ax1) * 0.5f;
            Box(annex, "Floor", new Vector3(acx, -0.2f, gapZ), new Vector3(8.4f, 0.4f, 8.8f), concreteFloor);
            Box(annex, "Ceiling", new Vector3(acx, RoomHeight + 0.2f, gapZ), new Vector3(8.4f, 0.4f, 8.8f), concrete);
            Box(annex, "Wall_E", new Vector3(ax1 + 0.2f, RoomHeight * 0.5f, gapZ), new Vector3(0.4f, RoomHeight, 8.8f), concrete);
            // Vault access uses a measured 1.8 m clear opening centered on the annex north wall.
            const float vaultDoorX = 83f, vaultDoorWidth = 1.8f;
            float annexNorthWallZ = az1 + 0.2f;
            Box(annex, "Wall_N_West", new Vector3((ax0 + vaultDoorX - vaultDoorWidth * 0.5f) * 0.5f,
                RoomHeight * 0.5f, annexNorthWallZ), new Vector3(vaultDoorX - vaultDoorWidth * 0.5f - ax0, RoomHeight, 0.4f), concrete);
            Box(annex, "Wall_N_East", new Vector3((ax1 + vaultDoorX + vaultDoorWidth * 0.5f) * 0.5f,
                RoomHeight * 0.5f, annexNorthWallZ), new Vector3(ax1 - vaultDoorX - vaultDoorWidth * 0.5f, RoomHeight, 0.4f), concrete);
            Box(annex, "Wall_N_Lintel", new Vector3(vaultDoorX, 2.65f, annexNorthWallZ), new Vector3(vaultDoorWidth, 0.7f, 0.4f), concrete);
            home.vaultPlug = Box(annex, "VaultPlug", new Vector3(vaultDoorX, 1.15f, annexNorthWallZ),
                new Vector3(vaultDoorWidth, 2.3f, 0.4f), concrete);
            Box(annex, "Wall_S", new Vector3(acx, RoomHeight * 0.5f, az0 - 0.2f), new Vector3(8.4f, RoomHeight, 0.4f), concrete);
            AddLight(annex, new Vector3(acx, 2.6f, gapZ), 9f, 1.2f, false);
            Attached(annex, "bunker_lamp", new Vector3(acx, RoomHeight, gapZ), 0f);
            home.annexRoom = annex.gameObject;

            BuildNorthWing(room, home);
            BuildVault(room, home);

            // Lamps and dressing.
            Vector3[] lamps = { new Vector3(68.5f, 0f, -220f), new Vector3(75.5f, 0f, -220f), new Vector3(68.5f, 0f, -214.5f), new Vector3(75.5f, 0f, -214.5f) };
            for (int i = 0; i < lamps.Length; i++)
            {
                Attached(room, "bunker_lamp", lamps[i] + Vector3.up * RoomHeight, 0f);
                AddLight(room, lamps[i] + Vector3.up * 2.4f, 10f, 1.25f, i == 0);
            }
            Attached(room, "bunker_pipes", new Vector3(cx, 0f, RoomMaxZ), 0f);
            Attached(room, "bunker_pipes", new Vector3(RoomMinX, 0f, cz), -90f);
            Attached(room, "bunker_shelf", new Vector3(RoomMinX, 0f, -220f), -90f);
            Attached(room, "bunker_shelf", new Vector3(RoomMinX, 0f, -214.5f), -90f);
            Prop(room, "bunker_locker", "Lockers", new Vector3(77.8f, 0f, -222.8f), 90f, new Vector3(1.2f, 2f, 0.5f));

            GameObject console = GameObject.Find("ExpeditionConsole");
            if (console != null) console.transform.position = new Vector3(RoomMinX + 0.6f, 0.65f, cz);
        }

        private static void BuildDoor(Transform room, HomeBase home)
        {
            const float leaf = 2.3f, leafHeight = 2.25f;
            GameObject hinge = new GameObject("DoorHinge");
            hinge.transform.SetParent(room, true);
            hinge.transform.position = new Vector3(StairX - leaf * 0.5f, leafHeight * 0.5f, RoomMinZ - 0.2f);
            GameObject door = new GameObject("Door");
            door.transform.SetParent(hinge.transform, false);
            door.transform.localPosition = new Vector3(leaf * 0.5f, 0f, 0f);
            BoxCollider collider = door.AddComponent<BoxCollider>();
            collider.size = new Vector3(leaf, leafHeight, 0.18f);
            InteractableDoor script = door.AddComponent<InteractableDoor>();
            script.doorVisual = hinge.transform;
            home.mainBlastDoor = script;
            if (Model("blast_door_leaf") != null)
            {
                Attach(door.transform, "blast_door_leaf").transform.localPosition = new Vector3(0f, -leafHeight * 0.5f, 0f);
            }
            else
            {
                GameObject mesh = Box(door.transform, "Mesh", door.transform.position, new Vector3(leaf, leafHeight, 0.18f), rust, false);
                mesh.transform.localPosition = Vector3.zero;
            }
        }

        private static void BuildNorthWing(Transform bunker, HomeBase home)
        {
            const float x0 = 66f, x1 = 76f, z0 = -211.2f, z1 = -203.2f;
            Transform wing = Group(bunker, "NorthWing");
            Vector3 center = new Vector3((x0 + x1) * .5f, 0f, (z0 + z1) * .5f);
            Box(wing, "Floor", center + Vector3.down * .2f, new Vector3(x1 - x0 + .4f, .4f, z1 - z0 + .4f), concreteFloor);
            Box(wing, "Ceiling", center + Vector3.up * (RoomHeight + .2f), new Vector3(x1 - x0 + .4f, .4f, z1 - z0 + .4f), concrete);
            Box(wing, "Wall_W", new Vector3(x0 - .2f, RoomHeight * .5f, center.z), new Vector3(.4f, RoomHeight, z1 - z0), concrete);
            Box(wing, "Wall_E", new Vector3(x1 + .2f, RoomHeight * .5f, center.z), new Vector3(.4f, RoomHeight, z1 - z0), concrete);
            Box(wing, "Wall_N", new Vector3(center.x, RoomHeight * .5f, z1 + .2f), new Vector3(x1 - x0 + .8f, RoomHeight, .4f), concrete);
            Transform workshop = Group(wing, "WorkshopFitout");
            Transform hydro = Group(wing, "HydroponicsFitout");
            home.northWingShell = wing.gameObject;
            home.workshopWing = workshop.gameObject;
            home.hydroWing = hydro.gameObject;
            wing.gameObject.SetActive(false);
            workshop.gameObject.SetActive(false);
            hydro.gameObject.SetActive(false);
        }

        private static void BuildVault(Transform bunker, HomeBase home)
        {
            const float x0 = 80f, x1 = 86f, z0 = -213.1f, z1 = -207.1f;
            Transform vault = Group(bunker, "VaultRoom");
            float cx = (x0 + x1) * .5f, cz = (z0 + z1) * .5f;
            Box(vault, "Floor", new Vector3(cx, -.2f, cz), new Vector3(x1 - x0 + .4f, .4f, z1 - z0 + .4f), concreteFloor);
            Box(vault, "Ceiling", new Vector3(cx, RoomHeight + .2f, cz), new Vector3(x1 - x0 + .4f, .4f, z1 - z0 + .4f), concrete);
            Box(vault, "Wall_W", new Vector3(x0 - .2f, RoomHeight * .5f, cz), new Vector3(.4f, RoomHeight, z1 - z0), concrete);
            Box(vault, "Wall_E", new Vector3(x1 + .2f, RoomHeight * .5f, cz), new Vector3(.4f, RoomHeight, z1 - z0), concrete);
            Box(vault, "Wall_N", new Vector3(cx, RoomHeight * .5f, z1 + .2f), new Vector3(x1 - x0 + .8f, RoomHeight, .4f), concrete);
            const float doorX = 83f, doorWidth = 1.8f;
            Box(vault, "Wall_S_West", new Vector3((x0 + doorX - doorWidth * .5f) * .5f, RoomHeight * .5f, z0 - .2f),
                new Vector3(doorX - doorWidth * .5f - x0, RoomHeight, .4f), concrete);
            Box(vault, "Wall_S_East", new Vector3((x1 + doorX + doorWidth * .5f) * .5f, RoomHeight * .5f, z0 - .2f),
                new Vector3(x1 - doorX - doorWidth * .5f, RoomHeight, .4f), concrete);
            Box(vault, "DoorLintel", new Vector3(doorX, 2.65f, z0 - .2f), new Vector3(doorWidth, .7f, .4f), concrete);

            GameObject hinge = new GameObject("VaultDoorHinge");
            hinge.transform.SetParent(vault, true);
            hinge.transform.position = new Vector3(doorX - doorWidth * .5f, 1.15f, z0 - .2f);
            GameObject door = new GameObject("VaultDoor");
            door.transform.SetParent(hinge.transform, false);
            door.transform.localPosition = new Vector3(doorWidth * .5f, 0f, 0f);
            BoxCollider collider = door.AddComponent<BoxCollider>();
            collider.size = new Vector3(doorWidth, 2.3f, .18f);
            InteractableDoor interactable = door.AddComponent<InteractableDoor>();
            interactable.doorVisual = hinge.transform;
            Box(door.transform, "DoorLeaf", door.transform.position,
                new Vector3(doorWidth, 2.3f, .18f), rust, false);
            home.vaultRoom = vault.gameObject;
            vault.gameObject.SetActive(false);
        }

        private static void MoveFurniture()
        {
            foreach (PlacedPiece piece in Object.FindObjectsByType<PlacedPiece>(FindObjectsInactive.Include))
            {
                Transform t = piece.transform.root;
                switch (piece.pieceId)
                {
                    case "storage_crate": t.SetPositionAndRotation(new Vector3(67f, 0.5f, -222.4f), Quaternion.identity); break;
                    default: Object.DestroyImmediate(t.gameObject); break;
                }
            }
        }

        private static void WireSupplyCage(HomeBase home)
        {
            GameObject cage = Prop(home.transform, "bunker_locker", "Supply Cage",
                new Vector3(67f, 0f, -220.6f), -90f, new Vector3(1.2f, 2f, .5f));
            home.supplyCage = cage.AddComponent<StorageCratePiece>();
            cage.AddComponent<SupplyCageGuard>();
        }

        private static void PlacePlayer(GameObject player)
        {
            // Inside the gate, looking at the laptop on the table with the trailer behind it.
            player.transform.SetPositionAndRotation(new Vector3(60f, Ground + 0.1f, -242f), Quaternion.identity);
        }

        // ---------- Helpers ----------

        private static Material Mat(string name) => AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/" + name + ".mat");
        private static GameObject Model(string id) => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Models/" + id + ".fbx");
        private static GameObject[] Models(params string[] ids) => ids.Select(Model).Where(m => m != null).ToArray();

        private static Transform Group(Transform parent, string name)
        {
            Transform group = new GameObject(name).transform;
            group.SetParent(parent, false);
            group.position = parent.position;
            return group;
        }

        private static GameObject Slab(Transform parent, string name, float x0, float x1, float z0, float z1, float top)
        {
            return Box(parent, name, new Vector3((x0 + x1) * 0.5f, top - 0.2f, (z0 + z1) * 0.5f), new Vector3(x1 - x0, 0.4f, z1 - z0), concreteFloor);
        }

        /// <summary>World-space box. A collider can be kept with its renderer hidden, where a model shows instead.</summary>
        private static GameObject Box(Transform parent, string name, Vector3 position, Vector3 size, Material material, bool solid = true, bool visible = true)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, true);
            box.transform.position = position;
            box.transform.rotation = Quaternion.identity;
            box.transform.localScale = size;
            if (material != null) box.GetComponent<Renderer>().sharedMaterial = material;
            if (!solid) Object.DestroyImmediate(box.GetComponent<Collider>());
            if (!visible) Object.DestroyImmediate(box.GetComponent<Renderer>());
            return box;
        }

        private static GameObject Attach(Transform parent, string model)
        {
            GameObject instance = Object.Instantiate(Model(model), parent, false);
            instance.name = "Visual";
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true)) renderer.sharedMaterial = atlas;
            return instance;
        }

        /// <summary>A model with no collider. Quietly nothing if the model doesn't exist, so art can arrive later.</summary>
        private static GameObject Visual(Transform parent, string model, Vector3 position, float yaw)
        {
            if (Model(model) == null) { Debug.LogWarning("[M20] Missing model " + model); return null; }
            GameObject root = new GameObject(model);
            root.transform.SetParent(parent, true);
            root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            Attach(root.transform, model);
            return root;
        }

        private static void Attached(Transform parent, string model, Vector3 position, float yaw) => Visual(parent, model, position, yaw);

        /// <summary>A model with a box collider sized by hand, and a plain block if the model is missing.</summary>
        private static GameObject Prop(Transform parent, string model, string name, Vector3 position, float yaw, Vector3 size)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(parent, true);
            root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            if (Model(model) != null) Attach(root.transform, model);
            else
            {
                Debug.LogWarning("[M20] Missing model " + model);
                GameObject mesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(mesh.GetComponent<Collider>());
                mesh.transform.SetParent(root.transform, false);
                mesh.transform.localPosition = Vector3.up * size.y * 0.5f;
                mesh.transform.localScale = size;
                mesh.GetComponent<Renderer>().sharedMaterial = rust;
            }
            BoxCollider box = root.AddComponent<BoxCollider>();
            box.center = Vector3.up * size.y * 0.5f;
            box.size = size;
            return root;
        }

        private static void AddLight(Transform parent, Vector3 position, float range, float intensity, bool shadows)
        {
            GameObject lightObject = new GameObject("Light");
            lightObject.transform.SetParent(parent, true);
            lightObject.transform.position = position;
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.84f, 0.62f);
            light.range = range;
            light.intensity = intensity;
            light.shadows = shadows ? LightShadows.Soft : LightShadows.None;
        }
    }
}
