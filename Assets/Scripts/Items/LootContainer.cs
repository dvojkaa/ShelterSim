using System.Collections.Generic;
using System.Text;
using SheltreSim.Interaction;
using SheltreSim.UI;
using UnityEngine;

namespace SheltreSim.Items
{
    [System.Serializable]
    public sealed class LootOpenPart
    {
        public Transform part;
        public Vector3 openLocalPosition;
        public Vector3 openLocalEulerAngles;
    }

    /// <summary>One rolled item stack inside a loot crate.</summary>
    [System.Serializable]
    public struct LootEntry
    {
        public ResourceDefinition resource;
        public int amount;
    }

    /// <summary>
    /// A single-use searchable container (furniture, a car trunk, etc.) placed at a
    /// "LootSpawn" marker by the scene-population tool. Unlike ResourceNode, it isn't a
    /// repeatable/charge-limited gather point — it's searched exactly once, and the
    /// outcome (empty or not) is pre-rolled externally via hasLoot/entries rather than
    /// decided by this component.
    ///
    /// Holds a LIST of entries (not a single resource/amount pair) since the M10 QC-era
    /// loot rebalance lets one crate contain more than one item stack — a playtester
    /// complaint ("10 crates, got 2 Water and nothing else") traced to crates handing out
    /// at most one stack each, not just the empty-crate rate.
    /// </summary>
    public class LootContainer : MonoBehaviour, IInteractable
    {
        public List<LootEntry> entries = new List<LootEntry>();
        public bool hasLoot = true;
        [Tooltip("Optional authored pivots. If empty, conventional Door_L, Door_R, Drawer_* and Lid children are detected.")]
        public LootOpenPart[] openParts = System.Array.Empty<LootOpenPart>();

        private bool searched;
        private struct OpenPose
        {
            public Transform part;
            public Vector3 closedPosition, openPosition;
            public Quaternion closedRotation, openRotation;
        }
        private OpenPose[] openPoses = System.Array.Empty<OpenPose>();
        private float openElapsed;
        private bool isAnimatingOpen;
        private const float OpenDuration = .25f;

        private void Awake() => CacheOpenParts();

        private void Update()
        {
            if (!isAnimatingOpen) return;
            openElapsed = Mathf.Min(OpenDuration, openElapsed + Time.deltaTime);
            float t = Mathf.SmoothStep(0f, 1f, openElapsed / OpenDuration);
            ApplyOpenPose(t);
            if (openElapsed >= OpenDuration) isAnimatingOpen = false;
        }

        private void OnDisable()
        {
            if (!isAnimatingOpen) return;
            isAnimatingOpen = false;
            ApplyOpenPose(1f);
        }

        private void CacheOpenParts()
        {
            if (openParts == null || openParts.Length == 0)
            {
                var detected = new List<LootOpenPart>();
                foreach (Transform child in GetComponentsInChildren<Transform>(true))
                {
                    if (child == transform) continue;
                    string name = child.name;
                    if (name == "Door_L" || name == "Door") detected.Add(new LootOpenPart { part = child, openLocalEulerAngles = new Vector3(0f, -90f, 0f) });
                    else if (name == "Door_R") detected.Add(new LootOpenPart { part = child, openLocalEulerAngles = new Vector3(0f, 90f, 0f) });
                    else if (name == "Lid") detected.Add(new LootOpenPart { part = child, openLocalEulerAngles = new Vector3(-90f, 0f, 0f) });
                    else if (name.StartsWith("Drawer_", System.StringComparison.Ordinal))
                        detected.Add(new LootOpenPart { part = child, openLocalPosition = child.localPosition + child.localRotation * Vector3.forward * .25f });
                }
                openParts = detected.ToArray();
            }

            var poses = new List<OpenPose>();
            foreach (LootOpenPart definition in openParts)
            {
                if (definition == null || definition.part == null) continue;
                poses.Add(new OpenPose
                {
                    part = definition.part,
                    closedPosition = definition.part.localPosition,
                    openPosition = definition.openLocalPosition,
                    closedRotation = definition.part.localRotation,
                    openRotation = definition.part.localRotation * Quaternion.Euler(definition.openLocalEulerAngles)
                });
            }
            openPoses = poses.ToArray();
            if (searched) ApplyOpenPose(1f);
        }

        /// <summary>A drawer slides, a cupboard door creaks, anything else (a crate, a lid) is the plain crate sound.</summary>
        private string OpenSoundKey()
        {
            if (openPoses == null || openPoses.Length == 0) CacheOpenParts();
            bool door = false;
            foreach (OpenPose pose in openPoses)
            {
                if (pose.part == null) continue;
                if (pose.part.name.StartsWith("Drawer_", System.StringComparison.Ordinal)) return SheltreSim.Audio.Sfx.DrawerOpen;
                if (pose.part.name.StartsWith("Door", System.StringComparison.Ordinal)) door = true;
            }
            return door ? SheltreSim.Audio.Sfx.CabinetOpen : SheltreSim.Audio.Sfx.CrateOpen;
        }

        private void BeginOpen(bool animate)
        {
            if (openPoses == null || openPoses.Length == 0) CacheOpenParts();
            if (!animate || !Application.isPlaying || !isActiveAndEnabled)
            {
                isAnimatingOpen = false;
                ApplyOpenPose(1f);
                return;
            }
            openElapsed = 0f;
            isAnimatingOpen = true;
        }

        private void ApplyOpenPose(float t)
        {
            if (openPoses == null) return;
            foreach (OpenPose pose in openPoses)
            {
                if (pose.part == null) continue;
                pose.part.SetLocalPositionAndRotation(Vector3.LerpUnclamped(pose.closedPosition, pose.openPosition, t),
                    Quaternion.SlerpUnclamped(pose.closedRotation, pose.openRotation, t));
            }
        }

        public string InteractPrompt => searched
            ? "Already searched"
            : "Press E to search";

        /// <summary>Exposed for SaveManager, which persists this via a hierarchy-path id.</summary>
        public bool Searched => searched;

        /// <summary>
        /// Restores save-loaded state. Called by SaveManager after matching this instance to
        /// a saved entry; kept separate from the private field so <c>searched</c> itself can
        /// stay private to the normal Interact() flow.
        /// </summary>
        public void RestoreSearchedState(bool isSearched)
        {
            searched = isSearched;
            if (searched) BeginOpen(false);
            else
            {
                isAnimatingOpen = false;
                if (openPoses == null || openPoses.Length == 0) CacheOpenParts();
                ApplyOpenPose(0f);
            }
        }

        public void Interact(GameObject interactor)
        {
            if (interactor == null)
            {
                return;
            }
            if (searched)
            {
                GameMessageBus.Post("Already searched — nothing left.");
                return;
            }

            if (!hasLoot || entries == null || entries.Count == 0)
            {
                searched = true;
                BeginOpen(true);
                SheltreSim.Audio.GameAudio.PlayAt(OpenSoundKey(), transform.position, 0.85f, 20f);
                SheltreSim.Audio.GameAudio.Play2D(SheltreSim.Audio.Sfx.LootEmpty, 0.4f, 0f, 0.5f);
                GameMessageBus.Post("Searched — found nothing.");
                return;
            }

            Inventory inv = interactor.GetComponent<Inventory>();
            if (inv == null)
            {
                inv = interactor.GetComponentInParent<Inventory>();
            }

            if (inv == null)
            {
                Debug.LogError($"'{interactor.name}' has no Inventory component — cannot loot.");
                return;
            }

            searched = true;
            BeginOpen(true);
            StringBuilder found = new StringBuilder();
            for (int i = 0; i < entries.Count; i++)
            {
                LootEntry entry = entries[i];
                if (entry.resource == null || string.IsNullOrEmpty(entry.resource.id) || entry.amount <= 0)
                {
                    continue;
                }

                inv.Add(entry.resource.id, entry.amount);

                if (found.Length > 0)
                {
                    found.Append(", ");
                }
                found.Append(entry.amount).Append(' ').Append(entry.resource.displayName);
            }

            SheltreSim.Audio.GameAudio.PlayAt(OpenSoundKey(), transform.position, 0.85f, 20f);
            SheltreSim.Audio.GameAudio.Play2D(found.Length > 0 ? SheltreSim.Audio.Sfx.LootFound : SheltreSim.Audio.Sfx.LootEmpty, 0.55f, 0f, 0.5f);
            GameMessageBus.Post(found.Length > 0 ? $"Found {found}." : "Searched — found nothing.");
        }
    }
}
