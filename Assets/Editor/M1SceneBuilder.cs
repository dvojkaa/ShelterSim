using SheltreSim.Interaction;
using SheltreSim.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SheltreSim.EditorTools
{
    /// <summary>Generates the M1 "bunker shell" scene: a grey-boxed room, the player rig, and a test door.</summary>
    public static class M1SceneBuilder
    {
        private const float RoomSize = 6f;
        private const float WallHeight = 3f;
        private const float WallThickness = 0.2f;
        /// <summary>
        /// Width of the gap left in the south wall for the doorway. Was 1.2m for M1-M11,
        /// which was sized for the player alone (CharacterController radius 0.35 — needs
        /// ~0.86m of clear width including its 0.08 skinWidth) and left no room for anything
        /// bulkier. M12 made raiders solid (EnemyAI drives a CharacterController now, radius
        /// 0.5 → needs ~1.16m) and gave them a reason to walk through this doorway, which
        /// turned the old number into a hard blocker: measured on the built scene, the open
        /// door's bottleneck clear band was 1.04m — the swung-open leaf eats ~0.15m of the
        /// gap — so a raider physically could not fit through the bunker's one entrance, and
        /// "enemies can come in through the door" would have been false no matter how
        /// correct InteractableDoor.ForceOpen and EnemyAI.TryBreakNearbyDoors were.
        /// 1.8m leaves a measured 1.65m bottleneck: ~0.25m of slack per side for a raider
        /// that steers in a straight line with no pathfinding to correct its approach angle.
        /// </summary>
        private const float DoorGap = 1.8f;

        [MenuItem("Tools/SheltreSim/Build M1 Bunker Scene")]
        public static void BuildScene()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildRoom();
            BuildPlayer();
            EnsureDirectionalLight();

            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
            {
                AssetDatabase.CreateFolder("Assets", "Scenes");
            }

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), "Assets/Scenes/M1_BunkerShell.unity");
            Debug.Log("M1 bunker shell scene built: Assets/Scenes/M1_BunkerShell.unity");
        }

        private static readonly Color FloorColor = new Color(0.5f, 0.5f, 0.5f);
        private static readonly Color WallColor = new Color(0.35f, 0.35f, 0.38f);
        private static readonly Color DoorColor = new Color(0.45f, 0.28f, 0.14f);

        private static void BuildRoom()
        {
            float half = RoomSize / 2f;

            CreateBlock("Floor", new Vector3(0f, -WallThickness / 2f, 0f), new Vector3(RoomSize, WallThickness, RoomSize), FloorColor);

            CreateBlock("Wall_North", new Vector3(0f, WallHeight / 2f, half), new Vector3(RoomSize, WallHeight, WallThickness), WallColor);
            CreateBlock("Wall_East", new Vector3(half, WallHeight / 2f, 0f), new Vector3(WallThickness, WallHeight, RoomSize), WallColor);
            CreateBlock("Wall_West", new Vector3(-half, WallHeight / 2f, 0f), new Vector3(WallThickness, WallHeight, RoomSize), WallColor);

            // South wall split around a doorway gap centered on x = 0.
            float sideSpan = (RoomSize - DoorGap) / 2f;
            float sideCenterOffset = DoorGap / 2f + sideSpan / 2f;
            CreateBlock("Wall_South_A", new Vector3(-sideCenterOffset, WallHeight / 2f, -half), new Vector3(sideSpan, WallHeight, WallThickness), WallColor);
            CreateBlock("Wall_South_B", new Vector3(sideCenterOffset, WallHeight / 2f, -half), new Vector3(sideSpan, WallHeight, WallThickness), WallColor);

            // Hinge-based door: rotating the door's OWN transform around its own center just
            // spins it in place (a 1m x 0.1m slab becomes a 0.1m x 1m slab at the same spot —
            // it still blocks the gap, just along the other axis). A real door needs to pivot
            // around one vertical edge. "DoorHinge" sits at the left edge of the gap; the
            // visible door mesh is its child, offset by half the door's width, so rotating the
            // HINGE swings the mesh out of the doorway instead of spinning it on the spot.
            // Kept 0.2m narrower than DoorGap so the closed door reads as a door in a frame
            // rather than a perfectly flush plug (unchanged proportion from the original
            // 1.2m gap / 1.0m leaf).
            const float doorWidth = DoorGap - 0.2f;
            GameObject doorHinge = new GameObject("DoorHinge");
            doorHinge.transform.position = new Vector3(-doorWidth / 2f, 1.1f, -half);

            GameObject door = GameObject.CreatePrimitive(PrimitiveType.Cube);
            door.name = "Door";
            door.transform.SetParent(doorHinge.transform, false);
            door.transform.localScale = new Vector3(doorWidth, 2.2f, 0.1f);
            door.transform.localPosition = new Vector3(doorWidth / 2f, 0f, 0f);
            EditorMaterialUtility.Apply(door, DoorColor);

            InteractableDoor doorScript = door.AddComponent<InteractableDoor>();
            doorScript.doorVisual = doorHinge.transform;
        }

        private static GameObject CreateBlock(string name, Vector3 position, Vector3 scale, Color color)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.position = position;
            block.transform.localScale = scale;
            EditorMaterialUtility.Apply(block, color);
            return block;
        }

        private static void BuildPlayer()
        {
            GameObject player = new GameObject("Player");
            player.transform.position = new Vector3(0f, 1f, 1.5f);

            CharacterController controller = player.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.radius = 0.35f;

            FirstPersonController firstPersonController = player.AddComponent<FirstPersonController>();
            PlayerWiring wiring = player.AddComponent<PlayerWiring>();

            GameObject cameraPivot = new GameObject("CameraPivot");
            cameraPivot.transform.SetParent(player.transform, false);
            cameraPivot.transform.localPosition = new Vector3(0f, 1.6f, 0f);

            cameraPivot.AddComponent<Camera>();
            cameraPivot.AddComponent<AudioListener>();
            PlayerInteractor interactor = cameraPivot.AddComponent<PlayerInteractor>();

            firstPersonController.cameraPivot = cameraPivot.transform;
            wiring.firstPersonController = firstPersonController;
            wiring.interactor = interactor;
        }

        private static void EnsureDirectionalLight()
        {
            foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type == LightType.Directional)
                {
                    return;
                }
            }

            GameObject lightObject = new GameObject("Directional Light");
            Light directionalLight = lightObject.AddComponent<Light>();
            directionalLight.type = LightType.Directional;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }
    }
}
