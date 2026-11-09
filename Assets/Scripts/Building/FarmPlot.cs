using SheltreSim.Crafting;
using SheltreSim.Interaction;
using SheltreSim.Items;
using SheltreSim.Power;
using SheltreSim.Saving;
using SheltreSim.Storage;
using SheltreSim.UI;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SheltreSim.Building
{
    /// <summary>
    /// Farm Plot: plant a seed (E), wait, harvest vegetables and a few seeds back. A simple timer like
    /// WaterCollectorPiece. Save with Capture/Restore (state 0 empty, 1 growing, 2 ready). Fertilizer and a powered
    /// Irrigator each double the growth speed. Fertilizer lasts for one crop and is saved through IPieceState.
    /// </summary>
    public class FarmPlot : MonoBehaviour, IInteractable, IPieceState
    {
        public const int Empty = 0, Growing = 1, Ready = 2;
        public const float MaxSpeedMultiplier = 4f;
        private const string FertilizerId = "fertilizer";

        private static readonly List<FarmPlot> all = new List<FarmPlot>();

        /// <summary>Every enabled plot, for automation.</summary>
        public static IReadOnlyList<FarmPlot> All => all;

        [Min(1f)] public float growSeconds = 240f;
        public int vegetableYield = 3;
        public int seedYield = 1;

        private int state;
        private float elapsed;
        private bool fertilized;

        [Serializable]
        private class PieceState { public bool fertilized; }

        public int State => state;
        public float GrowElapsed => elapsed;
        public bool Fertilized => fertilized;

        /// <summary>How fast the crop grows right now.</summary>
        public float SpeedMultiplier
        {
            get
            {
                float speed = 1f;
                if (IrrigatorPiece.AnyIrrigating(transform.position)) speed *= 2f;
                if (state == Growing && fertilized) speed *= 2f;
                return Mathf.Min(speed, MaxSpeedMultiplier);
            }
        }

        private int SecondsLeft => Mathf.CeilToInt(Mathf.Max(0f, growSeconds - elapsed) / SpeedMultiplier);

        public string InteractPrompt => state == Empty ? "Press E to plant a Seed"
            : state == Growing ? $"Crops growing... ({SecondsLeft}s{(SpeedMultiplier > 1f ? $", x{SpeedMultiplier:0} speed" : "")}){(fertilized ? "" : ". Press E to use Fertilizer")}"
            : $"Press E to harvest {vegetableYield} Vegetables";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => all.Clear();

        private void OnEnable() { if (!all.Contains(this)) all.Add(this); }
        private void OnDisable() => all.Remove(this);

        public string CapturePieceState() => state == Growing && fertilized ? JsonUtility.ToJson(new PieceState { fertilized = true }) : null;

        public void RestorePieceState(string saved)
        {
            PieceState parsed = PowerNetworks.ParseState<PieceState>(saved);
            if (parsed != null) fertilized = parsed.fertilized;
        }

        /// <summary>Save API: state and growth timer.</summary>
        public void Capture(out int savedState, out float savedElapsed) { savedState = state; savedElapsed = elapsed; }

        /// <summary>Restore API, mirrors WaterCollectorPiece.RestoreState. Invalid input is clamped.</summary>
        public void RestoreState(int savedState, float growElapsed)
        {
            state = Mathf.Clamp(savedState, Empty, Ready);
            elapsed = state == Growing && !float.IsNaN(growElapsed) && !float.IsInfinity(growElapsed) ? Mathf.Clamp(growElapsed, 0f, growSeconds) : 0f;
            fertilized = false; // SaveManager restores the piece state after this.
        }

        public void Restore(int savedState, float growElapsed) => RestoreState(savedState, growElapsed);

        private void Update() => AdvanceGrowth(Time.deltaTime);

        /// <summary>Game time only. Public so a probe can drive the exact same code in fixed steps.</summary>
        public void AdvanceGrowth(float seconds)
        {
            if (state != Growing) return;
            elapsed += seconds * SpeedMultiplier;
            if (elapsed >= growSeconds) { state = Ready; elapsed = 0f; fertilized = false; }
        }

        /// <summary>Harvests a ready crop into the nearest crate in reach. Without one the crop stays ready.</summary>
        public bool TryAutoHarvest(Vector3 from, float radius)
        {
            if (state != Ready) return false;
            IItemStore crate = StorageRegistry.Nearest(from, radius);
            if (crate == null) return false;
            crate.Add("vegetables", vegetableYield);
            if (seedYield > 0) crate.Add("seeds", seedYield);
            state = Empty; elapsed = 0f; fertilized = false;
            SheltreSim.Audio.GameAudio.PlayAt(SheltreSim.Audio.Sfx.GatherLeaves, transform.position, 0.6f, 15f);
            return true;
        }

        public void Interact(GameObject interactor)
        {
            if (interactor == null) return;
            Inventory inv = interactor.GetComponentInParent<Inventory>();
            if (inv == null) { Debug.LogError($"'{interactor.name}' has no Inventory — cannot farm."); return; }
            if (state == Empty)
            {
                if (!inv.TryConsume("seeds", 1))
                {
                    SheltreSim.Audio.GameAudio.Play2D(SheltreSim.Audio.Sfx.UiDeny, 0.35f);
                    GameMessageBus.Post("You need Seeds to plant.");
                    return;
                }
                state = Growing; elapsed = 0f; fertilized = false;
                SheltreSim.Audio.GameAudio.PlayAt(SheltreSim.Audio.Sfx.GatherLeaves, transform.position, 0.7f, 15f);
                GameMessageBus.Post("Planted a seed. It will take a few minutes.");
            }
            else if (state == Growing)
            {
                if (!fertilized && inv.TryConsume(FertilizerId, 1))
                {
                    fertilized = true;
                    SheltreSim.Audio.GameAudio.PlayAt(SheltreSim.Audio.Sfx.GatherLeaves, transform.position, 0.7f, 15f);
                    GameMessageBus.Post($"Fertilized the crop. It grows twice as fast ({SecondsLeft}s left).");
                    return;
                }
                GameMessageBus.Post($"Still growing ({SecondsLeft}s left).{(fertilized ? "" : " You have no Fertilizer.")}");
            }
            else
            {
                inv.Add("vegetables", vegetableYield);
                if (seedYield > 0) inv.Add("seeds", seedYield);
                state = Empty; elapsed = 0f; fertilized = false;
                SheltreSim.Audio.GameAudio.PlayAt(SheltreSim.Audio.Sfx.GatherLeaves, transform.position, 0.9f, 20f);
                GameMessageBus.Post($"Harvested {vegetableYield} Vegetables and {seedYield} Seed.");
            }
        }
    }
}
