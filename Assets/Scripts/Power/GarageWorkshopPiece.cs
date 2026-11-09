using System;
using System.Collections.Generic;
using SheltreSim.Building;
using SheltreSim.Crafting;
using SheltreSim.Interaction;
using SheltreSim.Items;
using SheltreSim.Saving;
using SheltreSim.Storage;
using SheltreSim.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SheltreSim.Power
{
    /// <summary>
    /// Powered, player-enabled workshop service for nearby motor vehicles. Repair kits are paid from local stores once,
    /// then their HP credit is spent only while the grid grants power. Both the toggle and unused credit persist per piece.
    /// </summary>
    public sealed class GarageWorkshopPiece : PowerConsumerDevice, IDismantleGuard
    {
        public const float RepairRange = 12f;
        public const float SupplyRange = 8f;
        public const float RepairKitHp = 150f;
        public const float AssemblySeconds = 45f;
        public const float RequiredPowerKw = 2f;
        private const string ScoutBuggyId = "piece_scout_buggy";
        private const string ArmoredVanId = "piece_armored_van";

        public float heatPerSecond = 0.6f;
        public float hpPerSecond = 4f;

        [Serializable]
        private sealed class SavedState
        {
            public bool serviceEnabled;
            public float paidHp;
            public string targetId;
            public string assemblyId;
            public float assemblyProgress;
        }

        private bool serviceEnabled;
        private float paidHp;
        private string targetId = string.Empty;
        private string assemblyId = string.Empty;
        private float assemblyProgress;
        private StructureHealth target;
        private bool warnedNoKit;
        private Text panelStatus;
        private Button serviceButton, buggyButton, vanButton;

        public override string PowerName => "Garage Workshop";
        public override int PowerPriority => 2;
        public override float PowerDemand
        {
            get
            {
                if (!string.IsNullOrEmpty(assemblyId))
                    return assemblyProgress < AssemblySeconds ? RequiredPowerKw : 0f;
                return serviceEnabled && FindTarget() != null && CanPay ? RequiredPowerKw : 0f;
            }
        }
        public override float HeatOutput => PowerRunning && (serviceEnabled || !string.IsNullOrEmpty(assemblyId)) ? Mathf.Max(0f, heatPerSecond) : 0f;
        public override string InteractPrompt => $"Press E: {PowerName} — {PowerStatus}";

        private bool CanPay => paidHp > 0f || StorageRegistry.CountNearby(PowerPosition, SupplyRange, HomesteadCatalog.RepairKitMetal) > 0;

        public override string PowerStatus
        {
            get
            {
                if (!string.IsNullOrEmpty(assemblyId))
                {
                    string name = VehicleName(assemblyId);
                    if (assemblyProgress >= AssemblySeconds) return $"{name} assembled; waiting for a clear nearby spot";
                    string assembly = $"assembling {name}, {PowerNetworks.FormatTime(AssemblySeconds - assemblyProgress)} left";
                    return PowerRunning ? assembly : $"{NoPowerReason()} ({assembly})";
                }
                if (!serviceEnabled) return "service off";
                StructureHealth current = FindTarget();
                if (current == null) return $"on, no damaged vehicle within {RepairRange:0} m";
                string job = $"repairing {current.name.Replace("(Clone)", "")} ({Mathf.RoundToInt(current.Fraction * 100f)}%), {paidHp:0} HP credit";
                if (!CanPay) return $"stopped, needs {ItemName(HomesteadCatalog.RepairKitMetal)} in a store within {SupplyRange:0} m";
                return PowerRunning ? job : $"{NoPowerReason()} ({job})";
            }
        }

        public override void Interact(GameObject interactor)
        {
            OpenPanel(interactor);
        }

        private void ToggleService()
        {
            serviceEnabled = !serviceEnabled;
            warnedNoKit = false;
            GameMessageBus.Post(serviceEnabled
                ? $"Garage service enabled. Requires {RequiredPowerKw:0.0} kW and Metal Repair Kits in a nearby store."
                : $"Garage service stopped. Unused repair credit: {paidHp:0} HP.");
            RefreshPanel();
        }

        public override void Advance(float seconds)
        {
            if (seconds <= 0f) return;
            if (!string.IsNullOrEmpty(assemblyId))
            {
                if (assemblyProgress >= AssemblySeconds) { TryCompleteAssembly(); return; }
                if (!PowerRunning) return;
                assemblyProgress = Mathf.Min(AssemblySeconds, assemblyProgress + seconds);
                if (assemblyProgress >= AssemblySeconds) TryCompleteAssembly();
                return;
            }
            if (!serviceEnabled) return;
            StructureHealth current = FindTarget();
            if (current == null) return;
            if (!CanPay)
            {
                if (!warnedNoKit)
                {
                    warnedNoKit = true;
                    GameMessageBus.Post($"Garage workshop paused: store a Metal Repair Kit within {SupplyRange:0} m.");
                }
                return;
            }
            warnedNoKit = false;
            if (!PowerRunning || PowerDemand <= 0f) return;

            float want = Mathf.Min(Mathf.Max(0f, hpPerSecond) * seconds, current.maxHealth - current.Health);
            if (want <= 0f) { target = null; targetId = string.Empty; return; }
            if (paidHp + 0.001f < want)
            {
                if (!StorageRegistry.TryTakeNearby(PowerPosition, SupplyRange, HomesteadCatalog.RepairKitMetal, 1)) return;
                paidHp += RepairKitHp;
            }

            float repaired = current.Repair(Mathf.Min(want, paidHp));
            paidHp = Mathf.Max(0f, paidHp - repaired);
            if (repaired > 0f && current.Health >= current.maxHealth)
            {
                target = null;
                targetId = string.Empty;
                GameMessageBus.Post("Vehicle service complete.");
            }
        }

        protected override Vector2 PanelSize => new Vector2(900f, 600f);

        protected override void BuildPanel(Transform panel)
        {
            Vector2 top = new Vector2(0.5f, 1f);
            Vector2 bottom = new Vector2(0.5f, 0f);
            panelStatus = GameUI.Label(panel, "GarageStatus", "", top, top, new Vector2(0f, -76f), new Vector2(820f, 94f), 18, Color.white, TextAnchor.MiddleCenter);
            serviceButton = GameUI.Button(panel, "ServiceToggle", "", top, top, new Vector2(0f, -198f), new Vector2(470f, 56f), ToggleService, true);
            buggyButton = GameUI.Button(panel, "AssembleScoutBuggy", "", top, top, new Vector2(0f, -278f), new Vector2(470f, 56f), () => StartAssembly(ScoutBuggyId));
            vanButton = GameUI.Button(panel, "AssembleArmoredVan", "", top, top, new Vector2(0f, -352f), new Vector2(470f, 56f), () => StartAssembly(ArmoredVanId));
            GameUI.Button(panel, "Close", "CLOSE [ESC]", bottom, bottom, new Vector2(0f, 14f), new Vector2(240f, 42f), ClosePanel);
            RefreshPanel();
        }

        protected override void RefreshPanel()
        {
            if (panelStatus == null) return;
            string assembly = string.IsNullOrEmpty(assemblyId) ? "No assembly queued." :
                assemblyProgress >= AssemblySeconds ? $"{VehicleName(assemblyId)} is ready; move clear of the garage output area." :
                $"Assembly: {VehicleName(assemblyId)} — {PowerNetworks.FormatTime(AssemblySeconds - assemblyProgress)} remaining.";
            string targetText = FindTarget() is StructureHealth current
                ? $"Vehicle: {current.name.Replace("(Clone)", "")} — {Mathf.RoundToInt(current.Fraction * 100f)}% HP."
                : "No damaged Scout Buggy or Armored Van in service range.";
            panelStatus.text = $"{PowerStatus}\n{assembly}\n{targetText}\nPower: {RequiredPowerKw:0.0} kW — local supplies: {SupplyRange:0} m.";
            if (serviceButton != null) serviceButton.GetComponentInChildren<Text>().text = serviceEnabled ? "STOP VEHICLE REPAIR" : "ENABLE VEHICLE REPAIR";
            if (buggyButton != null)
            {
                buggyButton.GetComponentInChildren<Text>().text = AssemblyButtonText(ScoutBuggyId);
                buggyButton.interactable = string.IsNullOrEmpty(assemblyId);
            }
            if (vanButton != null)
            {
                vanButton.GetComponentInChildren<Text>().text = AssemblyButtonText(ArmoredVanId);
                vanButton.interactable = string.IsNullOrEmpty(assemblyId);
            }
        }

        private string AssemblyButtonText(string id)
        {
            BuildPieceDefinition definition = FindDefinition(id);
            string status = definition == null || definition.prefab == null ? "setup needed" :
                PanelInventory != null && CanAffordLocal(PanelInventory, id) ? "ready" : "missing materials";
            return $"ASSEMBLE {VehicleName(id).ToUpperInvariant()} — {status}";
        }

        private void StartAssembly(string pieceId)
        {
            if (!string.IsNullOrEmpty(assemblyId)) { PanelMessage.text = "Finish the current assembly first."; return; }
            if (PanelInventory == null) { PanelMessage.text = "Player inventory is unavailable."; return; }
            BuildPieceDefinition definition = FindDefinition(pieceId);
            if (definition == null || definition.prefab == null || definition.prefab.GetComponent<PlacedPiece>() == null)
            {
                PanelMessage.text = $"{VehicleName(pieceId)} is not registered in the build catalogue yet.";
                return;
            }
            if (!TrySpendLocal(PanelInventory, pieceId, out string reason)) { PanelMessage.text = reason; return; }
            assemblyId = pieceId;
            assemblyProgress = 0f;
            serviceEnabled = false;
            PanelMessage.text = $"Materials reserved. {VehicleName(pieceId)} assembly needs {RequiredPowerKw:0.0} kW for {AssemblySeconds:0} seconds.";
            GameMessageBus.Post($"{VehicleName(pieceId)} assembly started.");
            RefreshPanel();
        }

        private bool CanAffordLocal(Inventory inventory, string pieceId)
        {
            if (inventory == null || !RecipeBook.PieceCosts.TryGetValue(pieceId, out Ingredient[] costs)) return false;
            foreach (Ingredient item in costs)
            {
                if (string.IsNullOrEmpty(item.id) || item.amount <= 0 || !SheltreSim.Crafting.ItemCatalog.Exists(item.id)) return false;
                long available = (long)inventory.GetCount(item.id) + StorageRegistry.CountNearby(PowerPosition, SupplyRange, item.id);
                if (available < item.amount) return false;
            }
            return true;
        }

        private bool TrySpendLocal(Inventory inventory, string pieceId, out string reason)
        {
            reason = "Missing materials in your pack or stores within 8 m of the garage.";
            if (inventory == null || !RecipeBook.PieceCosts.TryGetValue(pieceId, out Ingredient[] costs) || costs == null || costs.Length == 0) return false;

            var totals = new Dictionary<string, int>();
            foreach (Ingredient item in costs)
            {
                if (string.IsNullOrWhiteSpace(item.id) || item.amount <= 0 || !SheltreSim.Crafting.ItemCatalog.Exists(item.id))
                { reason = "This vehicle has an invalid recipe; no items were taken."; return false; }
                totals.TryGetValue(item.id, out int previous);
                long total = (long)previous + item.amount;
                if (total > int.MaxValue) { reason = "Vehicle recipe amount exceeds inventory limits."; return false; }
                totals[item.id] = (int)total;
            }

            var packAmounts = new Dictionary<string, int>();
            foreach (KeyValuePair<string, int> cost in totals)
            {
                int fromPack = Mathf.Min(inventory.GetCount(cost.Key), cost.Value);
                long available = (long)fromPack + StorageRegistry.CountNearby(PowerPosition, SupplyRange, cost.Key);
                if (available < cost.Value)
                {
                    reason = $"Missing {cost.Value - (int)available} {SheltreSim.Crafting.ItemCatalog.NameOf(cost.Key)} locally.";
                    return false;
                }
                packAmounts[cost.Key] = fromPack;
            }

            var taken = new List<(IItemStore store, string id, int amount)>();
            foreach (KeyValuePair<string, int> cost in totals)
            {
                int fromStores = cost.Value - packAmounts[cost.Key];
                if (fromStores <= 0) continue;
                if (TakeLocal(cost.Key, fromStores, taken)) continue;
                foreach (var entry in taken) if (entry.store != null) entry.store.Add(entry.id, entry.amount);
                reason = "Local stores changed during assembly start. No materials were consumed.";
                return false;
            }

            foreach (KeyValuePair<string, int> amount in packAmounts)
                if (amount.Value > 0) inventory.SetCount(amount.Key, inventory.GetCount(amount.Key) - amount.Value);
            reason = null;
            return true;
        }

        private bool TakeLocal(string id, int amount, List<(IItemStore store, string id, int amount)> taken)
        {
            int left = amount;
            float radiusSq = SupplyRange * SupplyRange;
            foreach (IItemStore store in StorageRegistry.All)
            {
                if (left <= 0) break;
                if (store == null || (store.StorePosition - PowerPosition).sqrMagnitude > radiusSq) continue;
                int take = Mathf.Min(left, store.GetCount(id));
                if (take <= 0 || !store.TryTake(id, take)) continue;
                left -= take;
                taken.Add((store, id, take));
            }
            return left <= 0;
        }

        private void TryCompleteAssembly()
        {
            BuildPieceDefinition definition = FindDefinition(assemblyId);
            if (definition == null || definition.prefab == null) return;
            if (!TryFindAssemblyPose(definition, out Vector3 position, out Quaternion rotation)) return;
            string completedId = assemblyId;
            GameObject built = Instantiate(definition.prefab, position, rotation);
            PlacedPiece placed = built != null ? built.GetComponent<PlacedPiece>() : null;
            if (placed == null || placed.pieceId != completedId)
            {
                if (built != null) Destroy(built);
                Debug.LogError($"[GarageWorkshopPiece] Prefab for {completedId} must have a matching PlacedPiece root.");
                return;
            }

            assemblyId = string.Empty;
            assemblyProgress = 0f;
            GameMessageBus.Post($"{VehicleName(completedId)} assembled and ready to drive.");
            PanelMessageSafe("Assembly complete: one vehicle was registered at the garage exit.");
        }

        private bool TryFindAssemblyPose(BuildPieceDefinition definition, out Vector3 position, out Quaternion rotation)
        {
            Vector3 footprint = definition.footprintSize;
            rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            Vector3[] directions = { transform.forward, -transform.forward, transform.right, -transform.right,
                (transform.forward + transform.right).normalized, (transform.forward - transform.right).normalized,
                (-transform.forward + transform.right).normalized, (-transform.forward - transform.right).normalized };
            float distance = Mathf.Max(footprint.x, footprint.z) * 0.5f + 2.5f;
            PlacedPiece garage = GetComponent<PlacedPiece>();
            foreach (Vector3 direction in directions)
            {
                Vector3 candidate = transform.position + direction * distance;
                RaycastHit[] grounds = Physics.RaycastAll(candidate + Vector3.up * 16f, Vector3.down, 40f, ~0, QueryTriggerInteraction.Ignore);
                Array.Sort(grounds, (a, b) => a.distance.CompareTo(b.distance));
                bool foundGround = false;
                foreach (RaycastHit ground in grounds)
                {
                    PlacedPiece supportingPiece = ground.collider.GetComponentInParent<PlacedPiece>();
                    if (supportingPiece != null || ground.normal.y < 0.55f || ground.collider.GetComponentInParent<CharacterController>() != null) continue;
                    candidate.y = ground.point.y + footprint.y * 0.5f + 0.03f;
                    foundGround = true;
                    break;
                }
                if (!foundGround) continue;

                Vector3 half = new Vector3(Mathf.Max(.1f, footprint.x * .48f), Mathf.Max(.1f, footprint.y * .48f), Mathf.Max(.1f, footprint.z * .48f));
                Collider[] overlaps = Physics.OverlapBox(candidate, half, rotation, ~0, QueryTriggerInteraction.Ignore);
                bool blocked = false;
                foreach (Collider overlap in overlaps)
                {
                    if (overlap == null || overlap.transform == transform || overlap.transform.IsChildOf(transform)) continue;
                    if (garage != null && overlap.GetComponentInParent<PlacedPiece>() == garage) continue;
                    blocked = true;
                    break;
                }
                if (blocked) continue;
                position = candidate;
                return true;
            }
            position = default;
            return false;
        }

        private static BuildPieceDefinition FindDefinition(string id)
        {
            foreach (BuildModeController controller in FindObjectsByType<BuildModeController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (controller == null || controller.availablePieces == null) continue;
                foreach (BuildPieceDefinition definition in controller.availablePieces)
                    if (definition != null && definition.id == id) return definition;
            }
            return null;
        }

        private static string VehicleName(string id) => id == ScoutBuggyId ? "Scout Buggy" : id == ArmoredVanId ? "Armored Van" : id;

        private void PanelMessageSafe(string text)
        {
            if (PanelMessage != null) PanelMessage.text = text;
        }

        public bool CanDismantle(out string reason)
        {
            if (!string.IsNullOrEmpty(assemblyId))
            {
                reason = "Finish the vehicle assembly before dismantling the garage.";
                return false;
            }
            if (serviceEnabled || paidHp > 0.01f)
            {
                reason = "Stop vehicle service and spend its paid repair credit before dismantling the garage.";
                return false;
            }
            reason = null;
            return true;
        }

        public override string CapturePieceState() => JsonUtility.ToJson(new SavedState
        {
            serviceEnabled = serviceEnabled,
            paidHp = paidHp,
            targetId = targetId,
            assemblyId = assemblyId,
            assemblyProgress = assemblyProgress,
        });

        public override void RestorePieceState(string state)
        {
            SavedState saved = PowerNetworks.ParseState<SavedState>(state);
            serviceEnabled = saved != null && saved.serviceEnabled;
            paidHp = saved != null ? PowerNetworks.Sane(saved.paidHp, 0f, RepairKitHp) : 0f;
            targetId = saved != null ? saved.targetId ?? string.Empty : string.Empty;
            assemblyId = saved != null && (saved.assemblyId == ScoutBuggyId || saved.assemblyId == ArmoredVanId) ? saved.assemblyId : string.Empty;
            assemblyProgress = assemblyId.Length > 0 && saved != null ? PowerNetworks.Sane(saved.assemblyProgress, 0f, AssemblySeconds) : 0f;
            target = null;
            warnedNoKit = false;
        }

        private StructureHealth FindTarget()
        {
            if (IsValidTarget(target)) return target;
            target = null;

            if (!string.IsNullOrEmpty(targetId))
            {
                foreach (StructureHealth candidate in StructureHealth.All)
                {
                    if (!IsValidTarget(candidate)) continue;
                    PlacedPiece piece = candidate.GetComponent<PlacedPiece>();
                    if (piece != null && SaveManager.ComputeStableId(piece.transform) == targetId)
                    {
                        target = candidate;
                        return target;
                    }
                }
                // The old target was removed or moved out of service range. Its paid credit stays in this station.
                targetId = string.Empty;
            }

            float best = float.PositiveInfinity;
            foreach (StructureHealth candidate in StructureHealth.All)
            {
                if (!IsValidTarget(candidate)) continue;
                float distance = (candidate.transform.position - PowerPosition).sqrMagnitude;
                if (distance >= best) continue;
                best = distance;
                target = candidate;
            }
            if (target != null)
            {
                PlacedPiece piece = target.GetComponent<PlacedPiece>();
                targetId = piece != null ? SaveManager.ComputeStableId(piece.transform) : string.Empty;
            }
            return target;
        }

        private bool IsValidTarget(StructureHealth candidate)
        {
            if (candidate == null || !candidate.isActiveAndEnabled || !candidate.IsDamaged) return false;
            PlacedPiece piece = candidate.GetComponent<PlacedPiece>();
            if (piece == null || !IsVehiclePiece(piece.pieceId)) return false;
            return (candidate.transform.position - PowerPosition).sqrMagnitude <= RepairRange * RepairRange;
        }

        private static bool IsVehiclePiece(string id) => id == "piece_scout_buggy" || id == "piece_armored_van";
        private static string ItemName(string id) => SheltreSim.Crafting.ItemCatalog.NameOf(id);
    }
}
