using System.Collections.Generic;
using SheltreSim.Enemies;
using UnityEngine;

namespace SheltreSim.Bestiary
{
    public enum PoseKind
    {
        Rest, ArmsUp, Slam, Crouch, Lunge, Inhale, Scream, WindLeft, SweepLeft, WindRight, SweepRight,
        Planted, WideSwing, Buried, Guard, ShieldRest, Stagger, Low, Paw, Charge, Gore,
    }

    /// <summary>
    /// The attack telegraphs: rotations of the model's named parts on top of the rest pose (and the walk cycle where one drives
    /// that part), blended in LateUpdate. The brains only call Set(kind). Deliberately small, so GPT's animation clips can
    /// replace it: keep the PoseKind names as the clip names. Time.deltaTime drives the blend, so it freezes with the game.
    /// </summary>
    public sealed class EnemyPose : MonoBehaviour
    {
        /// <summary>Sign of a local-X rotation that swings a hanging limb forward. Flip if GPT's parts raise backwards on screen.</summary>
        public const float Forward = -1f;
        /// <summary>Sign of a local-Z rotation that lifts Arm_R away from the body (Arm_L uses the opposite).</summary>
        public const float Outward = 1f;

        private static readonly string[] PartNames =
            { "Torso", "Head", "Arm_L", "Arm_R", "Leg_L", "Leg_R", "Foreleg_L", "Foreleg_R", "Hindleg_L", "Hindleg_R" };
        // Parts a walk animator rewrites each Update. The others are posed on their rest rotation.
        private static readonly bool[] WalkDriven = { true, false, true, true, true, true, true, true, true, true };

        private struct Shape
        {
            public Vector3[] euler; // per PartNames index
            public float drop;      // metres the whole model sinks
        }

        private static Dictionary<PoseKind, Shape> shapes;

        public PoseKind Current { get; private set; } = PoseKind.Rest;
        /// <summary>Degrees per second the pose blends at. Faster for a strike.</summary>
        public float BlendSpeed = 360f;

        private Transform visual;
        private Vector3 visualRest;
        private readonly Transform[] parts = new Transform[PartNames.Length];
        private readonly Quaternion[] rests = new Quaternion[PartNames.Length];
        private readonly Vector3[] offsets = new Vector3[PartNames.Length];
        private float drop;
        private Behaviour walker;

        private void Awake() => Rebind();

        /// <summary>Finds the parts again after the Visual child was swapped.</summary>
        public void Rebind()
        {
            visual = transform.Find("Visual");
            if (visual != null) visualRest = visual.localPosition;
            for (int i = 0; i < PartNames.Length; i++)
            {
                parts[i] = RaiderWalkAnimator.Part(visual, PartNames[i]);
                rests[i] = parts[i] != null ? parts[i].localRotation : Quaternion.identity;
                offsets[i] = Vector3.zero;
            }
            drop = 0f;
            walker = GetComponent<CreatureWalkAnimator>();
            if (walker == null) walker = GetComponent<RaiderWalkAnimator>();
        }

        public bool HasPart(string name) => RaiderWalkAnimator.Part(visual, name) != null;

        /// <summary>Blends towards a pose. speed is degrees per second (a strike snaps faster than a wind-up).</summary>
        public void Set(PoseKind kind, float speed = 360f)
        {
            Current = kind;
            BlendSpeed = speed;
        }

        private void LateUpdate()
        {
            if (visual == null) return;
            Shape target = ShapeOf(Current);
            float step = BlendSpeed * Time.deltaTime;
            bool walking = walker != null && walker.isActiveAndEnabled;
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] == null) continue;
                offsets[i] = Vector3.MoveTowards(offsets[i], target.euler[i], step);
                Quaternion baseRotation = walking && WalkDriven[i] ? parts[i].localRotation : rests[i];
                parts[i].localRotation = baseRotation * Quaternion.Euler(offsets[i]);
            }
            drop = Mathf.MoveTowards(drop, target.drop, step / 360f * 1.5f);
            visual.localPosition = visualRest - Vector3.up * (drop / Mathf.Max(0.01f, Mathf.Abs(transform.lossyScale.y)));
        }

        private static Shape ShapeOf(PoseKind kind)
        {
            if (shapes == null) Build();
            return shapes.TryGetValue(kind, out Shape s) ? s : shapes[PoseKind.Rest];
        }

        // ---------- The table ----------

        private static void Build()
        {
            shapes = new Dictionary<PoseKind, Shape>();
            float f = Forward, o = Outward;
            // A limb forward is +f about X; a torso or head leaning forward is the opposite rotation (it points up, not down).
            Add(PoseKind.Rest, 0f);
            Add(PoseKind.ArmsUp, 0f, ("Arm_L", X(f * 150f)), ("Arm_R", X(f * 150f)), ("Torso", X(f * 10f)), ("Head", X(f * 10f)));
            Add(PoseKind.Slam, 0.05f, ("Arm_L", X(f * 55f)), ("Arm_R", X(f * 55f)), ("Torso", X(-f * 25f)), ("Head", X(-f * 10f)));
            Add(PoseKind.Crouch, 0.15f, ("Torso", X(-f * 30f)), ("Arm_L", X(f * 35f)), ("Arm_R", X(f * 35f)), ("Head", X(f * 20f)),
                ("Leg_L", X(f * 25f)), ("Leg_R", X(f * -15f)));
            Add(PoseKind.Lunge, 0.1f, ("Torso", X(-f * 35f)), ("Arm_L", X(f * 80f)), ("Arm_R", X(f * 80f)), ("Leg_L", X(f * 30f)), ("Leg_R", X(f * -30f)));
            Add(PoseKind.Inhale, 0f, ("Torso", X(f * 15f)), ("Head", X(f * 30f)), ("Arm_L", Z(-o * 70f)), ("Arm_R", Z(o * 70f)));
            Add(PoseKind.Scream, 0f, ("Torso", X(-f * 15f)), ("Head", X(-f * 10f)), ("Arm_L", Z(-o * 35f) + X(f * 30f)), ("Arm_R", Z(o * 35f) + X(f * 30f)));
            Add(PoseKind.WindLeft, 0f, ("Torso", Y(-40f)), ("Arm_L", X(f * -40f) + Z(-o * 60f)), ("Head", Y(20f)));
            Add(PoseKind.SweepLeft, 0f, ("Torso", Y(35f)), ("Arm_L", X(f * 80f) + Z(-o * 20f)));
            Add(PoseKind.WindRight, 0f, ("Torso", Y(40f)), ("Arm_R", X(f * -40f) + Z(o * 60f)), ("Head", Y(-20f)));
            Add(PoseKind.SweepRight, 0f, ("Torso", Y(-35f)), ("Arm_R", X(f * 80f) + Z(o * 20f)));
            Add(PoseKind.Planted, 0.15f, ("Leg_L", Z(-o * 18f)), ("Leg_R", Z(o * 18f)), ("Torso", Y(45f)), ("Arm_L", Z(-o * 85f)), ("Arm_R", Z(o * 85f)));
            Add(PoseKind.WideSwing, 0.15f, ("Leg_L", Z(-o * 18f)), ("Leg_R", Z(o * 18f)), ("Torso", Y(-50f)), ("Arm_L", Z(-o * 85f)), ("Arm_R", Z(o * 85f)));
            Add(PoseKind.Buried, 0.3f, ("Torso", X(-f * 45f)), ("Arm_L", X(f * 60f)), ("Arm_R", X(f * 60f)), ("Head", X(-f * 15f)));
            Add(PoseKind.Guard, 0f, ("Arm_L", X(f * 75f)), ("Arm_R", X(f * 140f)), ("Torso", Y(-15f)));
            Add(PoseKind.ShieldRest, 0.1f, ("Arm_L", X(f * 5f) + Z(-o * 10f)), ("Arm_R", X(f * 10f)), ("Torso", X(-f * 15f)), ("Head", X(-f * 20f)));
            Add(PoseKind.Stagger, 0f, ("Torso", X(f * 18f)), ("Head", X(f * 25f)), ("Arm_L", Z(-o * 25f)), ("Arm_R", Z(o * 25f)),
                ("Foreleg_L", X(f * -20f)), ("Foreleg_R", X(f * -20f)));
            Add(PoseKind.Low, 0.45f, ("Head", X(-f * 10f)), ("Foreleg_L", Z(-o * 30f)), ("Foreleg_R", Z(o * 30f)), ("Hindleg_L", Z(-o * 30f)), ("Hindleg_R", Z(o * 30f)));
            Add(PoseKind.Paw, 0.08f, ("Head", X(-f * 25f)), ("Foreleg_R", X(f * 45f)), ("Torso", X(-f * 6f)));
            Add(PoseKind.Charge, 0.05f, ("Head", X(-f * 30f)), ("Torso", X(-f * 8f)));
            Add(PoseKind.Gore, 0f, ("Head", X(f * 25f)), ("Foreleg_L", X(f * 20f)), ("Foreleg_R", X(f * 20f)));
        }

        private static Vector3 X(float d) => new Vector3(d, 0f, 0f);
        private static Vector3 Y(float d) => new Vector3(0f, d, 0f);
        private static Vector3 Z(float d) => new Vector3(0f, 0f, d);

        private static void Add(PoseKind kind, float drop, params (string part, Vector3 euler)[] rows)
        {
            var shape = new Shape { euler = new Vector3[PartNames.Length], drop = drop };
            foreach (var row in rows)
            {
                int index = System.Array.IndexOf(PartNames, row.part);
                if (index >= 0) shape.euler[index] = row.euler;
            }
            shapes[kind] = shape;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => shapes = null;
    }
}
