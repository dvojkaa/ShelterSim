using System.Collections.Generic;
using SheltreSim.Audio;
using SheltreSim.Crafting;
using SheltreSim.Interaction;
using SheltreSim.Items;
using SheltreSim.UI;
using UnityEngine;

namespace SheltreSim.Core
{
    /// <summary>
    /// The start of the game: savings, the bunker project and the shop. The world stays normal until
    /// the last stage is finished, which is what starts the apocalypse. Each stage takes real time.
    /// The player can't overspend into a softlock: what the bunker still costs is always kept back.
    /// </summary>
    public sealed class HomeBase : MonoBehaviour
    {
        public sealed class Stage
        {
            public string name, description;
            public int price;
            public float seconds;
        }

        public static HomeBase Active { get; private set; }

        public static readonly Stage[] Stages =
        {
            new Stage { name = "Excavation", description = "Dig the pit and the stair shaft.", price = 5000, seconds = 20f },
            new Stage { name = "Concrete shell", description = "Pour the floor, walls and blast roof.", price = 8000, seconds = 30f },
            new Stage { name = "Stairs and blast door", description = "Open the way down. You can go inside and start furnishing.", price = 3000, seconds = 15f },
            new Stage { name = "Life support", description = "Air, power, water. FINISHING THIS COMPLETES THE BUNKER AND THE APOCALYPSE BEGINS.", price = 4000, seconds = 20f },
        };

        public const int StartingMoney = 25000;
        public const int AnnexPrice = 3000;

        public GameStateManager gameState;
        public Inventory inventory;
        public GameObject[] stageVisuals = new GameObject[0];
        public GameObject entranceCover;
        public GameObject annexPlug;
        public GameObject annexRoom;
        public GameObject[] removedWhenComplete = new GameObject[0];
        public GameObject[] removedByBombs = new GameObject[0];

        public int Money { get; private set; } = StartingMoney;
        public int CompletedStages { get; private set; }
        public bool IsBuilding { get; private set; }
        public float BuildElapsed { get; private set; }
        public bool HasAnnex { get; private set; }
        public bool BunkerComplete => CompletedStages >= Stages.Length;
        public bool CanShop => gameState == null || gameState.CurrentPhase == GamePhase.Building;

        public int NextStageIndex => IsBuilding ? CompletedStages + 1 : CompletedStages;
        public float BuildFraction => IsBuilding ? Mathf.Clamp01(BuildElapsed / Stages[CompletedStages].seconds) : 0f;

        /// <summary>Money the bunker still needs, kept out of reach of the shop.</summary>
        public int ReservedForBunker
        {
            get
            {
                int total = 0;
                for (int i = NextStageIndex; i < Stages.Length; i++) total += Stages[i].price;
                return total;
            }
        }

        public int SpendableMoney => Mathf.Max(0, Money - ReservedForBunker);

        private void Awake()
        {
            Active = this;
            if (gameState == null) gameState = FindFirstObjectByType<GameStateManager>();
        }

        private void Start()
        {
            RefreshVisuals();
        }

        private void OnDestroy()
        {
            if (Active == this) Active = null;
        }

        private void Update()
        {
            if (!IsBuilding || Time.timeScale <= 0f) return;
            BuildElapsed += Time.deltaTime;
            if (BuildElapsed < Stages[CompletedStages].seconds) return;
            CompletedStages++;
            IsBuilding = false;
            BuildElapsed = 0f;
            RefreshVisuals();
            GameAudio.PlayAt(Sfx.PlacePiece, transform.position, 1f, 60f);
            if (BunkerComplete)
            {
                GameMessageBus.Post("The bunker is finished.");
                if (gameState != null) gameState.TriggerApocalypse();
            }
            else
            {
                GameMessageBus.Post($"Finished: {Stages[CompletedStages - 1].name}. Back to the site terminal.");
            }
        }

        public bool TryStartNextStage(out string result)
        {
            if (!CanShop || BunkerComplete) { result = "The bunker is finished."; return false; }
            if (IsBuilding) { result = $"The crew is still on {Stages[CompletedStages].name}."; return false; }
            Stage stage = Stages[CompletedStages];
            if (Money < stage.price) { result = "Not enough money."; return false; }
            Money -= stage.price;
            IsBuilding = true;
            BuildElapsed = 0f;
            result = $"Crew hired: {stage.name}. Takes about {stage.seconds:0} seconds.";
            GameMessageBus.Post(result);
            return true;
        }

        public bool TryBuyAnnex(out string result)
        {
            if (!CanShop || HasAnnex) { result = "Not available."; return false; }
            if (CompletedStages < 2) { result = "The concrete shell has to be finished first."; return false; }
            if (SpendableMoney < AnnexPrice) { result = "The rest of your money is needed to finish the bunker."; return false; }
            Money -= AnnexPrice;
            HasAnnex = true;
            RefreshVisuals();
            result = "Storage annex added to the east side of the bunker.";
            GameMessageBus.Post(result);
            return true;
        }

        public bool TryBuy(ShopEntry entry, out string result)
        {
            if (!CanShop) { result = "The shops are gone."; return false; }
            if (inventory == null || entry == null) { result = "Unavailable."; return false; }
            if (SpendableMoney < entry.price) { result = "The rest of your money is needed to finish the bunker."; return false; }
            ResourceDefinition definition = ItemCatalog.Def(entry.id);
            if (definition == null) { result = "Unavailable."; return false; }
            Money -= entry.price;
            inventory.Add(entry.id, entry.quantity);
            result = $"Bought {entry.quantity} x {definition.displayName}.";
            GameMessageBus.Post(result);
            return true;
        }

        /// <summary>Diagnostics only: skip the opening and start in the ruined world with a finished bunker.</summary>
        public void DebugStartSurvival()
        {
            CompletedStages = Stages.Length;
            IsBuilding = false;
            if (gameState != null) gameState.RestorePhase(GamePhase.Survival);
            if (SheltreSim.World.ProceduralWorld.Active != null) SheltreSim.World.ProceduralWorld.Active.SetRuined(true);
            RefreshVisuals();
        }

        public void Restore(int money, int completed, bool building, float elapsed, bool annex)
        {
            Money = Mathf.Max(0, money);
            CompletedStages = Mathf.Clamp(completed, 0, Stages.Length);
            IsBuilding = building && !BunkerComplete;
            BuildElapsed = IsBuilding ? Mathf.Max(0f, elapsed) : 0f;
            HasAnnex = annex;
            RefreshVisuals();
        }

        /// <summary>Sets what stands on the site from the saved state alone, so a load and a live game can't differ.</summary>
        public void RefreshVisuals()
        {
            for (int i = 0; i < stageVisuals.Length; i++)
                if (stageVisuals[i] != null) stageVisuals[i].SetActive(CompletedStages > i);
            if (entranceCover != null) entranceCover.SetActive(CompletedStages < 3);
            if (annexPlug != null) annexPlug.SetActive(!HasAnnex);
            if (annexRoom != null) annexRoom.SetActive(HasAnnex);
            foreach (GameObject item in removedWhenComplete)
                if (item != null) item.SetActive(!BunkerComplete);
            bool bombed = gameState != null && gameState.CurrentPhase == GamePhase.Survival;
            foreach (GameObject item in removedByBombs)
                if (item != null) item.SetActive(!bombed);
        }
    }
}
