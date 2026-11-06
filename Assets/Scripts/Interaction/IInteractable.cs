namespace SheltreSim.Interaction
{
    /// <summary>Contract for anything the player can interact with via PlayerInteractor's raycast.</summary>
    public interface IInteractable
    {
        string InteractPrompt { get; }
        void Interact(UnityEngine.GameObject interactor);
    }
}
