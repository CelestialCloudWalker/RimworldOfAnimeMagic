using RimWorld;
using Verse;

namespace AnimeArsenal
{
    public class CompProperties_AbilityLaunchScalingProjectile : CompProperties_AbilityEffect
    {
        public ThingDef projectileDef;
        public StatDef damageFactor;

        public CompProperties_AbilityLaunchScalingProjectile()
        {
            compClass = typeof(CompAbilityEffect_LaunchScalingProjectile);
        }
    }

    public class CompAbilityEffect_LaunchScalingProjectile : CompAbilityEffect
    {
        public new CompProperties_AbilityLaunchScalingProjectile Props => (CompProperties_AbilityLaunchScalingProjectile)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            LaunchProjectile(target, dest);
        }

        private void LaunchProjectile(LocalTargetInfo target, LocalTargetInfo dest)
        {
            if (Props.projectileDef == null) return;

            Pawn pawn = parent.pawn;
            if (pawn == null || pawn.Map == null) return;

            Projectile projectile = (Projectile)GenSpawn.Spawn(Props.projectileDef, pawn.Position, pawn.Map, WipeMode.Vanish);


            if (projectile != null && projectile is ScalingStatDamageProjectile statDamageProjectile)
            {
                float statValue = pawn.GetStatValue(Props.damageFactor);
                statDamageProjectile.SetDamageScale(statValue);
            }

            if (projectile != null)
            {
                projectile.Launch(pawn, pawn.DrawPos, target, target, ProjectileHitFlags.IntendedTarget);
            }
        }
    }
}