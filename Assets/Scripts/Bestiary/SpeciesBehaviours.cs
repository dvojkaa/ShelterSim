using SheltreSim.Enemies;
using UnityEngine;

namespace SheltreSim.Bestiary
{
    /// <summary>
    /// Gives a species actor its brain, walk animator and telegraph poses, and the two group verbs the world uses:
    /// Alert (a scream, or a camp member attacked) and Wake (a boss's lair entered or its marked trigger opened).
    /// </summary>
    public static class SpeciesBehaviours
    {
        /// <summary>Called by Bestiary.Spawn on the active actor, after its model is in place.</summary>
        public static IEnemyBrain Create(SpeciesActor actor)
        {
            if (actor == null) return null;
            GameObject go = actor.gameObject;
            SpeciesSpec spec = actor.Spec;

            // The right walk cycle for the body, bound to the model that is there now.
            RaiderWalkAnimator walker = go.GetComponent<RaiderWalkAnimator>();
            if (spec.Body == BodyKind.Quadruped)
            {
                if (walker != null) walker.enabled = false;
                CreatureWalkAnimator creature = go.GetComponent<CreatureWalkAnimator>();
                if (creature == null) creature = go.AddComponent<CreatureWalkAnimator>();
                else creature.Rebind();
            }
            else
            {
                if (walker == null) walker = go.AddComponent<RaiderWalkAnimator>();
                else walker.Rebind();
            }
            EnemyPose pose = go.GetComponent<EnemyPose>();
            if (pose == null) go.AddComponent<EnemyPose>();
            else pose.Rebind();

            SpeciesBrain brain = go.GetComponent<SpeciesBrain>();
            if (brain != null) return brain;
            switch (spec.Id)
            {
                case SpeciesId.Runner: return go.AddComponent<RunnerBrain>();
                case SpeciesId.Screamer: return go.AddComponent<ScreamerBrain>();
                case SpeciesId.Breacher: return go.AddComponent<BreacherBrain>();
                case SpeciesId.Mirelurker: return go.AddComponent<MirelurkerBrain>();
                case SpeciesId.IronBoar: return go.AddComponent<IronBoarBrain>();
                case SpeciesId.Gatekeeper: return go.AddComponent<GatekeeperBrain>();
                case SpeciesId.RootColossus: return go.AddComponent<RootColossusBrain>();
                case SpeciesId.ShiftCommander: return go.AddComponent<ShiftCommanderBrain>();
                default: return go.AddComponent<MeleeBrain>(); // Wanderer, Scavenger, Plated, Rootwalker
            }
        }

        /// <summary>Wakes every living species actor within radius: it engages the player. Spawns nothing.</summary>
        public static void Alert(Vector3 position, float radius)
        {
            float squared = radius * radius;
            var brains = new System.Collections.Generic.List<SpeciesBrain>(SpeciesBrain.All);
            foreach (SpeciesBrain brain in brains)
            {
                if (brain == null || brain.Enemy == null || brain.Enemy.IsDead) continue;
                if ((brain.transform.position - position).sqrMagnitude <= squared) brain.Alert(position);
            }
        }

        /// <summary>Wakes a dormant boss (or alerts an ordinary actor). Safe to call repeatedly: the boss announces itself once.</summary>
        public static void Wake(SpeciesActor actor)
        {
            if (actor == null) return;
            SpeciesBrain brain = actor.GetComponent<SpeciesBrain>();
            if (brain == null) return;
            if (brain.IsDormant) brain.Wake();
            else brain.Alert(actor.transform.position);
        }
    }
}
