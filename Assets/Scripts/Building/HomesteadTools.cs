using System.Collections.Generic;
using System.Text;
using SheltreSim.Audio;
using SheltreSim.Crafting;
using SheltreSim.Items;
using SheltreSim.Saving;
using SheltreSim.Storage;
using UnityEngine;

namespace SheltreSim.Building
{
    /// <summary>
    /// Repairing and dismantling placed pieces. Nothing here can make material: a repair only ever costs a kit, and a refund
    /// is at most what went in. Only a piece that was just placed by mistake and never damaged returns everything.
    /// </summary>
    public static class HomesteadTools
    {
        private static readonly Collider[] overlaps = new Collider[64];

        public static PlacedPiece PieceUnderRay(Ray ray, float range, int mask)
        {
            if (!Physics.Raycast(ray, out RaycastHit hit, range, mask, QueryTriggerInteraction.Ignore)) return null;
            return hit.collider.GetComponentInParent<PlacedPiece>();
        }

        public static bool TryRepair(Inventory inventory, PlacedPiece piece, out string message)
        {
            if (piece == null) { message = "Aim at a built piece to repair it."; return false; }
            if (inventory == null || inventory.GetCount(HomesteadCatalog.Hammer) <= 0) { message = "You need a Hammer to repair."; return false; }
            StructureHealth health = piece.GetComponent<StructureHealth>();
            if (health == null) { message = "This can't be repaired."; return false; }
            if (!health.IsDamaged) { message = "This is already at full health."; return false; }

            string kit = HomesteadCatalog.RepairKitFor(piece.pieceId);
            ResourceDefinition kitDefinition = ItemCatalog.Def(kit);
            string kitName = ItemCatalog.NameOf(kit);
            if (kitDefinition == null || !CraftingSystem.TrySpend(inventory, new List<CraftingCost> { new CraftingCost { resource = kitDefinition, amount = 1 } }))
            {
                message = $"You need a {kitName} to repair this.";
                return false;
            }
            float repaired = health.Repair(health.maxHealth * HomesteadCatalog.RepairFraction);
            GameAudio.PlayAt(Sfx.RepairTick, piece.transform.position, 0.8f, 25f, 0.05f);
            message = $"Repaired {Mathf.CeilToInt(repaired)} HP with a {kitName}.";
            return true;
        }

        public static bool CanDismantle(Inventory inventory, PlacedPiece piece, out string reason)
        {
            reason = null;
            if (piece == null) { reason = "Aim at a built piece to dismantle it."; return false; }
            if (inventory == null || inventory.GetCount(HomesteadCatalog.Hammer) <= 0) { reason = "You need a Hammer to dismantle."; return false; }

            foreach (IItemStore store in piece.GetComponentsInChildren<IItemStore>())
                foreach (KeyValuePair<string, int> line in store.Contents)
                    if (line.Value > 0) { reason = "Empty it first: it still holds items."; return false; }
            foreach (IDismantleGuard guard in piece.GetComponentsInChildren<IDismantleGuard>())
                if (!guard.CanDismantle(out string guardReason))
                {
                    reason = string.IsNullOrEmpty(guardReason) ? "This can't be dismantled right now." : guardReason;
                    return false;
                }

            if (piece.pieceId == "piece_door_frame" && DoorInFrame(piece))
            {
                reason = "Remove the door first.";
                return false;
            }
            PlacedPiece resting = FindResting(piece);
            if (resting != null)
            {
                reason = $"{DisplayName(resting)} rests on this: remove it first.";
                return false;
            }
            return true;
        }

        /// <summary>Another piece standing on this one, if any. Stairs, ramps and doors hold nothing up.</summary>
        public static PlacedPiece FindResting(PlacedPiece piece)
        {
            BuildPlacementRule rule = HomesteadCatalog.RuleFor(piece.pieceId);
            if (rule == BuildPlacementRule.Access || rule == BuildPlacementRule.DoorInFrame) return null;

            Physics.SyncTransforms();
            Vector3 size = HomesteadPlacement.SizeOf(piece);
            bool known = HomesteadCatalog.Find(piece.pieceId) != null;
            Vector3 centre = piece.transform.position;
            Quaternion rotation = piece.transform.rotation;
            float top = centre.y + size.y / 2f;
            if (!known)
            {
                Bounds bounds = HomesteadPlacement.BoundsOf(piece);
                centre = bounds.center;
                top = bounds.max.y;
                rotation = Quaternion.identity;
            }

            Vector3 half = new Vector3(Mathf.Max(size.x / 2f - .05f, .02f), .05f, Mathf.Max(size.z / 2f - .05f, .02f));
            int count = Physics.OverlapBoxNonAlloc(new Vector3(centre.x, top + .06f, centre.z), half, overlaps, rotation, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                PlacedPiece other = overlaps[i].GetComponentInParent<PlacedPiece>();
                if (other != null && other != piece) return other;
            }
            return null;
        }

        private static bool DoorInFrame(PlacedPiece frame)
        {
            Vector3 opening = frame.transform.position + Vector3.down * .4f;
            foreach (PlacedPiece other in PlacedPieceRegistry.All)
                if (other.GetComponent<HomesteadDoorPiece>() != null && (other.transform.position - opening).sqrMagnitude < .25f) return true;
            return false;
        }

        public static string DescribeRefund(BuildPieceDefinition definition, PlacedPiece piece, bool forgiven)
        {
            if (piece != null && piece.wasPlacedFromKit && forgiven)
            {
                string kitId = "kit_" + piece.pieceId;
                ResourceDefinition kit = ItemCatalog.Def(kitId);
                return "1 " + (kit != null ? kit.displayName : kitId.Replace('_', ' '));
            }
            StringBuilder text = new StringBuilder();
            float fraction = HealthFraction(piece);
            if (definition != null)
                foreach (CraftingCost line in definition.craftingCost)
                {
                    if (line == null || line.resource == null) continue;
                    int amount = HomesteadCatalog.RefundAmount(line.amount, fraction, forgiven);
                    if (amount <= 0) continue;
                    if (text.Length > 0) text.Append(", ");
                    text.Append(amount).Append(' ').Append(line.resource.displayName);
                }
            return text.Length > 0 ? text.ToString() : "nothing";
        }

        /// <summary>Hands back the refund and removes the piece. Call CanDismantle first. Returns what was refunded, as text.</summary>
        public static string Dismantle(Inventory inventory, BuildPieceDefinition definition, PlacedPiece piece, bool forgiven)
        {
            string refund = DescribeRefund(definition, piece, forgiven);
            float fraction = HealthFraction(piece);
            if (piece != null && piece.wasPlacedFromKit && forgiven)
                inventory.Add("kit_" + piece.pieceId, 1);
            else if (definition != null)
                foreach (CraftingCost line in definition.craftingCost)
                {
                    if (line == null || line.resource == null) continue;
                    int amount = HomesteadCatalog.RefundAmount(line.amount, fraction, forgiven);
                    if (amount > 0) inventory.Add(line.resource.id, amount);
                }
            GameAudio.PlayAt(Sfx.RepairTick, piece.transform.position, 1f, 30f, 0.1f);
            piece.gameObject.SetActive(false);
            Object.Destroy(piece.gameObject);
            Physics.SyncTransforms();
            return refund;
        }

        private static float HealthFraction(PlacedPiece piece)
        {
            StructureHealth health = piece.GetComponent<StructureHealth>();
            return health != null ? health.Fraction : 1f;
        }

        private static string DisplayName(PlacedPiece piece)
        {
            HomesteadEntry entry = HomesteadCatalog.Find(piece.pieceId);
            return entry != null ? entry.displayName : "Something";
        }
    }
}
