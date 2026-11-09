using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SheltreSim.Combat
{
    /// <summary>Pooled shot lines and one muzzle flash light. Purely visual: no colliders, no gameplay state.</summary>
    public sealed class RangedWeaponFx
    {
        private const int MaxLines = 48;

        private sealed class Line
        {
            public LineRenderer renderer;
            public float remaining, duration;
            public Color color;
            public bool held;
        }

        private readonly List<Line> lines = new List<Line>();
        private readonly Transform root;
        private readonly Material material;
        private readonly Light flash;
        private int flashFrames;

        public RangedWeaponFx()
        {
            root = new GameObject("RangedWeaponFx").transform;
            // Sprites/Default is in the project's Always Included Shaders, so it exists in a build. It reads the line's vertex colour.
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("UI/Default");
            material = new Material(shader);
            var lightObject = new GameObject("MuzzleFlash");
            lightObject.transform.SetParent(root, false);
            flash = lightObject.AddComponent<Light>();
            flash.type = LightType.Point;
            flash.range = 7f;
            flash.intensity = 2.2f;
            flash.shadows = LightShadows.None;
            flash.enabled = false;
        }

        public int ActiveLines
        {
            get { int n = 0; foreach (Line line in lines) if (line.remaining > 0f || line.held) n++; return n; }
        }

        public bool FlashVisible => flash != null && flash.enabled;

        private Line Acquire()
        {
            Line best = null;
            foreach (Line line in lines)
            {
                if (line.held) continue;
                if (line.remaining <= 0f) { best = line; break; }
                if (lines.Count >= MaxLines && (best == null || line.remaining < best.remaining)) best = line;
            }
            if (best != null && (best.remaining <= 0f || lines.Count >= MaxLines)) return best;
            var go = new GameObject("ShotLine");
            go.transform.SetParent(root, false);
            var renderer = go.AddComponent<LineRenderer>();
            renderer.sharedMaterial = material;
            renderer.positionCount = 2;
            renderer.useWorldSpace = true;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            var made = new Line { renderer = renderer };
            lines.Add(made);
            return made;
        }

        private Line Show(Vector3 from, Vector3 to, float width, Color color, float duration, bool held)
        {
            Line line = Acquire();
            line.remaining = duration;
            line.duration = duration;
            line.color = color;
            line.held = held;
            line.renderer.enabled = true;
            line.renderer.SetPosition(0, from);
            line.renderer.SetPosition(1, to);
            line.renderer.startWidth = width;
            line.renderer.endWidth = width;
            line.renderer.startColor = color;
            line.renderer.endColor = color;
            return line;
        }

        public void Tracer(Vector3 from, Vector3 to, Color color) => Show(from, to, 0.014f, color, 0.045f, false);

        public void Beam(Vector3 from, Vector3 to, Color color) => Show(from, to, 0.03f, color, 0.09f, false);

        /// <summary>A line that stays until the owner releases it (a plasma bolt). Returned as an opaque handle.</summary>
        public object HoldLine(Vector3 from, Vector3 to, float width, Color color) => Show(from, to, width, color, 1f, true);

        public void MoveHeld(object handle, Vector3 from, Vector3 to)
        {
            var line = (Line)handle;
            line.renderer.SetPosition(0, from);
            line.renderer.SetPosition(1, to);
        }

        public void ReleaseHeld(object handle)
        {
            var line = (Line)handle;
            line.held = false;
            line.remaining = 0f;
            line.renderer.enabled = false;
        }

        public void Flash(Vector3 position, Color color)
        {
            flash.transform.position = position;
            flash.color = color;
            flash.enabled = true;
            flashFrames = 2;
        }

        public void Tick(float deltaTime)
        {
            if (flashFrames > 0 && --flashFrames == 0) flash.enabled = false;
            foreach (Line line in lines)
            {
                if (line.held || line.remaining <= 0f) continue;
                line.remaining -= deltaTime;
                if (line.remaining <= 0f) { line.renderer.enabled = false; continue; }
                Color faded = line.color;
                faded.a *= Mathf.Clamp01(line.remaining / line.duration);
                line.renderer.startColor = faded;
                line.renderer.endColor = faded;
            }
        }

        public void Destroy()
        {
            if (root != null) Object.Destroy(root.gameObject);
            if (material != null) Object.Destroy(material);
        }
    }
}
