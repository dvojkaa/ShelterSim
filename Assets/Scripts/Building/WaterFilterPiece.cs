using SheltreSim.Interaction;
using SheltreSim.Items;
using SheltreSim.UI;
using UnityEngine;

namespace SheltreSim.Building
{
    /// <summary>Water Filter: converts every Dirty Water you carry into clean Water (1:1, no fuel needed, unlike boiling).</summary>
    public class WaterFilterPiece : MonoBehaviour, IInteractable
    {
        public string InteractPrompt => "Press E to filter Dirty Water";

        public void Interact(GameObject interactor)
        {
            if (interactor == null) return;
            Inventory inv = interactor.GetComponentInParent<Inventory>();
            if (inv == null) return;
            int dirty = inv.GetCount("dirty_water");
            if (dirty <= 0)
            {
                SheltreSim.Audio.GameAudio.Play2D(SheltreSim.Audio.Sfx.UiDeny, 0.35f);
                GameMessageBus.Post("You have no Dirty Water to filter.");
                return;
            }
            if (!inv.TryConsume("dirty_water", dirty)) return;
            inv.Add("water", dirty);
            SheltreSim.Audio.GameAudio.PlayAt(SheltreSim.Audio.Sfx.Drink, transform.position, 0.6f, 15f, 0.05f, 0f, 1.1f);
            GameMessageBus.Post($"Filtered {dirty} Dirty Water into clean Water.");
        }
    }
}
