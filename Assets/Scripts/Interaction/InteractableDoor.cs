using SheltreSim.UI;
using UnityEngine;

namespace SheltreSim.Interaction
{
    public class InteractableDoor : MonoBehaviour, IInteractable
    {
        public Transform doorVisual;
        /// <summary>How much beating raid enemies need to break this door down. Patrols don't use it, they force it open.</summary>
        public float maxRaidHealth = 300f;
        private const string RepairKit = "repair_kit_metal";

        private float raidDamage;
        private bool broken;
        private bool warned;
        private bool isOpen;
        private bool initialized;
        private BoxCollider doorCollider;
        private Quaternion closedRotation;
        private Vector3 passageCenter;
        private Vector3 passageNormal;
        private Vector3 closedHalfExtents;
        private Quaternion closedColliderRotation;

        public string InteractPrompt
        {
            get
            {
                if (broken) return $"Door broken down. Press E to repair (1 {SheltreSim.Crafting.ItemCatalog.NameOf(RepairKit)})";
                string action = isOpen ? "Press E to close" : "Press E to open";
                return raidDamage > 0f ? $"{action} ({RaidHealth:0}/{maxRaidHealth:0})" : action;
            }
        }

        public bool IsOpen => isOpen;
        /// <summary>Broken down by a raid. Stays open until repaired.</summary>
        public bool IsBroken => broken;
        public float RaidHealth => Mathf.Max(0f, maxRaidHealth - raidDamage);

        // These describe the doorway, not the leaf after it has swung to one side.
        public Vector3 PassageCenter { get { Initialize(); return passageCenter; } }
        public Vector3 PassageNormal { get { Initialize(); return passageNormal; } }

        private void Awake() => Initialize();

        private void Initialize()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            doorCollider = GetComponent<BoxCollider>();
            closedRotation = doorVisual != null ? doorVisual.localRotation : Quaternion.identity;
            passageCenter = doorCollider != null
                ? transform.TransformPoint(doorCollider.center)
                : transform.position;
            passageNormal = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            closedColliderRotation = transform.rotation;
            if (doorCollider != null)
            {
                Vector3 scale = transform.lossyScale;
                scale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
                closedHalfExtents = Vector3.Scale(doorCollider.size * 0.5f, scale);
            }
        }

        public void ForceOpen()
        {
            if (isOpen)
            {
                return;
            }

            RestoreState(true);
            SheltreSim.Audio.GameAudio.PlayAt(SheltreSim.Audio.Sfx.DoorBash, passageCenter, 1f, 45f);
            GameMessageBus.Post("A raider forced the door open!");
        }

        /// <summary>A raid enemy's hit. A closed door has to be beaten down over time; ForceOpen is for patrols.</summary>
        public void TakeRaidDamage(float amount)
        {
            Initialize();
            if (broken || isOpen || amount <= 0f || float.IsNaN(amount) || float.IsInfinity(amount)) return;
            // A door that is a build piece has one health bar, the piece's.
            SheltreSim.Building.StructureHealth structure = GetComponentInParent<SheltreSim.Building.StructureHealth>();
            if (structure != null)
            {
                structure.TakeDamage(amount);
                return;
            }
            raidDamage = Mathf.Min(maxRaidHealth, raidDamage + amount);
            SheltreSim.Audio.GameAudio.PlayAt(SheltreSim.Audio.Sfx.DoorBash, passageCenter, 0.8f, 45f, 0.08f);
            if (!warned)
            {
                warned = true;
                GameMessageBus.Post("Raiders are breaking down the door!");
            }
            if (raidDamage < maxRaidHealth) return;
            broken = true;
            RestoreState(true);
            SheltreSim.Audio.GameAudio.PlayAt(SheltreSim.Audio.Sfx.StructureBreak, passageCenter, 1f, 45f);
            GameMessageBus.Post("The door was broken down!");
        }

        /// <summary>Saved through the raid system. Runs after SaveManager restored the pose, and overrides it for a broken door.</summary>
        public void RestoreRaidState(float restoredHealth, bool restoredBroken)
        {
            Initialize();
            raidDamage = Mathf.Clamp(maxRaidHealth - restoredHealth, 0f, maxRaidHealth);
            broken = restoredBroken;
            warned = raidDamage > 0f;
            if (broken) RestoreState(true);
        }

        /// <summary>A raid is over: dents stay dents, but the frame is braced again. A broken door still needs its kit.</summary>
        public void RecoverAfterRaid()
        {
            warned = false;
            if (!broken) raidDamage = 0f;
        }

        /// <summary>Automatic closing (alarm). False when broken, already closed, or someone stands in the doorway.</summary>
        public bool TryAutoClose()
        {
            Initialize();
            if (broken || !isOpen || DoorwayOccupied()) return false;
            RestoreState(false);
            SheltreSim.Audio.GameAudio.PlayAt(SheltreSim.Audio.Sfx.DoorClose, passageCenter, 0.85f, 28f, 0.03f);
            return true;
        }

        private void Repair(GameObject interactor)
        {
            SheltreSim.Items.Inventory inventory = interactor != null ? interactor.GetComponentInParent<SheltreSim.Items.Inventory>() : null;
            if (inventory == null) return;
            if (!inventory.TryConsume(RepairKit, 1))
            {
                SheltreSim.Audio.GameAudio.Play2D(SheltreSim.Audio.Sfx.UiDeny, 0.35f);
                GameMessageBus.Post($"You need 1 {SheltreSim.Crafting.ItemCatalog.NameOf(RepairKit)} to repair the door.");
                return;
            }
            broken = false;
            raidDamage = 0f;
            warned = false;
            SheltreSim.Audio.GameAudio.PlayAt(SheltreSim.Audio.Sfx.RepairTick, passageCenter, 0.9f, 28f);
            GameMessageBus.Post("The door is repaired.");
        }

        /// <summary>Restores a saved pose without replaying interaction feedback.</summary>
        public void RestoreState(bool open)
        {
            Initialize();
            isOpen = open;
            if (doorVisual != null)
            {
                doorVisual.localRotation = closedRotation * Quaternion.Euler(0f, open ? 90f : 0f, 0f);
            }
            else if (doorCollider != null)
            {
                doorCollider.enabled = !open;
            }
        }

        public void Interact(GameObject interactor)
        {
            if (Time.timeScale <= 0f)
            {
                return;
            }

            Initialize();
            if (broken)
            {
                Repair(interactor);
                return;
            }
            if (isOpen && DoorwayOccupied())
            {
                SheltreSim.Audio.GameAudio.Play2D(SheltreSim.Audio.Sfx.UiDeny, 0.35f);
                GameMessageBus.Post("The doorway is blocked. Move clear before closing it.");
                return;
            }

            RestoreState(!isOpen);
            SheltreSim.Audio.GameAudio.PlayAt(isOpen ? SheltreSim.Audio.Sfx.DoorOpen : SheltreSim.Audio.Sfx.DoorClose, passageCenter, 0.85f, 28f, 0.03f);
        }

        private bool DoorwayOccupied()
        {
            if (doorCollider == null)
            {
                return false;
            }

            Collider[] overlaps = Physics.OverlapBox(passageCenter, closedHalfExtents,
                closedColliderRotation, ~0, QueryTriggerInteraction.Ignore);
            foreach (Collider other in overlaps)
            {
                if (other.GetComponentInParent<CharacterController>() != null)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
