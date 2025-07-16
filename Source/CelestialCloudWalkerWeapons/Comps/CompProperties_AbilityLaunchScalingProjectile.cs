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
        public override bool AICanTargetNow(LocalTargetInfo target)
        {

            return true;
        }

        private void LaunchProjectile(LocalTargetInfo target, LocalTargetInfo dest)
        {
            
            if (Props?.projectileDef == null)
            {
                Log.Error("ProjectileDef is null in LaunchProjectile.");
                return;
            }

            
            Pawn pawn = parent?.pawn;
            if (pawn == null || pawn.Map == null)
            {
                Log.Error("Pawn or map is null in LaunchProjectile.");
                return;
            }

            
            Projectile projectile = (Projectile)GenSpawn.Spawn(Props.projectileDef, pawn.Position, pawn.Map, WipeMode.Vanish);

            
            bool shouldScale = Props?.damageFactor != null;

            if (shouldScale && projectile is ScalingStatDamageProjectile statDamageProjectile)
            {
                float statValue = pawn.GetStatValue(Props.damageFactor);
                statDamageProjectile.SetDamageScale(statValue);
            }
            else
            {
                Log.Warning("Scaling skipped for projectile.");
            }

            if (projectile != null)
            {
                projectile.Launch(pawn, pawn.DrawPos, target, target, ProjectileHitFlags.IntendedTarget);
            }
        }
    }
}
