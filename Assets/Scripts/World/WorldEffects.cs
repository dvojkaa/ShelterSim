using UnityEngine;

namespace SheltreSim.World
{
    /// <summary>Burning-town smoke and fire. Scenery only; destroyed with its chunk.</summary>
    public static class WorldEffects
    {
        private static Material smoke;
        private static Material fire;

        private static Material Get(ref Material slot, Color color, bool additive)
        {
            if (slot != null) return slot;
            // In a Resources folder, so it ships in builds without being referenced from a scene.
            Shader shader = Resources.Load<Shader>(additive ? "Shaders/SS_FireParticle" : "Shaders/SS_SmokeParticle");
            if (shader == null) return null;
            slot = new Material(shader) { color = color };
            return slot;
        }

        public static void SmokePlume(Transform parent, Vector3 position)
        {
            Material smokeMaterial = Get(ref smoke, new Color(0.14f, 0.13f, 0.12f, 0.75f), false);
            Material fireMaterial = Get(ref fire, new Color(1f, 0.55f, 0.15f, 1f), true);
            if (smokeMaterial == null) return;
            var root = new GameObject("SmokePlume");
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(position, Quaternion.identity);
            Emit(root.transform, smokeMaterial, 9f, 14f, 4f, 16f, 4f, 3.5f, 0.6f);
            if (fireMaterial != null) Emit(root.transform, fireMaterial, 12f, 1.1f, 1.2f, 0.3f, 2.2f, 1.6f, 0.0f);
        }

        private static void Emit(Transform parent, Material material, float rate, float life, float startSize,
            float endSize, float speed, float radius, float drift)
        {
            var go = new GameObject("Particles");
            go.transform.SetParent(parent, false);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.loop = true;
            main.duration = 5f;
            main.startLifetime = life;
            main.startSpeed = speed;
            main.startSize = startSize;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = Mathf.CeilToInt(rate * life) + 4;
            main.playOnAwake = true;
            main.prewarm = true;
            var emission = system.emission;
            emission.rateOverTime = rate;
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 9f;
            shape.radius = radius;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, endSize / Mathf.Max(startSize, 0.01f)));
            var color = system.colorOverLifetime;
            color.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
            color.color = fade;
            var velocity = system.velocityOverLifetime;
            velocity.enabled = true;
            velocity.x = new ParticleSystem.MinMaxCurve(drift);
            velocity.y = new ParticleSystem.MinMaxCurve(0f);
            velocity.z = new ParticleSystem.MinMaxCurve(drift * 0.4f);
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            system.Play();
        }
    }
}
