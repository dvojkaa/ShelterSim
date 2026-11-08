using SheltreSim.Crafting;
using SheltreSim.Interaction;
using SheltreSim.Items;
using SheltreSim.UI;
using UnityEngine;

namespace SheltreSim.Building
{
    /// <summary>
    /// Farm Plot: plant a seed (E), wait, harvest vegetables and a few seeds back. A simple timer like
    /// WaterCollectorPiece. Save with Capture/Restore (state 0 empty, 1 growing, 2 ready).
    /// </summary>
    public class FarmPlot : MonoBehaviour, IInteractable
    {
        public const int Empty = 0, Growing = 1, Ready = 2;

        [Min(1f)] public float growSeconds = 240f;
        public int vegetableYield = 3;
        public int seedYield = 1;

        private int state;
        private float elapsed;

        public int State => state;
        public float GrowElapsed => elapsed;

        public string InteractPrompt => state == Empty ? "Press E to plant a Seed"
            : state == Growing ? $"Crops growing... ({Mathf.CeilToInt(Mathf.Max(0f, growSeconds - elapsed))}s)"
            : $"Press E to harvest {vegetableYield} Vegetables";

        /// <summary>Save API: state and growth timer.</summary>
        public void Capture(out int savedState, out float savedElapsed) { savedState = state; savedElapsed = elapsed; }

        /// <summary>Restore API, mirrors WaterCollectorPiece.RestoreState. Invalid input is clamped.</summary>
        public void RestoreState(int savedState, float growElapsed)
        {
            state = Mathf.Clamp(savedState, Empty, Ready);
            elapsed = state == Growing && !float.IsNaN(growElapsed) && !float.IsInfinity(growElapsed) ? Mathf.Clamp(growElapsed, 0f, growSeconds) : 0f;
        }

        public void Restore(int savedState, float growElapsed) => RestoreState(savedState, growElapsed);

        private void Update()
        {
            if (state != Growing) return;
            elapsed += Time.deltaTime;
            if (elapsed >= growSeconds) { state = Ready; elapsed = 0f; }
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
                state = Growing; elapsed = 0f;
                SheltreSim.Audio.GameAudio.PlayAt(SheltreSim.Audio.Sfx.GatherLeaves, transform.position, 0.7f, 15f);
                GameMessageBus.Post("Planted a seed. It will take a few minutes.");
            }
            else if (state == Growing)
            {
                GameMessageBus.Post($"Still growing ({Mathf.CeilToInt(Mathf.Max(0f, growSeconds - elapsed))}s left).");
            }
            else
            {
                inv.Add("vegetables", vegetableYield);
                if (seedYield > 0) inv.Add("seeds", seedYield);
                state = Empty; elapsed = 0f;
                SheltreSim.Audio.GameAudio.PlayAt(SheltreSim.Audio.Sfx.GatherLeaves, transform.position, 0.9f, 20f);
                GameMessageBus.Post($"Harvested {vegetableYield} Vegetables and {seedYield} Seed.");
            }
        }
    }
}
