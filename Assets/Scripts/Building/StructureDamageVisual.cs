using UnityEngine;

namespace SheltreSim.Building
{
    /// <summary>
    /// Shows GPT's worn and broken versions of a piece as its StructureHealth falls: below 65% the "_damaged" model,
    /// below 30% the "_critical" one, and back again on repair. Looks only: the intact "Visual" child just has its
    /// renderers switched off, and the stage models carry no colliders, so the piece's collision never changes.
    /// Stage models are prefabs at Resources/DamageStages/&lt;modelId&gt;_damaged and _critical (M22SystemsSetup), built
    /// the first time they're needed, so a full-health piece and a build ghost (enabled once, then disabled) make nothing.
    /// A missing stage model falls back to the next milder stage.
    /// </summary>
    public sealed class StructureDamageVisual : MonoBehaviour
    {
        public const float DamagedBelow = 0.65f;
        public const float CriticalBelow = 0.3f;
        public const string ResourceFolder = "DamageStages/";
        private static readonly string[] Suffixes = { "", "_damaged", "_critical" };

        /// <summary>GPT's id of the intact model, e.g. piece_wall or piece_door_steel.</summary>
        public string modelId;

        private StructureHealth health;
        private Transform intact;
        private Renderer[] intactRenderers;
        private readonly GameObject[] stages = new GameObject[3];
        private readonly bool[] missing = new bool[3];
        private int stage = -1;

        /// <summary>0 intact, 1 damaged, 2 critical: what's showing now.</summary>
        public int Stage => Mathf.Max(0, stage);

        public static int StageFor(float fraction) => fraction < CriticalBelow ? 2 : fraction < DamagedBelow ? 1 : 0;

        private void OnEnable()
        {
            health = GetComponent<StructureHealth>();
            if (health == null) return;
            health.OnChanged += Apply;
            Apply();
        }

        private void OnDisable()
        {
            if (health != null) health.OnChanged -= Apply;
        }

        /// <summary>Public so a probe can force a refresh after setting health directly.</summary>
        public void Apply()
        {
            if (health == null || string.IsNullOrEmpty(modelId)) return;
            int wanted = StageFor(health.Fraction);
            while (wanted > 0 && StageObject(wanted) == null) wanted--;
            if (wanted == stage) return;
            // Nothing to undo on the first look at a healthy piece: leave the prefab exactly as it was built.
            if (stage < 0 && wanted == 0) { stage = 0; return; }
            if (!FindIntact()) return;
            stage = wanted;
            foreach (Renderer r in intactRenderers) if (r != null) r.enabled = stage == 0;
            for (int i = 1; i < stages.Length; i++)
                if (stages[i] != null && stages[i].activeSelf != (i == stage)) stages[i].SetActive(i == stage);
        }

        private bool FindIntact()
        {
            if (intact != null) return true;
            intact = FindChild(transform, "Visual");
            if (intact == null) return false;
            intactRenderers = intact.GetComponentsInChildren<Renderer>(true);
            return true;
        }

        private GameObject StageObject(int index)
        {
            if (stages[index] != null) return stages[index];
            if (missing[index] || !FindIntact()) return null;
            GameObject prefab = Resources.Load<GameObject>(ResourceFolder + modelId + Suffixes[index]);
            if (prefab == null) { missing[index] = true; return null; }
            GameObject instance = Instantiate(prefab, intact.parent, false);
            instance.name = "DamageStage" + Suffixes[index];
            instance.SetActive(false);
            instance.transform.localPosition = intact.localPosition;
            instance.transform.localRotation = intact.localRotation;
            instance.transform.localScale = intact.localScale;
            foreach (Collider c in instance.GetComponentsInChildren<Collider>(true)) Destroy(c);
            foreach (Transform t in instance.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = intact.gameObject.layer;
            stages[index] = instance;
            return instance;
        }

        private static Transform FindChild(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name) return child;
                Transform found = FindChild(child, name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
