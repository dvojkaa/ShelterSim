using System;
using System.Collections.Generic;
using SheltreSim.Audio;
using SheltreSim.Enemies;
using SheltreSim.UI;
using SheltreSim.World;
using UnityEngine;

namespace SheltreSim.Combat
{
    /// <summary>
    /// What a shot does to the world: hit finding, damage and plasma bolts. Only raiders and animals can be hurt.
    /// The player and build pieces have no damage path here at all, including plasma splash.
    /// </summary>
    public sealed class RangedCombat
    {
        public const float BoltSpeed = 45f;
        public const float BoltLifeSeconds = 3f;
        private const float BurstLift = 0.15f;

        public enum HitKind { World, Target, Kill }

        private sealed class Bolt
        {
            public Vector3 position, direction, visualOffset;
            public float life, rangeLeft, damage, radius, travelled;
            public Color color;
            public object line;
        }

        private readonly List<Bolt> bolts = new List<Bolt>();
        private readonly Transform playerRoot;
        private readonly RangedWeaponFx fx;
        private readonly Action<bool> onHit;

        public int ActiveBolts => bolts.Count;

        public RangedCombat(Transform playerRoot, RangedWeaponFx fx, Action<bool> onHit)
        {
            this.playerRoot = playerRoot;
            this.fx = fx;
            this.onHit = onHit;
        }

        private bool BelongsToPlayer(Collider candidate) =>
            playerRoot != null && (candidate.transform == playerRoot || candidate.transform.IsChildOf(playerRoot));

        /// <summary>Nearest solid thing along the ray. Skips the player, triggers and bodies that are already dead.</summary>
        public bool FindHit(Vector3 origin, Vector3 direction, float distance, out RaycastHit best)
        {
            best = default;
            bool found = false;
            foreach (RaycastHit hit in Physics.RaycastAll(origin, direction, distance, ~0, QueryTriggerInteraction.Ignore))
            {
                if ((found && hit.distance >= best.distance) || BelongsToPlayer(hit.collider)) continue;
                EnemyAI enemy = hit.collider.GetComponentInParent<EnemyAI>();
                if (enemy != null && enemy.IsDead) continue;
                WorldWildlife animal = enemy == null ? hit.collider.GetComponentInParent<WorldWildlife>() : null;
                if (animal != null && animal.IsDead) continue;
                best = hit;
                found = true;
            }
            return found;
        }

        /// <summary>Hurts what the collider belongs to, if it can be hurt. One line only for a kill: the hit marker covers the rest.</summary>
        public HitKind ApplyDamage(Collider target, float damage, Vector3 from)
        {
            EnemyAI enemy = target.GetComponentInParent<EnemyAI>();
            if (enemy != null)
            {
                if (enemy.IsDead) return HitKind.World;
                enemy.TakeDamage(damage);
                if (!enemy.IsDead) return HitKind.Target;
                GameMessageBus.Post("Raider defeated.");
                return HitKind.Kill;
            }
            WorldWildlife animal = target.GetComponentInParent<WorldWildlife>();
            if (animal == null || animal.IsDead || !animal.IsHuntable) return HitKind.World;
            string message = animal.TakeHit(damage, from);
            if (!animal.IsDead) return HitKind.Target;
            GameMessageBus.Post(message);
            return HitKind.Kill;
        }

        // ---------- Plasma ----------

        public void LaunchBolt(Vector3 origin, Vector3 direction, Vector3 muzzle, float damage, float radius, float range, Color color)
        {
            bolts.Add(new Bolt
            {
                position = origin,
                direction = direction,
                visualOffset = muzzle - origin,
                life = BoltLifeSeconds,
                rangeLeft = range,
                damage = damage,
                radius = radius,
                color = color,
                line = fx.HoldLine(muzzle, muzzle + direction * 0.1f, 0.1f, color),
            });
        }

        public void TickBolts(float deltaTime)
        {
            if (deltaTime <= 0f) return;
            for (int i = bolts.Count - 1; i >= 0; i--)
            {
                Bolt bolt = bolts[i];
                float step = Mathf.Min(BoltSpeed * deltaTime, bolt.rangeLeft);
                bool burst = FindHit(bolt.position, bolt.direction, step, out RaycastHit hit);
                if (burst) Burst(hit, bolt);
                bolt.position += bolt.direction * step;
                bolt.travelled += step;
                bolt.life -= deltaTime;
                bolt.rangeLeft -= step;
                if (burst || bolt.life <= 0f || bolt.rangeLeft <= 0f)
                {
                    fx.ReleaseHeld(bolt.line);
                    bolts.RemoveAt(i);
                    continue;
                }
                // Flies from the camera so it lands where aimed; the drawing converges from the muzzle.
                Vector3 head = bolt.position + bolt.visualOffset * Mathf.Clamp01(1f - bolt.travelled / 8f);
                fx.MoveHeld(bolt.line, head - bolt.direction * Mathf.Min(0.9f, bolt.travelled), head);
            }
        }

        public void ClearBolts()
        {
            foreach (Bolt bolt in bolts) fx.ReleaseHeld(bolt.line);
            bolts.Clear();
        }

        private void Burst(RaycastHit hit, Bolt bolt)
        {
            Vector3 centre = hit.point + hit.normal * BurstLift;
            GameAudio.PlayAt(Sfx.PlasmaBurst, centre, 1f, 45f);
            fx.Flash(centre, bolt.color);
            for (int i = 0; i < 8; i++) fx.Beam(centre, centre + UnityEngine.Random.onUnitSphere * bolt.radius * 0.6f, bolt.color);

            var done = new HashSet<Component>();
            bool any = false, kill = false;
            Component direct = TargetOf(hit.collider);
            if (direct != null)
            {
                done.Add(direct);
                HitKind result = ApplyDamage(hit.collider, bolt.damage, centre);
                any |= result != HitKind.World;
                kill |= result == HitKind.Kill;
            }
            foreach (Collider nearby in Physics.OverlapSphere(centre, bolt.radius, ~0, QueryTriggerInteraction.Ignore))
            {
                if (BelongsToPlayer(nearby)) continue;
                Component target = TargetOf(nearby);
                if (target == null || !done.Add(target)) continue;
                Bounds bounds = nearby.bounds;
                if (!HasClearPath(centre, bounds.center)) continue;
                float falloff = Mathf.Lerp(1f, 0.5f, Mathf.Clamp01(Vector3.Distance(centre, bounds.ClosestPoint(centre)) / bolt.radius));
                HitKind result = ApplyDamage(nearby, bolt.damage * falloff, centre);
                any |= result != HitKind.World;
                kill |= result == HitKind.Kill;
            }
            if (any) onHit?.Invoke(kill);
        }

        private static Component TargetOf(Collider collider)
        {
            EnemyAI enemy = collider.GetComponentInParent<EnemyAI>();
            if (enemy != null) return enemy;
            return collider.GetComponentInParent<WorldWildlife>();
        }

        /// <summary>Line of sight for a burst. Other bodies don't shield each other, only solid scenery does.</summary>
        private bool HasClearPath(Vector3 from, Vector3 to)
        {
            Vector3 offset = to - from;
            float distance = offset.magnitude;
            if (distance < 0.001f) return true;
            foreach (RaycastHit hit in Physics.RaycastAll(from, offset / distance, distance, ~0, QueryTriggerInteraction.Ignore))
                if (!BelongsToPlayer(hit.collider) && TargetOf(hit.collider) == null) return false;
            return true;
        }
    }
}
