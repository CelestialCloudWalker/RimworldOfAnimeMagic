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
                TwistPawnsInArea(target.Cell);
            }
        }

        private void TwistPawnsInArea(IntVec3 center)
        {
            Map map = parent.pawn.Map;
            List<Pawn> pawnsToTwist = GetEnemyPawnsInRange(center, map, Props.radius);

            int targetCount = Mathf.Min(pawnsToTwist.Count, Props.maxTargets);

            for (int i = 0; i < targetCount; i++)
            {
                Pawn enemyPawn = pawnsToTwist[i];
                if (enemyPawn != null && !enemyPawn.Dead && !enemyPawn.Destroyed)
                {
                    float CEScale = 1f; 
                    try
                    {
                        CEScale = AnimeArsenalUtility.CalcAstralPulseScalingFactor(parent.pawn, enemyPawn);
                    }
                    catch
                    {
                        CEScale = 1f;
                    }

                    TwistTargetLimb(enemyPawn, parent.pawn, Props.baseDamage, CEScale);
                }
            }
        }

        private List<Pawn> GetEnemyPawnsInRange(IntVec3 center, Map map, float radius)
        {
            List<Pawn> validTargets = new List<Pawn>();

            var cellsInRadius = GenRadial.RadialCellsAround(center, radius, true).ToList();

            foreach (IntVec3 cell in cellsInRadius)
            {
                if (!cell.InBounds(map)) continue;

                List<Thing> thingsInCell = new List<Thing>(cell.GetThingList(map));

                foreach (Thing thing in thingsInCell)
                {
                    if (thing is Pawn pawn && IsValidTarget(pawn))
                    {
                        validTargets.Add(pawn);
                    }
                }
            }

            
            return validTargets.OrderBy(p => p.Position.DistanceTo(center)).ToList();
        }

        private bool IsValidTarget(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || pawn.Destroyed) return false;
            if (pawn == parent.pawn) return false; 

            
            if (pawn.Faction != null && pawn.Faction == parent.pawn?.Faction) return false;

            return true;
        }

        public static void TwistTargetLimb(Pawn Target, Pawn Caster, float BaseDamage, float Scale)
        {
            BodyPartRecord targetLimb = AnimeArsenalUtility.GetRandomLimb(Target);
            if (targetLimb != null)
            {
                float damage = BaseDamage * Scale;
                DamageInfo dinfo = new DamageInfo(CelestialDefof.TwistDamage, damage, 1f, -1f, Caster, targetLimb);
                Target.TakeDamage(dinfo);
            }
        }
    }
}