using UnityEngine;

namespace SheltreSim.Power
{
    public enum LightFixture { Ceiling = 0, Floodlight = 1 }

    /// <summary>
    /// A grid-powered light: 0.1 kW at life-support priority. Lit while the grid serves it, dark when the generator runs dry
    /// or the network overheats, fading over 0.3 s either way. The Light is made the first time the piece is powered, so a
    /// build ghost (enabled once, then disabled) never makes one. Ceiling is a warm point light, Floodlight a wide spot
    /// light aimed forward and down. No saved state: a loaded light is lit again by the next grid tick.
    /// </summary>
    public sealed class LightPiece : PowerConsumerDevice
    {
        public LightFixture fixture = LightFixture.Ceiling;
        public float powerKw = 0.1f;
        public float fadeSeconds = 0.3f;
        public Color ceilingColor = new Color(1f, 0.87f, 0.66f);
        public Color floodColor = new Color(0.92f, 0.95f, 1f);

        private Light lamp;
        private float level, fullIntensity;

        public override string PowerName => fixture == LightFixture.Floodlight ? "Floodlight" : "Ceiling Light";
        public override int PowerPriority => 1;
        public override float PowerDemand => Mathf.Max(0f, powerKw);
        public override string PowerStatus => Granted ? "lit" : NoPowerReason();

        /// <summary>0 dark .. 1 fully lit. For probes and status displays.</summary>
        public float LightLevel => level;

        public override void Advance(float seconds)
        {
            float target = Granted ? 1f : 0f;
            if (lamp == null)
            {
                if (target <= 0f) return;
                CreateLight();
            }
            level = Mathf.MoveTowards(level, target, seconds / Mathf.Max(0.01f, fadeSeconds));
            lamp.intensity = fullIntensity * level;
            lamp.enabled = level > 0.001f;
        }

        protected override void LeaveGrid()
        {
            base.LeaveGrid();
            // Advance doesn't run while disabled, so go dark here rather than stay lit with nothing feeding it.
            level = 0f;
            if (lamp != null) lamp.enabled = false;
        }

        private void CreateLight()
        {
            Bounds bounds = PieceBounds();
            var go = new GameObject(fixture == LightFixture.Floodlight ? "FloodLight" : "CeilingLight");
            go.transform.SetParent(transform, false);
            lamp = go.AddComponent<Light>();
            lamp.shadows = LightShadows.None;
            lamp.renderMode = LightRenderMode.Auto;
            if (fixture == LightFixture.Floodlight)
            {
                // From the top front of the lamp head, forward and 25 degrees down.
                go.transform.position = new Vector3(bounds.center.x, bounds.max.y - 0.1f, bounds.center.z) + transform.forward * Mathf.Min(0.3f, bounds.extents.z);
                go.transform.rotation = Quaternion.LookRotation(transform.forward, Vector3.up) * Quaternion.Euler(25f, 0f, 0f);
                lamp.type = LightType.Spot;
                lamp.spotAngle = 80f;
                lamp.range = 28f;
                lamp.color = floodColor;
                fullIntensity = 2.4f;
            }
            else
            {
                go.transform.position = bounds.center - Vector3.up * Mathf.Min(0.1f, bounds.extents.y);
                lamp.type = LightType.Point;
                lamp.range = 9f;
                lamp.color = ceilingColor;
                fullIntensity = 1.5f;
            }
            lamp.intensity = 0f;
            lamp.enabled = false;
        }

        private Bounds PieceBounds()
        {
            bool any = false;
            Bounds bounds = new Bounds(transform.position + Vector3.up * 0.5f, Vector3.one * 0.2f);
            foreach (Renderer r in GetComponentsInChildren<Renderer>())
            {
                if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
                if (!any) { bounds = r.bounds; any = true; }
                else bounds.Encapsulate(r.bounds);
            }
            return bounds;
        }
    }
}
