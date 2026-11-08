using SheltreSim.Building;
using SheltreSim.Player;
using SheltreSim.Survival;
using SheltreSim.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SheltreSim.World
{
    /// <summary>Local atlas generated from the same seed; map viewing never creates world chunks.</summary>
    public sealed class WorldMapUI : MonoBehaviour
    {
        private InputAction toggle, cancel;
        private Canvas canvas;
        private RawImage map;
        private Text heading, position;
        private RectTransform home;
        private Texture2D texture;
        private FirstPersonController player;
        private Vector2Int center;
        private int shownSeed;
        private bool open;
        private bool closeRequested;
        private static readonly Color[] Colors = {
            new Color(.42f,.36f,.27f), new Color(.15f,.32f,.18f), new Color(.74f,.43f,.22f),
            new Color(.80f,.87f,.91f), new Color(.20f,.34f,.31f), new Color(.40f,.44f,.49f)
        };

        private void Awake()
        {
            toggle = new InputAction("WorldMap", InputActionType.Button, "<Keyboard>/m");
            cancel = new InputAction("CloseWorldMap", InputActionType.Button, "<Keyboard>/escape");
        }
        private void OnEnable()
        {
            toggle.Enable(); cancel.Enable();
            toggle.performed += OnToggle; cancel.performed += OnCancel;
        }
        private void OnDisable()
        {
            Close();
            toggle.performed -= OnToggle; cancel.performed -= OnCancel;
            toggle.Disable(); cancel.Disable();
        }
        private void OnToggle(InputAction.CallbackContext _) { if (open) Close(); else Open(); }
        private void OnCancel(InputAction.CallbackContext _) { if (open) closeRequested = true; }

        public void Open()
        {
            player = FindFirstObjectByType<FirstPersonController>();
            if (player == null || (player.GetComponent<SurvivalStats>()?.IsDead ?? false) ||
                (player.GetComponentInChildren<BuildModeController>()?.IsBuildModeActive ?? false)) return;
            if (!ModalUI.TryOpen(this, Close)) return;
            if (canvas == null) BuildUI();
            open = true;
            canvas.gameObject.SetActive(true);
            Refresh();
        }

        public void Close()
        {
            if (!open) return;
            open = false;
            closeRequested = false;
            if (canvas != null) canvas.gameObject.SetActive(false);
            ModalUI.Close(this);
        }

        private void BuildUI()
        {
            canvas = GameUI.CreateCanvas(transform, "WorldMapCanvas", 120, true);
            GameUI.Backdrop(canvas.transform, "Backdrop");
            var panel = GameUI.Panel(canvas.transform, "Atlas", Vector2.one * .5f, Vector2.one * .5f,
                Vector2.zero, new Vector2(920, 1000), GameUI.PanelColor).transform;
            heading = GameUI.Label(panel, "Heading", "WILDERNESS ATLAS", new Vector2(.5f,1), new Vector2(.5f,1),
                new Vector2(0,-22), new Vector2(850,42), 25, GameUI.Accent, TextAnchor.MiddleCenter);
            var obj = new GameObject("Map", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            obj.transform.SetParent(panel, false);
            map = obj.GetComponent<RawImage>();
            map.rectTransform.anchorMin = map.rectTransform.anchorMax = map.rectTransform.pivot = Vector2.one * .5f;
            map.rectTransform.anchoredPosition = new Vector2(0,25);
            map.rectTransform.sizeDelta = new Vector2(800,800);
            map.raycastTarget = false;
            texture = new Texture2D(128,128,TextureFormat.RGB24,false) { filterMode = FilterMode.Point, name = "LocalAtlas" };
            map.texture = texture;
            GameUI.Label(map.transform, "Player", "+", Vector2.one*.5f, Vector2.one*.5f,
                Vector2.zero,new Vector2(28,28),24,Color.yellow,TextAnchor.MiddleCenter);
            home = GameUI.Label(map.transform, "Bunker", "B", Vector2.one*.5f,Vector2.one*.5f,
                Vector2.zero,new Vector2(28,28),24,GameUI.Accent,TextAnchor.MiddleCenter).rectTransform;
            position = GameUI.Label(panel,"Position","",new Vector2(.5f,0),new Vector2(.5f,0),
                new Vector2(0,54),new Vector2(850,120),18,Color.white,TextAnchor.MiddleCenter);
            GameUI.Button(panel,"Close","CLOSE  [M / ESC]",new Vector2(.5f,0),new Vector2(.5f,0),
                new Vector2(0,12),new Vector2(270,38),Close);
        }

        private void Refresh()
        {
            var world = ProceduralWorld.Active;
            if (world == null) return;
            center = WorldLayout.Coordinate(player.transform.position);
            shownSeed = world.Seed;
            const float span = 1536f;
            Vector3 p = player.transform.position;
            for (int z=0;z<128;z++) for(int x=0;x<128;x++)
            {
                float wx=p.x+(x/127f-.5f)*span;
                float wz=p.z+(z/127f-.5f)*span;
                Color color=Colors[WorldLayout.BiomeIndexAt(wx,wz,world.Seed)];
                if(WorldLayout.IsTownChunk(WorldLayout.Coordinate(new Vector3(wx,0,wz)),world.Seed)) color=new Color(.70f,.69f,.61f);
                if(WorldLayout.IsRoad(wx,wz,2f)||WorldLayout.IsConnectorRoad(wx,wz,world.Seed,2f)) color=new Color(.13f,.15f,.16f);
                if(WorldLayout.IsHomePlot(wx,wz)) color=new Color(.38f,.41f,.30f);
                texture.SetPixel(x,z,color);
            }
            texture.Apply(false);
            Vector2 homeOffset = new Vector2(-p.x,-p.z)/span*800f;
            home.gameObject.SetActive(Mathf.Abs(homeOffset.x)<385 && Mathf.Abs(homeOffset.y)<385);
            home.anchoredPosition=homeOffset;
            int biome=WorldLayout.BiomeIndexAt(p.x,p.z,world.Seed);
            var atmosphere=world.GetComponent<WorldAtmosphere>();
            heading.text="WILDERNESS ATLAS  /  "+WorldLayout.BiomeNames[biome]+(atmosphere!=null?$"  {atmosphere.Hour:00}:00 {atmosphere.WeatherName}":"");
            position.text=$"NORTH ↑   /   YELLOW + YOU   /   GREEN B BUNKER\nSeed {world.Seed}   |   X {p.x:0}   Z {p.z:0}   |   Bunker {new Vector2(p.x,p.z).magnitude:0} m\nLocal view 1.5 km • Pale districts: towns • Dark lines: roads\nTerrain ahead is predicted from the seed; undiscovered loot is not shown.";
        }

        private void LateUpdate()
        {
            // Keep ownership through input dispatch so Escape cannot also open PauseMenu.
            if (closeRequested) Close();
        }

        private void Update()
        {
            if (open && player != null && ProceduralWorld.Active != null &&
                (center != WorldLayout.Coordinate(player.transform.position) || shownSeed != ProceduralWorld.Active.Seed)) Refresh();
        }
        private void OnDestroy()
        {
            toggle?.Dispose(); cancel?.Dispose();
            if(texture!=null) Destroy(texture);
        }
    }
}
