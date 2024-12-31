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
            // Ensure Props.projectileDef is valid
            if (Props?.projectileDef == null)
            {
                Log.Error("ProjectileDef is null in LaunchProjectile.");
                return;
            }

            // Ensure parent and pawn are valid
            Pawn pawn = parent?.pawn;
            if (pawn == null || pawn.Map == null)
            {
                Log.Error("Pawn or map is null in LaunchProjectile.");
                return;
            }

            // Spawn the projectile
            Projectile projectile = (Projectile)GenSpawn.Spawn(Props.projectileDef, pawn.Position, pawn.Map, WipeMode.Vanish);

            // Check if scaling is enabled (you can add your own condition here)
            bool shouldScale = Props?.damageFactor != null;

            if (shouldScale && projectile is ScalingStatDamageProjectile statDamageProjectile)
            {
                // Apply scaling if enabled
                float statValue = pawn.GetStatValue(Props.damageFactor);
                statDamageProjectile.SetDamageScale(statValue);
            }
            else
            {
                // Skip scaling if not enabled
                Log.Warning("Scaling skipped for projectile.");
            }

            // Launch the projectile regardless of scaling
            if (projectile != null)
            {
                projectile.Launch(pawn, pawn.DrawPos, target, target, ProjectileHitFlags.IntendedTarget);
            }
        }
    }
}
