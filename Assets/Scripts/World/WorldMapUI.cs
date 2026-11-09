using System.Collections.Generic;
using SheltreSim.Building;
using SheltreSim.Objectives;
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
        public const float ViewSpan = 1536f, MapSize = 760f;
        private InputAction toggle, cancel;
        private Canvas canvas;
        private RawImage map;
        private Text heading, position;
        private RectTransform home;
        private readonly List<Text> markers = new List<Text>();
        private Text waypointMarker;
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
            // Heading above, legend below, Close under that: nothing on the map can reach them, and labels clip at its edge.
            map.rectTransform.anchoredPosition = new Vector2(0,52);
            map.rectTransform.sizeDelta = new Vector2(MapSize,MapSize);
            map.raycastTarget = false;
            obj.AddComponent<RectMask2D>();
            texture = new Texture2D(128,128,TextureFormat.RGB24,false) { filterMode = FilterMode.Point, name = "LocalAtlas" };
            map.texture = texture;
            GameUI.Label(map.transform, "Player", "+", Vector2.one*.5f, Vector2.one*.5f,
                Vector2.zero,new Vector2(28,28),24,Color.yellow,TextAnchor.MiddleCenter);
            home = GameUI.Label(map.transform, "Bunker", "B", Vector2.one*.5f,Vector2.one*.5f,
                Vector2.zero,new Vector2(28,28),24,GameUI.Accent,TextAnchor.MiddleCenter).rectTransform;
            waypointMarker = GameUI.Label(map.transform, "Waypoint", "", Vector2.one*.5f,Vector2.one*.5f,
                Vector2.zero,new Vector2(180,24),15,new Color(1f,.55f,.25f),TextAnchor.MiddleCenter,true);
            position = GameUI.Label(panel,"Position","",new Vector2(.5f,0),new Vector2(.5f,0),
                new Vector2(0,56),new Vector2(850,112),15,Color.white,TextAnchor.MiddleCenter);
            GameUI.Button(panel,"Close","CLOSE  [M / ESC]",new Vector2(.5f,0),new Vector2(.5f,0),
                new Vector2(0,12),new Vector2(270,38),Close);
        }

        private void Refresh()
        {
            var world = ProceduralWorld.Active;
            if (world == null) return;
            center = WorldLayout.Coordinate(player.transform.position);
            shownSeed = world.Seed;
            const float span = ViewSpan;
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
            Vector2 homeOffset = MapOffset(WorldLayout.BunkerCenter, p);
            home.gameObject.SetActive(Mathf.Abs(homeOffset.x)<MapSize*.5f-15f && Mathf.Abs(homeOffset.y)<MapSize*.5f-15f);
            home.anchoredPosition=homeOffset;
            RefreshMarkers(p, world.Seed);
            ObjectiveTracker tracker=ObjectiveTracker.Instance;
            int biome=WorldLayout.BiomeIndexAt(p.x,p.z,world.Seed);
            var atmosphere=world.GetComponent<WorldAtmosphere>();
            heading.text="WILDERNESS ATLAS  /  "+WorldLayout.BiomeNames[biome]+(atmosphere!=null?$"  {atmosphere.Hour:00}:00 {atmosphere.WeatherName}":"");
            Vector3 homeFlat=WorldLayout.BunkerCenter-p; homeFlat.y=0f;
            string homeText=homeFlat.magnitude<40f?"Home: here":$"Home {homeFlat.magnitude:0} m {ObjectiveTracker.Compass(p,WorldLayout.BunkerCenter)}";
            string waypointText="No waypoint";
            string goal="";
            if(tracker!=null)
            {
                if(tracker.HasWaypoint)
                {
                    Vector3 w=tracker.WaypointPosition; w.y=p.y;
                    waypointText=$"Waypoint: {tracker.WaypointTitle}, {(w-p).magnitude:0} m {ObjectiveTracker.Compass(p,w)}";
                }
                goal=tracker.GoalText().Split('\n')[0];
            }
            position.text=$"NORTH ↑   /   YELLOW + YOU   /   GREEN B BUNKER   /   [ ] VISITED   /   ? REVEALED   /   X WAYPOINT\nSeed {world.Seed}   |   X {p.x:0}   Z {p.z:0}   |   {homeText}\n{waypointText}\n{goal}\nLocal view 1.5 km. Pale districts: towns. Dark lines: roads. Undiscovered loot is not shown.";
        }

        /// <summary>Where a world position falls on the map, relative to its centre (the viewer).</summary>
        public static Vector2 MapOffset(Vector3 world, Vector3 viewer) =>
            new Vector2(world.x-viewer.x,world.z-viewer.z)/ViewSpan*MapSize;

        // Works from stable ids and seed metadata alone: nothing here can generate a chunk.
        private void RefreshMarkers(Vector3 p, int seed)
        {
            var progression=ExpeditionProgression.Active;
            var tracker=ObjectiveTracker.Instance;
            int used=0;
            var shown=new HashSet<string>();
            if(progression!=null)
                foreach(string id in progression.CaptureDiscoveries()) PlaceSite(id,"[","]",GameUI.Accent,p,seed,shown,ref used);
            if(tracker!=null)
            {
                foreach(string id in tracker.RevealedLandmarks) PlaceSite(id,"? ","",GameUI.Warning,p,seed,shown,ref used);
                PlaceSite(tracker.RelayLocatedId,"? ","",GameUI.Warning,p,seed,shown,ref used);
            }
            for(int i=used;i<markers.Count;i++) markers[i].gameObject.SetActive(false);
            bool hasWaypoint=tracker!=null&&tracker.HasWaypoint;
            waypointMarker.gameObject.SetActive(hasWaypoint);
            if(!hasWaypoint) return;
            Vector2 offset=MapOffset(tracker.WaypointPosition,p);
            bool inside=Mathf.Abs(offset.x)<MapSize*.5f-92f&&Mathf.Abs(offset.y)<MapSize*.5f-14f;
            // Off the map it stays on the edge, along its true direction, so it always points the way.
            if(!inside)
            {
                float scale=Mathf.Min((MapSize*.5f-92f)/Mathf.Max(.01f,Mathf.Abs(offset.x)),(MapSize*.5f-14f)/Mathf.Max(.01f,Mathf.Abs(offset.y)));
                offset*=scale;
            }
            waypointMarker.rectTransform.anchoredPosition=offset;
            waypointMarker.text=inside?"X "+tracker.WaypointTitle:"X WAYPOINT";
        }

        private void PlaceSite(string id,string prefix,string suffix,Color color,Vector3 p,int seed,HashSet<string> shown,ref int used)
        {
            if(string.IsNullOrEmpty(id)||!shown.Add(id)) return;
            if(!ProceduralLandmarks.TryParseStableId(id,out Vector2Int coordinate,out int siteSeed)||siteSeed!=seed) return;
            Vector2 offset=MapOffset(ProceduralLandmarks.WorldCenter(coordinate),p);
            if(Mathf.Abs(offset.x)>=MapSize*.5f-8f||Mathf.Abs(offset.y)>=MapSize*.5f-8f) return;
            if(!ProceduralLandmarks.TryGetKind(coordinate,seed,out int kind)) return;
            if(used==markers.Count)
                markers.Add(GameUI.Label(map.transform,"Site"+used,"",Vector2.one*.5f,Vector2.one*.5f,Vector2.zero,new Vector2(110,22),14,Color.white,TextAnchor.MiddleCenter,true));
            Text marker=markers[used++];
            marker.gameObject.SetActive(true);
            marker.rectTransform.anchoredPosition=offset;
            marker.color=color;
            marker.text=prefix+ProceduralLandmarks.ShortName(kind)+suffix;
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
