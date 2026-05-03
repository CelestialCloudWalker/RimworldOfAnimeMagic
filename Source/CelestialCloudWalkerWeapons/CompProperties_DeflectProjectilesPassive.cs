using RimWorld;
using Verse;

namespace AnimeArsenal
{
    public class CompProperties_DeflectProjectilesPassive : HediffCompProperties
    {
        public float radius = 5f;
        public int maxShotsBeforeReset = 999;

        public CompProperties_DeflectProjectilesPassive()
        {
            compClass = typeof(HediffComp_DeflectProjectilesPassive);
        }
    }

    public class HediffComp_DeflectProjectilesPassive : HediffComp
    {
        public CompProperties_DeflectProjectilesPassive Props =>
            (CompProperties_DeflectProjectilesPassive)props;

        private int shotCount = 0;

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            if (Pawn == null || Pawn.Map == null || !Pawn.Spawned) return;

            shotCount = 0;

            foreach (Thing thing in GenRadial.RadialDistinctThingsAround(
                Pawn.Position, Pawn.Map, Props.radius, true))
            {
                if (shotCount >= Props.maxShotsBeforeReset) break;

                if (thing is Projectile proj &&
                    proj.Launcher != null &&
                    proj.Launcher.Faction != null &&
                    proj.Launcher.Faction != Pawn.Faction)
                {
                    ProjectileUtility.ReflectProjectile(proj, Pawn);
                    shotCount++;
                }
            }
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref shotCount, "shotCount", 0);
        }
    }
}