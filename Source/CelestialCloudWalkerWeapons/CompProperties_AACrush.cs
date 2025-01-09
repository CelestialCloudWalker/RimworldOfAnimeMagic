using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace AnimeArsenal
{
    public class CompProperties_Crush : CompProperties_AbilityEffect
    {
        public float radius = 10f;
        public int maxTargets = 10;
        public float baseDamage = 10f;

        public CompProperties_Crush()
        {
            compClass = typeof(CompAbilityEffect_Crush);
        }
    }

    public class CompAbilityEffect_Crush : CompAbilityEffect
    {
        public new CompProperties_Crush Props => (CompProperties_Crush)props;
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            if (target.Cell != null)
            {
                IEnumerable<Pawn> targets = GetEnemyPawnsInRange(target.Cell, parent.pawn.Map, Props.radius);

                foreach (var item in targets)
                {
                    DamageInfo dinfo = new DamageInfo(CelestialDefof.CrushDamage, Props.baseDamage, 1f, -1f, parent.pawn, AnimeArsenalUtility.GetRandomLimb(item));
                    item.TakeDamage(dinfo);
                }
            }
        }

        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            base.DrawEffectPreview(target);
            if (target.IsValid)
            {
                GenDraw.DrawRadiusRing(target.Cell, Props.radius);
            }
        }

        private IEnumerable<Pawn> GetEnemyPawnsInRange(IntVec3 center, Map map, float radius)
        {
            return GenRadial.RadialCellsAround(center, radius, true)
                .SelectMany(c => c.GetThingList(map))
                .OfType<Pawn>()
                .Where(p => p.Faction != null && p.Faction != Faction.OfPlayer).ToList();
        }
    }
}