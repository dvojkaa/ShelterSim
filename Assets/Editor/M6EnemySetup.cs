using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SheltreSim.Enemies;

namespace SheltreSim.EditorTools
{
    /// <summary>
    /// Spawns M6 enemies into the existing outdoor area (same single scene as the rest
    /// of the project — no separate Unity scene). Run after
    /// "Tools > SheltreSim > Build M1 Bunker Scene" and
    /// "Tools > SheltreSim > Setup M4 Outdoor Area" have already produced a scene with
    /// a "Player" GameObject and an "OutdoorArea" parent.
    /// </summary>
    public static class M6EnemySetup
    {
        [MenuItem("Tools/SheltreSim/Setup M6 Enemies")]
        public static void SetupEnemies()
        {
            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogError("No 'Player' GameObject found — run Tools > SheltreSim > Build M1 Bunker Scene first.");
                return;
            }

            GameObject outdoorArea = GameObject.Find("OutdoorArea");
            if (outdoorArea == null)
            {
                Debug.LogError("No 'OutdoorArea' found — run Tools > SheltreSim > Setup M4 Outdoor Area first.");
                return;
            }

            // Repositioned for M4's enlarged 40x36m outdoor area (was 24x20m) — 2 more raiders
            // added since the bigger map had too much empty space for just 2.
            SpawnEnemy("Raider_1", new Vector3(0f, 1f, -11f), outdoorArea.transform);
            SpawnEnemy("Raider_2", new Vector3(15f, 1f, -22f), outdoorArea.transform);
            SpawnEnemy("Raider_3", new Vector3(-15f, 1f, -22f), outdoorArea.transform);
            SpawnEnemy("Raider_4", new Vector3(0f, 1f, -34f), outdoorArea.transform);

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

            Debug.Log("M6 enemies ready. 4 raiders patrol the outdoor area — they'll detect, chase, and attack on sight. Fight back with left-click (M8 melee) or avoid them.");
        }

        private static readonly Color BodyColor = new Color(0.35f, 0.12f, 0.1f);
        private static readonly Color HeadColor = new Color(0.55f, 0.42f, 0.35f);
        private static readonly Color WeaponColor = new Color(0.2f, 0.19f, 0.18f);

        /// <summary>internal, not private: reused by M12BiomeBlendSetup to spawn extra raiders in the expanded wasteland with the exact same CharacterController conversion.</summary>
        internal static void SpawnEnemy(string name, Vector3 position, Transform parent)
        {
            if (GameObject.Find(name) != null)
            {
                return;
            }

            GameObject enemy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            enemy.name = name;
            enemy.transform.SetParent(parent, false);
            enemy.transform.position = position;
            EditorMaterialUtility.Apply(enemy, BodyColor);

            // EnemyAI now drives movement through a CharacterController (so raiders actually
            // collide with walls/buildings/cars instead of walking through them) — remove the
            // CapsuleCollider CreatePrimitive gave it for free, since CharacterController is
            // itself a Collider subclass and covers both physics collision and MeleeAttacker's
            // SphereCastAll hit detection. Same default shape (height 2, radius 0.5, centered
            // on the object) as the CapsuleCollider it replaces, so nothing in the hit-detection
            // or save paths needs to change — but note it is 1.0m ACROSS (and ~1.16m including
            // skinWidth, which is what a doorway has to clear), against a player that is only
            // 0.7m. Both the bunker's doorway and the ruined buildings' were sized for the
            // player alone and had to be widened before a raider could actually follow you
            // through one: see M1SceneBuilder.DoorGap / M10MapExpansionSetup.BuildingDoorGap.
            // If this radius ever changes, re-measure those two gaps.
            UnityEngine.Object.DestroyImmediate(enemy.GetComponent<CapsuleCollider>());
            enemy.AddComponent<CharacterController>();
            enemy.AddComponent<EnemyAI>();

            AddVisualDetail(enemy.transform);
        }

        /// <summary>
        /// Purely cosmetic child primitives — a head and a crude held weapon — so a raider
        /// reads as a humanoid figure rather than a plain capsule. Every collider
        /// `CreatePrimitive` gives these is explicitly destroyed: MeleeAttacker's SphereCastAll
        /// distance-0-vs-swept logic (rewritten twice already, see DESIGN_NOTES.md) was tuned
        /// and verified against exactly one collider per enemy — the CharacterController added
        /// above — and an extra collider poking further out than it in some direction would be
        /// new, unverified surface for that same bug category to reappear in. These primitives
        /// exist to be looked at, never to be hit-tested.
        /// </summary>
        private static void AddVisualDetail(Transform enemy)
        {
            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(enemy, false);
            head.transform.localPosition = new Vector3(0f, 0.82f, 0f);
            head.transform.localScale = new Vector3(0.42f, 0.42f, 0.42f);
            UnityEngine.Object.DestroyImmediate(head.GetComponent<Collider>());
            EditorMaterialUtility.Apply(head, HeadColor);

            GameObject weapon = GameObject.CreatePrimitive(PrimitiveType.Cube);
            weapon.name = "WeaponProp";
            weapon.transform.SetParent(enemy, false);
            weapon.transform.localPosition = new Vector3(0.32f, 0.05f, 0.25f);
            weapon.transform.localRotation = Quaternion.Euler(35f, 0f, 15f);
            weapon.transform.localScale = new Vector3(0.08f, 0.08f, 0.65f);
            UnityEngine.Object.DestroyImmediate(weapon.GetComponent<Collider>());
            EditorMaterialUtility.Apply(weapon, WeaponColor);
        }
    }
}
