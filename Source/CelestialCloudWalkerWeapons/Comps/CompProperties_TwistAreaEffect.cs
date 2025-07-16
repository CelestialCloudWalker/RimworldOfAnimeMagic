using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using static HarmonyLib.Code;
using Verse.AI;
using AnimeArsenal;

namespace JJK
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
                MoteMaker.ThrowText(parent.pawn.DrawPos, parent.pawn.Map, $"APPROACH!", Color.green);
                TwistPawnsInArea(target.Cell);
            }
        }
        private void TwistPawnsInArea(IntVec3 center)
        {
            Map map = parent.pawn.Map;
            IEnumerable<Pawn> pawnsToLure = GetEnemyPawnsInRange(center, map, Props.radius)
                .Take(Props.maxTargets);

            foreach (Pawn enemyPawn in pawnsToLure)
            {
                float CEScale = AnimeArsenalUtility.CalcAstralPulseScalingFactor(parent.pawn, enemyPawn);
                AreaTwistEffect.TwistTargetLimb(enemyPawn, parent.pawn, Props.baseDamage, CEScale);
            }
        }

        private IEnumerable<Pawn> GetEnemyPawnsInRange(IntVec3 center, Map map, float radius)
        {
            return GenRadial.RadialCellsAround(center, radius, true)
                .SelectMany(c => c.GetThingList(map))
                .OfType<Pawn>()
                .Where(p => p.Faction != null && p.Faction != Faction.OfPlayer);
        }

        public static void TwistTargetLimb(Pawn Target, Pawn Caster, float BaseDamage, float Scale)
        {
            BodyPartRecord targetLimb = AnimeArsenalUtility.GetRandomLimb(Target);

            if (targetLimb != null)
            {
                float damage = BaseDamage * Scale;

                DamageInfo dinfo = new DamageInfo(CelestialDefof.TwistDamage, damage, 1f, -1f, Caster, targetLimb);
                Target.TakeDamage(dinfo);

                MoteMaker.ThrowText(Caster.DrawPos, Caster.Map, "TWIST!", Color.red);
            }
        }
    }
}