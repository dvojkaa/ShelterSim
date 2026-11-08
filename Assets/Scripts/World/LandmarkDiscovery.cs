using UnityEngine;

namespace SheltreSim.World
{
    /// <summary>Discovery belongs to persistent player progression, not disposable streamed chunk state.</summary>
    public sealed class LandmarkDiscovery : MonoBehaviour
    {
        public string stableId;
        public string title;
        float nextCheck;
        bool discovered;

        void Update()
        {
            if (discovered || Time.time < nextCheck) return;
            nextCheck = Time.time + .5f;
            var progression = ExpeditionProgression.Active;
            if (progression == null) return;
            Vector3 offset = progression.transform.position - transform.position;
            // Horizontal proximity stays meaningful even where the destination bridges a hillside.
            offset.y = 0;
            if (offset.sqrMagnitude > 35f * 35f) return;
            progression.DiscoverLandmark(stableId, title);
            discovered = true;
        }
    }
}
