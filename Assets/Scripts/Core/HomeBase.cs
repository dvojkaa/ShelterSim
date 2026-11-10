using System.Collections.Generic;
using SheltreSim.Audio;
using SheltreSim.Building;
using SheltreSim.Crafting;
using SheltreSim.Interaction;
using SheltreSim.Items;
using SheltreSim.Saving;
using SheltreSim.Storage;
using SheltreSim.UI;
using UnityEngine;

namespace SheltreSim.Core
{
    /// <summary>
    /// The start of the game: savings, bunker contract, room upgrades and the supply shop.
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

        public static int StartingMoney => GameDifficultySettings.StartingBudget;
        public const int ContractPrice = 20000;

        public static class EconomyTuning
        {
            public const int NormalBudget = 70000;
            public const int ContractPrice = 20000;
        }

        public GameStateManager gameState;
        public Inventory inventory;
        public GameObject[] stageVisuals = new GameObject[0];
        public GameObject entranceCover;
        public GameObject annexPlug;
        public GameObject annexRoom;
        public GameObject workshopWing;
        public GameObject hydroWing;
        public GameObject vaultRoom;
        public GameObject northWingShell;
        public GameObject northWingPlug;
        public GameObject vaultPlug;
        public GameObject reinforcedDoorLeaf;
        /// <summary>Delivery pallet at the stair head: orders are arriving once the contract is signed, until the bunker is sealed.</summary>
        public GameObject deliveryPallet;
        public InteractableDoor mainBlastDoor;
        public StorageCratePiece supplyCage;
        public GameObject[] removedWhenComplete = new GameObject[0];
        public GameObject[] removedByBombs = new GameObject[0];

        public int Money { get; private set; } = StartingMoney;
        public int CompletedStages { get; private set; }
        public bool IsBuilding { get; private set; }
        public float BuildElapsed { get; private set; }
        public bool HasAnnex { get; private set; }
        public bool HasContract { get; private set; }
        public BunkerShop.RoomBits Rooms { get; private set; }
        public bool IsSealing => HasContract && IsBuilding && CompletedStages == Stages.Length - 1;
        public int Spent => Mathf.Max(0, StartingMoney - Money);
        public int UnspentSavings => Money;
        public bool BunkerComplete => CompletedStages >= Stages.Length;
        public bool CanShop => (gameState == null || gameState.CurrentPhase == GamePhase.Building) && !IsSealing;

        private readonly Dictionary<string, int> purchaseCounts = new Dictionary<string, int>();
        private RadioNewsCountdown radioNews;
        public int PurchaseCount(string offerId) => offerId != null && purchaseCounts.TryGetValue(offerId, out int n) ? n : 0;

        public int NextStageIndex => IsBuilding ? CompletedStages + 1 : CompletedStages;
        public float BuildFraction => IsBuilding ? Mathf.Clamp01(BuildElapsed / Stages[CompletedStages].seconds) : 0f;

        public int SpendableMoney => HasContract ? Money : 0;

        private void Awake()
        {
            Active = this;
            if (gameState == null) gameState = FindFirstObjectByType<GameStateManager>();
            radioNews = GetComponent<RadioNewsCountdown>();
            if (radioNews == null) radioNews = gameObject.AddComponent<RadioNewsCountdown>();
        }

        private void Start()
        {
            ResolveSupplyCage();
            RefreshVisuals();
        }

        public void ResolveSupplyCage()
        {
            if (supplyCage != null && supplyCage.isActiveAndEnabled) return;
            SupplyCageGuard guard = FindFirstObjectByType<SupplyCageGuard>();
            if (guard == null)
            {
                // Save/load recreates placed crates from their ordinary prefab. Re-identify
                // the designated bunker crate by its stable piece id and fixed site position.
                Vector3 bunkerCagePosition = new Vector3(67f, 0.5f, -222.4f);
                foreach (PlacedPiece piece in PlacedPieceRegistry.All)
                    if (piece != null && piece.pieceId == "storage_crate" &&
                        (piece.transform.position - bunkerCagePosition).sqrMagnitude < 1f)
                    {
                        guard = piece.GetComponent<SupplyCageGuard>();
                        if (guard == null) guard = piece.gameObject.AddComponent<SupplyCageGuard>();
                        StructureHealth health = piece.GetComponent<StructureHealth>();
                        if (health != null) { health.enabled = false; Destroy(health); }
                        break;
                    }
            }
            supplyCage = guard != null ? guard.GetComponent<StorageCratePiece>() : null;
        }

        private void OnDestroy()
        {
            if (Active == this) Active = null;
        }

        private void Update()
        {
            if (!IsBuilding || (Time.timeScale <= 0f && !BunkerTerminalUI.IsOpen)) return;
            // Construction can continue while the player browses the terminal before sealing.
            // Life support itself is the seal animation and must use scaled time; the terminal
            // closes as soon as sealing starts, and reopening any modal pauses this final stage.
            BuildElapsed += !IsSealing && BunkerTerminalUI.IsOpen ? Time.unscaledDeltaTime : Time.deltaTime;
            if (BuildElapsed < Stages[CompletedStages].seconds) return;
            CompletedStages++;
            IsBuilding = false;
            BuildElapsed = 0f;
            RefreshVisuals();
            GameAudio.PlayAt(Sfx.PlacePiece, transform.position, 1f, 60f);
            if (HasContract && CompletedStages < 3)
            {
                IsBuilding = true;
                GameMessageBus.Post($"Finished: {Stages[CompletedStages - 1].name}. Next bunker stage is starting.");
                return;
            }
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

        public bool TryBuyContract(out string result)
        {
            if (!CanShop || HasContract) { result = "The bunker contract is not available."; return false; }
            if (Money < ContractPrice) { result = "You need $20,000 to sign the bunker contract."; return false; }
            if (inventory == null) { result = "Your starter pack cannot be delivered right now."; return false; }
            Money -= ContractPrice;
            HasContract = true;
            inventory.Add("hammer", 1);
            inventory.Add("canned_food", 4);
            inventory.Add("bottled_water", 4);
            inventory.Add("bandage", 2);
            inventory.Add("paper_map", 1);
            CompletedStages = 0;
            IsBuilding = true;
            BuildElapsed = 0f;
            result = "Bunker contract signed. Excavation, concrete shell and stairs are now scheduled.";
            GameMessageBus.Post(result);
            if (radioNews == null) radioNews = GetComponent<RadioNewsCountdown>();
            radioNews?.StartAfterContract();
            return true;
        }

        public bool CanSeal => HasContract && CanShop && !IsBuilding && CompletedStages == 3;

        public bool TrySeal(out string result)
        {
            if (!CanSeal) { result = "Finish the concrete shell before sealing the bunker."; return false; }
            IsBuilding = true;
            BuildElapsed = 0f;
            BunkerTerminalUI.CloseIfOpen();
            result = "Life support is being installed. The shop is now closed.";
            GameMessageBus.Post(result);
            return true;
        }

        public bool TryBuyOffer(BunkerShop.Offer offer, out string result)
        {
            if (!CanShop || IsSealing) { result = "The shops are gone."; return false; }
            if (!HasContract && offer != null && offer.id != "bunker_contract") { result = "Sign the bunker contract first."; return false; }
            if (offer == null) { result = "Unavailable."; return false; }
            if (offer.id == "bunker_contract") return TryBuyContract(out result);
            int count = PurchaseCount(offer.id);
            if (count >= offer.limit) { result = "You have reached the purchase limit."; return false; }
            if (CompletedStages < offer.requiredStage) { result = $"Requires bunker stage {offer.requiredStage}."; return false; }
            if ((Rooms & offer.requiredRooms) != offer.requiredRooms) { result = "A required room has not been purchased."; return false; }
            if (!string.IsNullOrEmpty(offer.exclusiveGroup))
                foreach (BunkerShop.Offer other in BunkerShop.All)
                    if (other.id != offer.id && other.exclusiveGroup == offer.exclusiveGroup && (Rooms & other.roomBit) != 0)
                    { result = "That room footprint is already committed to the other fit-out."; return false; }
            if (Money < offer.price) { result = "Not enough savings."; return false; }
            ResolveSupplyCage();
            if (offer.grants != null && offer.grants.Length > 0)
            {
                if (supplyCage == null) { result = "The Supply Cage is not connected yet; purchase is safely blocked."; return false; }
                foreach (BunkerShop.Grant grant in offer.grants)
                    if (grant == null || grant.quantity <= 0 || supplyCage.GetAddRoom(grant.itemId) < grant.quantity)
                    { result = "The Supply Cage cannot hold that delivery."; return false; }
            }
            Money -= offer.price;
            if (offer.grants != null)
                foreach (BunkerShop.Grant grant in offer.grants) supplyCage.Add(grant.itemId, grant.quantity);
            if (offer.roomBit != BunkerShop.RoomBits.None) Rooms |= offer.roomBit;
            purchaseCounts[offer.id] = count + 1;
            HasAnnex = (Rooms & BunkerShop.RoomBits.Annex) != 0;
            RefreshVisuals();
            result = $"Purchased {offer.displayName}; delivery sent to the Supply Cage.";
            GameMessageBus.Post(result);
            return true;
        }

        public bool TryBuyAnnex(out string result) => TryBuyOffer(BunkerShop.Get("room_annex"), out result);

        /// <summary>Diagnostics only: skip the opening and start in the ruined world with a finished bunker.</summary>
        public void DebugStartSurvival()
        {
            HasContract = true;
            if (inventory != null && inventory.GetCount("paper_map") <= 0) inventory.Add("paper_map", 1);
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
            Rooms = annex ? BunkerShop.RoomBits.Annex : BunkerShop.RoomBits.None;
            HasContract = CompletedStages > 0 || IsBuilding;
            RefreshVisuals();
        }

        public string[] CaptureShopIds()
        {
            var ids = new List<string>(purchaseCounts.Keys);
            ids.Sort(System.StringComparer.Ordinal);
            return ids.ToArray();
        }

        public int[] CaptureShopCounts()
        {
            string[] ids = CaptureShopIds();
            var counts = new int[ids.Length];
            for (int i = 0; i < ids.Length; i++) counts[i] = purchaseCounts[ids[i]];
            return counts;
        }

        public void RestoreEconomy(int money, int completed, bool building, float elapsed, bool contract,
            BunkerShop.RoomBits rooms, string[] offerIds, int[] counts)
        {
            Restore(money, completed, building, elapsed, (rooms & BunkerShop.RoomBits.Annex) != 0);
            HasContract = contract;
            Rooms = rooms;
            purchaseCounts.Clear();
            if (offerIds != null && counts != null)
                for (int i = 0; i < Mathf.Min(offerIds.Length, counts.Length); i++)
                    if (!string.IsNullOrEmpty(offerIds[i]) && counts[i] > 0)
                        purchaseCounts[offerIds[i]] = counts[i];
            ResolveSupplyCage();
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
            if (workshopWing != null) workshopWing.SetActive((Rooms & BunkerShop.RoomBits.Workshop) != 0);
            if (hydroWing != null) hydroWing.SetActive((Rooms & BunkerShop.RoomBits.Hydro) != 0);
            if (vaultRoom != null) vaultRoom.SetActive((Rooms & BunkerShop.RoomBits.Vault) != 0);
            if (northWingShell != null) northWingShell.SetActive((Rooms & (BunkerShop.RoomBits.Workshop | BunkerShop.RoomBits.Hydro)) != 0);
            if (northWingPlug != null) northWingPlug.SetActive((Rooms & (BunkerShop.RoomBits.Workshop | BunkerShop.RoomBits.Hydro)) == 0);
            if (vaultPlug != null) vaultPlug.SetActive((Rooms & BunkerShop.RoomBits.Vault) == 0);
            if (deliveryPallet != null) deliveryPallet.SetActive(HasContract && !BunkerComplete);
            if (reinforcedDoorLeaf != null) reinforcedDoorLeaf.SetActive((Rooms & BunkerShop.RoomBits.ReinforcedDoor) != 0);
            if (mainBlastDoor != null)
                mainBlastDoor.maxRaidHealth = (Rooms & BunkerShop.RoomBits.ReinforcedDoor) != 0 ? 750f : 300f;
            foreach (GameObject item in removedWhenComplete)
                if (item != null) item.SetActive(!BunkerComplete);
            bool bombed = gameState != null && gameState.CurrentPhase == GamePhase.Survival;
            foreach (GameObject item in removedByBombs)
                if (item != null) item.SetActive(!bombed);
        }
    }
}
