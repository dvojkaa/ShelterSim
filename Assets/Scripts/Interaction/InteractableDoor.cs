using System.Collections.Generic;
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
        private DoorVisualTween visualTween;

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

        private void Update() => visualTween?.Tick(Time.deltaTime);

        private void OnDisable() => visualTween?.Finish();

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

            SetState(true, true, true);
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
            SetState(true, true, true);
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
            RestoreState(false, true);
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

        /// <summary>Restores a saved pose without feedback. Save loads snap unless animation is explicitly requested.</summary>
        public void RestoreState(bool open, bool animate = false) => SetState(open, animate, false);

        private void SetState(bool open, bool animate, bool forced)
        {
            Initialize();
            isOpen = open;
            if (doorVisual != null)
            {
                if (visualTween == null) visualTween = new DoorVisualTween(doorVisual);
                visualTween.SetPose(closedRotation * Quaternion.Euler(0f, open ? 90f : 0f, 0f),
                    animate && Application.isPlaying && isActiveAndEnabled,
                    forced ? .12f : open ? .35f : .3f, open ? forced ? 10f : 4f : 0f);
            }
            else if (doorCollider != null)
            {
                doorCollider.enabled = !open;
            }
            Physics.SyncTransforms();
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

            RestoreState(!isOpen, true);
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

    /// <summary>
    /// Keeps the real hinge (and all its colliders) at the logical pose, while mesh-only copies swing.
    /// Door leaves can share their transform with a collider, so rotating their transforms would delay collision.
    /// No coroutines: a new request continues from the displayed pose and replaces the previous tween.
    /// </summary>
    internal sealed class DoorVisualTween
    {
        private readonly Transform hinge;
        private readonly List<MeshRenderer> hidden = new List<MeshRenderer>();
        private readonly List<bool> previousForceRenderingOff = new List<bool>();
        private Transform visualRoot;
        private Quaternion start;
        private Quaternion target;
        private Quaternion displayed;
        private float elapsed;
        private float duration;
        private float overshoot;

        public DoorVisualTween(Transform hinge) => this.hinge = hinge;

        public void SetPose(Quaternion rotation, bool animate, float seconds, float overshootDegrees)
        {
            Quaternion from = visualRoot != null ? displayed : hinge.localRotation;
            Finish();
            target = rotation;
            hinge.localRotation = target;
            float angle = Quaternion.Angle(from, target);
            if (!animate || angle < .01f) return;

            // Rebuild only the mesh hierarchy, preserving nested rotations/scales without cloning scripts or colliders.
            MeshRenderer[] sources = hinge.GetComponentsInChildren<MeshRenderer>(true);
            if (sources.Length == 0) return;
            visualRoot = new GameObject("DoorMotionVisual").transform;
            visualRoot.SetParent(hinge, false);
            var copies = new Dictionary<Transform, Transform> { { hinge, visualRoot } };
            foreach (MeshRenderer source in sources)
            {
                MeshFilter mesh = source.GetComponent<MeshFilter>();
                if (mesh == null || mesh.sharedMesh == null) continue;
                Transform copy = CopyTransform(source.transform, copies);
                copy.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh.sharedMesh;
                MeshRenderer renderer = copy.gameObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = source.sharedMaterials;
                renderer.enabled = source.enabled;
                renderer.forceRenderingOff = source.forceRenderingOff;
                renderer.shadowCastingMode = source.shadowCastingMode;
                renderer.receiveShadows = source.receiveShadows;
                renderer.lightProbeUsage = source.lightProbeUsage;
                renderer.reflectionProbeUsage = source.reflectionProbeUsage;
                renderer.renderingLayerMask = source.renderingLayerMask;
                renderer.lightmapIndex = source.lightmapIndex;
                renderer.lightmapScaleOffset = source.lightmapScaleOffset;
                if (source.HasPropertyBlock())
                {
                    var properties = new MaterialPropertyBlock();
                    source.GetPropertyBlock(properties);
                    renderer.SetPropertyBlock(properties);
                }
                hidden.Add(source);
                previousForceRenderingOff.Add(source.forceRenderingOff);
                source.forceRenderingOff = true;
            }
            start = from;
            displayed = from;
            elapsed = 0f;
            duration = seconds;
            overshoot = overshootDegrees / angle;
            ApplyDisplayedPose();
        }

        private Transform CopyTransform(Transform source, Dictionary<Transform, Transform> copies)
        {
            if (copies.TryGetValue(source, out Transform copy)) return copy;
            Transform parent = CopyTransform(source.parent, copies);
            copy = new GameObject(source.name).transform;
            copy.SetParent(parent, false);
            copy.localPosition = source.localPosition;
            copy.localRotation = source.localRotation;
            copy.localScale = source.localScale;
            copy.gameObject.layer = source.gameObject.layer;
            copy.gameObject.SetActive(source.gameObject.activeSelf);
            copies.Add(source, copy);
            return copy;
        }

        public void Tick(float deltaTime)
        {
            if (visualRoot == null) return;
            elapsed += deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            if (t >= 1f)
            {
                Finish();
                return;
            }
            // Open quickly, settle the overshoot; close with a gradually accelerating swing.
            float progress;
            if (overshoot > 0f)
            {
                const float peak = .78f;
                progress = t < peak
                    ? (1f + overshoot) * (1f - Mathf.Pow(1f - t / peak, 3f))
                    : Mathf.Lerp(1f + overshoot, 1f, Mathf.SmoothStep(0f, 1f, (t - peak) / (1f - peak)));
            }
            else progress = t * t;
            displayed = Quaternion.SlerpUnclamped(start, target, progress);
            ApplyDisplayedPose();
        }

        private void ApplyDisplayedPose() => visualRoot.localRotation = Quaternion.Inverse(target) * displayed;

        public void Finish()
        {
            for (int i = 0; i < hidden.Count; i++)
                if (hidden[i] != null) hidden[i].forceRenderingOff = previousForceRenderingOff[i];
            hidden.Clear();
            previousForceRenderingOff.Clear();
            if (visualRoot == null) return;
            visualRoot.gameObject.SetActive(false);
            // Destroy is deferred; detach now so another request cannot copy the old visual subtree.
            visualRoot.SetParent(null, false);
            Object.Destroy(visualRoot.gameObject);
            visualRoot = null;
        }
    }
}
