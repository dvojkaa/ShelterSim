using SheltreSim.Interaction;
using UnityEngine;

namespace SheltreSim.Core
{
    /// <summary>The laptop on the folding table. Opens the project and the shop until the bombs fall.</summary>
    public sealed class SiteTerminal : MonoBehaviour, IInteractable
    {
        public string InteractPrompt => HomeBase.Active != null && HomeBase.Active.CanShop
            ? "Press E to open the bunker project"
            : "The laptop is dead";

        public void Interact(GameObject interactor)
        {
            if (HomeBase.Active == null || !HomeBase.Active.CanShop) return;
            BunkerTerminalUI.Open(interactor);
        }
    }
}
