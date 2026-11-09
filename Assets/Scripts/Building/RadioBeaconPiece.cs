using System.Collections.Generic;
using SheltreSim.Objectives;
using SheltreSim.Power;
using SheltreSim.Raids;
using SheltreSim.UI;
using UnityEngine;

namespace SheltreSim.Building
{
    /// <summary>
    /// End-of-game piece. Interacting starts the rescue broadcast, which RaidDirector runs: five minutes of game time
    /// during which this beacon must stay powered (3 kW, served first) and standing while three waves attack the bunker.
    /// It only draws power while it is the broadcasting beacon, so an idle beacon costs nothing. The game keeps running
    /// after victory.
    /// </summary>
    public sealed class RadioBeaconPiece : PowerConsumerDevice
    {
        public float powerKw = 3f;

        private static readonly List<RadioBeaconPiece> all = new List<RadioBeaconPiece>();

        /// <summary>Saved with the game by ObjectiveTracker.</summary>
        public static bool VictoryAnnounced => ObjectiveTracker.Victory;

        /// <summary>Every enabled beacon. A build ghost is enabled once and then disabled, so it leaves again at once.</summary>
        public static IReadOnlyList<RadioBeaconPiece> All => all;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => all.Clear();

        public bool IsBroadcasting => RaidDirector.Active != null && RaidDirector.Active.BroadcastBeacon == this;

        /// <summary>True while the grid is actually feeding the broadcast. Only then does its clock run.</summary>
        public bool Transmitting => Granted && IsBroadcasting;

        public override string PowerName => "Radio Beacon";
        public override float PowerDemand => IsBroadcasting ? powerKw : 0f;
        public override int PowerPriority => 0;

        public override string PowerStatus
        {
            get
            {
                if (ObjectiveTracker.Victory) return "rescue is on its way";
                if (!IsBroadcasting) return "idle";
                string clock = RaidDirector.Clock(RaidDirector.Active.BroadcastRemaining);
                return Granted ? $"broadcasting, {clock} left" : $"broadcast paused, {NoPowerReason()}, {clock} left";
            }
        }

        public override string InteractPrompt =>
            ObjectiveTracker.Victory || IsBroadcasting
                ? $"{PowerName}: {PowerStatus}"
                : $"Press E to start the rescue broadcast (5 min, needs {powerKw:0.#} kW)";

        protected override void JoinGrid()
        {
            base.JoinGrid();
            if (!all.Contains(this)) all.Add(this);
        }

        protected override void LeaveGrid()
        {
            base.LeaveGrid();
            all.Remove(this);
        }

        public override void Interact(GameObject interactor)
        {
            if (interactor == null) return;
            if (ObjectiveTracker.Victory)
            {
                GameMessageBus.Post("The rescue signal is out. Help is on its way.");
                return;
            }
            if (IsBroadcasting)
            {
                GameMessageBus.Post($"{PowerName}: {PowerStatus}");
                return;
            }
            string reason = null;
            if (RaidDirector.Active == null || !RaidDirector.Active.TryBeginBroadcast(this, out reason))
                GameMessageBus.Post(reason ?? "The beacon can't broadcast right now.");
        }
    }
}
