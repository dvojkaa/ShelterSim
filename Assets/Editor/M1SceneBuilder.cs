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
        private const float DoorGap = 1.2f;

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

        private static void BuildRoom()
        {
            float half = RoomSize / 2f;

            CreateBlock("Floor", new Vector3(0f, -WallThickness / 2f, 0f), new Vector3(RoomSize, WallThickness, RoomSize));

            CreateBlock("Wall_North", new Vector3(0f, WallHeight / 2f, half), new Vector3(RoomSize, WallHeight, WallThickness));
            CreateBlock("Wall_East", new Vector3(half, WallHeight / 2f, 0f), new Vector3(WallThickness, WallHeight, RoomSize));
            CreateBlock("Wall_West", new Vector3(-half, WallHeight / 2f, 0f), new Vector3(WallThickness, WallHeight, RoomSize));

            // South wall split around a doorway gap centered on x = 0.
            float sideSpan = (RoomSize - DoorGap) / 2f;
            float sideCenterOffset = DoorGap / 2f + sideSpan / 2f;
            CreateBlock("Wall_South_A", new Vector3(-sideCenterOffset, WallHeight / 2f, -half), new Vector3(sideSpan, WallHeight, WallThickness));
            CreateBlock("Wall_South_B", new Vector3(sideCenterOffset, WallHeight / 2f, -half), new Vector3(sideSpan, WallHeight, WallThickness));

            GameObject door = GameObject.CreatePrimitive(PrimitiveType.Cube);
            door.name = "Door";
            door.transform.localScale = new Vector3(1.0f, 2.2f, 0.1f);
            door.transform.position = new Vector3(0f, 1.1f, -half);

            InteractableDoor doorScript = door.AddComponent<InteractableDoor>();
            doorScript.doorVisual = door.transform;
        }

        private static GameObject CreateBlock(string name, Vector3 position, Vector3 scale)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.position = position;
            block.transform.localScale = scale;
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
