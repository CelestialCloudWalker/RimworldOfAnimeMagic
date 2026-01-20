using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;

namespace AnimeArsenal
{
    public class CompProperties_TwistAreaEffect : CompProperties_AbilityEffect
    {
        public float baseDamage;
        public int maxTargets = 10;
        public float radius = 10f;

        public CompProperties_TwistAreaEffect()
        {
            compClass = typeof(AreaTwistEffect);
        }
    }

    public class AreaTwistEffect : CompAbilityEffect
    {
        public new CompProperties_TwistAreaEffect Props => (CompProperties_TwistAreaEffect)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            if (parent?.pawn == null || parent.pawn.Map == null)
            {
                Log.Warning("[AnimeArsenal] TwistAreaEffect: Invalid parent or pawn");
                return;
            }

            if (!target.IsValid)
            {
                Log.Warning("[AnimeArsenal] TwistAreaEffect: Invalid target");
                return;
            }

            IntVec3 targetPos;
            if (target.HasThing && target.Thing is Pawn)
            {
                targetPos = target.Thing.Position;
            }
            else if (target.Cell.IsValid)
            {
                targetPos = target.Cell;
            }
            else
            {
                Log.Warning("[AnimeArsenal] TwistAreaEffect: Could not determine target position");
                return;
            }

            DoTwistAttack(targetPos);
        }

        private void DoTwistAttack(IntVec3 targetPos)
        {
            if (parent?.pawn == null)
                return;

            Map map = parent.pawn.Map;
            if (map == null)
                return;

            var enemies = FindTargetsInRadius(targetPos, map);

            if (enemies.Count == 0)
            {
                Messages.Message(
                    "No valid targets in range.",
                    MessageTypeDefOf.RejectInput
                );
                return;
            }

            int numToHit = Mathf.Min(enemies.Count, Props.maxTargets);

            for (int i = 0; i < numToHit; i++)
            {
                Pawn target = enemies[i];

                if (target?.Dead == false && !target.Destroyed)
                {
                    float scaleFactor = 1f;

                    float finalDamage = Props.baseDamage * scaleFactor;

                    TwistLimb(target, parent.pawn, finalDamage);
                }
            }
        }

        private List<Pawn> FindTargetsInRadius(IntVec3 center, Map map)
        {
            List<Pawn> targets = new List<Pawn>();

            foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, Props.radius, true))
            {
                if (!cell.InBounds(map))
                    continue;

                List<Thing> things = cell.GetThingList(map);
                if (things == null)
                    continue;

                foreach (Thing thing in things)
                {
                    if (thing is Pawn p && CanTarget(p))
                    {
                        targets.Add(p);
                    }
                }
            }

            return targets.OrderBy(p => p.Position.DistanceTo(center)).ToList();
        }

        private bool CanTarget(Pawn pawn)
        {
            if (pawn?.Dead != false || pawn.Destroyed)
                return false;

            if (pawn == parent?.pawn)
                return false;

            if (pawn.Faction != null && pawn.Faction == parent.pawn?.Faction)
                return false;

            return true;
        }

        private void TwistLimb(Pawn target, Pawn caster, float totalDamage)
        {
            if (target == null || target.Dead || target.Destroyed)
                return;

            if (caster == null)
                return;

            BodyPartRecord limb = AnimeArsenalUtility.GetRandomLimb(target);

            if (limb != null)
            {
                DamageInfo dmg = new DamageInfo(
                    CelestialDefof.TwistDamage,
                    totalDamage,
                    1f,    
                    -1f,   
                    caster,
                    limb
                );

                target.TakeDamage(dmg);

                FleckMaker.ThrowMicroSparks(target.DrawPos, target.Map);
            }
            else
            {
                DamageInfo dmg = new DamageInfo(
                    CelestialDefof.TwistDamage,
                    totalDamage,
                    1f,
                    -1f,
                    caster
                );

                target.TakeDamage(dmg);

                FleckMaker.ThrowMicroSparks(target.DrawPos, target.Map);
            }
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (target.Cell.IsValid)
                return true;

            if (target.HasThing && target.Thing is Pawn)
                return true;

            if (throwMessages)
            {
                Messages.Message("Must target a location or pawn", MessageTypeDefOf.RejectInput);
            }

            return false;
        }
    }
}