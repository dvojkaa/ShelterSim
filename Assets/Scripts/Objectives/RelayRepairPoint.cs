using SheltreSim.Interaction;
using SheltreSim.Items;
using SheltreSim.UI;
using UnityEngine;

namespace SheltreSim.Objectives
{
    /// <summary>
    /// The broken cabinet on a military landmark. Holds no state: the chunk it stands in can be unloaded
    /// and rebuilt at any time, so it asks ObjectiveTracker what happened here.
    /// </summary>
    public sealed class RelayRepairPoint : MonoBehaviour, IInteractable
    {
        public string landmarkId;
        public Renderer lamp;

        private static readonly Color Dead = new Color(.45f, .08f, .06f);
        private static readonly Color Online = new Color(.2f, .9f, .3f);
        private MaterialPropertyBlock block;

        public string InteractPrompt
        {
            get
            {
                ObjectiveTracker tracker = ObjectiveTracker.Ensure();
                if (tracker.IsRelayRepairedAt(landmarkId)) return "Relay repaired. Contact it from a powered Radio Console.";
                if (tracker.RelayRepaired) return "A dead relay. The network already runs through another outpost.";
                string missing = tracker.RelayMissingText(FindInventory());
                return string.IsNullOrEmpty(missing)
                    ? "Press E to repair the relay (" + ObjectiveTracker.RelayCostText() + ")"
                    : "Broken relay. Missing " + missing;
            }
        }

        private void Start() => RefreshLamp();

        public void Interact(GameObject interactor)
        {
            if (interactor == null) return;
            Inventory inventory = interactor.GetComponentInParent<Inventory>();
            ObjectiveTracker.Ensure().TryRepairRelay(landmarkId, inventory, out string result);
            GameMessageBus.Post(result);
            RefreshLamp();
        }

        private void RefreshLamp()
        {
            if (lamp == null) return;
            if (block == null) block = new MaterialPropertyBlock();
            lamp.GetPropertyBlock(block);
            block.SetColor("_Color", ObjectiveTracker.Ensure().IsRelayRepairedAt(landmarkId) ? Online : Dead);
            lamp.SetPropertyBlock(block);
        }

        private static Inventory FindInventory()
        {
            var player = ObjectiveTracker.Ensure().PlayerTransform;
            return player != null ? player.GetComponent<Inventory>() : null;
        }
    }
}
