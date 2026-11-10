using SheltreSim.Audio;
using SheltreSim.Building;
using SheltreSim.Enemies;
using SheltreSim.Interaction;
using SheltreSim.Items;
using SheltreSim.Power;
using SheltreSim.Saving;
using SheltreSim.Storage;
using SheltreSim.UI;
using UnityEngine;

namespace SheltreSim.Raids
{
    /// <summary>With power: the warning comes earlier, a klaxon sounds as a raid begins, and the bunker door shuts if nobody's in it.</summary>
    public sealed class AlarmPiece : MonoBehaviour, IPowerConsumer, IInteractable
    {
        private static readonly System.Collections.Generic.List<AlarmPiece> all = new System.Collections.Generic.List<AlarmPiece>();

        public float powerDemand = 0.1f;
        public bool Powered { get; private set; }

        public Vector3 PowerPosition => transform.position;
        public float PowerDemand => powerDemand;
        public int PowerPriority => 0;
        public float HeatOutput => 0f;
        public float CoolingOutput => 0f;
        public string PowerName => "Alarm";
        public void SetPowered(bool powered) => Powered = powered;

        public string InteractPrompt => Powered ? "Alarm: armed" : "Alarm: no power";
        public void Interact(GameObject interactor) => GameMessageBus.Post(Powered ? "The alarm is armed: you get twice the warning before a raid." : "The alarm has no power.");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => all.Clear();

        private void OnEnable()
        {
            all.Add(this);
            PowerGrid.Register(this);
        }

        private void OnDisable()
        {
            all.Remove(this);
            PowerGrid.Unregister(this);
            Powered = false;
        }

        public static bool AnyPoweredNear(Vector3 position, float radius)
        {
            foreach (AlarmPiece alarm in all)
                if (alarm.Powered && (alarm.transform.position - position).sqrMagnitude <= radius * radius) return true;
            return false;
        }

        public static void NotifyRaidBegan()
        {
            bool sounded = false;
            foreach (AlarmPiece alarm in all)
            {
                if (!alarm.Powered) continue;
                sounded = true;
                GameAudio.PlayAt(Sfx.AlarmKlaxon, alarm.transform.position, 1f, 80f);
            }
            if (!sounded) return;
            foreach (InteractableDoor door in InteractableDoor.Registry)
                if (door.TryAutoClose()) GameMessageBus.Post("The alarm shut the bunker door.");
        }
    }
}
