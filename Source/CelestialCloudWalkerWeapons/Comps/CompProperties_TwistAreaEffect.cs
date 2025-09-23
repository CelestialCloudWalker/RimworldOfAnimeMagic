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
            if (target.IsValid && target.Cell.IsValid)
            {
                DoTwistAttack(target.Cell);
            }
        }

        private void DoTwistAttack(IntVec3 targetPos)
        {
            Map map = parent.pawn.Map;
            var enemies = FindTargetsInRadius(targetPos, map);

            int numToHit = Mathf.Min(enemies.Count, Props.maxTargets);

            for (int i = 0; i < numToHit; i++)
            {
                Pawn target = enemies[i];
                if (target?.Dead == false && !target.Destroyed)
                {
                    float scaleFactor = 1f;
                    try
                    {
                        scaleFactor = AnimeArsenalUtility.CalcAstralPulseScalingFactor(parent.pawn, target);
                    }
                    catch
                    {
                    }

                    TwistLimb(target, parent.pawn, Props.baseDamage * scaleFactor);
                }
            }
        }

        private List<Pawn> FindTargetsInRadius(IntVec3 center, Map map)
        {
            List<Pawn> targets = new List<Pawn>();

            foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, Props.radius, true))
            {
                if (!cell.InBounds(map)) continue;

                foreach (Thing thing in cell.GetThingList(map))
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

            if (pawn == parent.pawn)
                return false;

            if (pawn.Faction == parent.pawn?.Faction)
                return false;

            return true;
        }

        public static void TwistLimb(Pawn target, Pawn caster, float totalDamage)
        {
            BodyPartRecord limb = AnimeArsenalUtility.GetRandomLimb(target);
            if (limb != null)
            {
                DamageInfo dmg = new DamageInfo(CelestialDefof.TwistDamage, totalDamage, 1f, -1f, caster, limb);
                target.TakeDamage(dmg);
            }
        }
    }
}