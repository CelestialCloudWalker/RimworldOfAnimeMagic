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
            var pawn = parent.pawn;
            var projectile = (Projectile)GenSpawn.Spawn(Props.projectileDef, pawn.Position, pawn.Map, WipeMode.Vanish);

            if (Props.damageFactor != null && projectile is ScalingStatDamageProjectile statProjectile)
            {
                var statValue = pawn.GetStatValue(Props.damageFactor);
                statProjectile.SetDamageScale(statValue);
            }

            projectile.Launch(pawn, pawn.DrawPos, target, target, ProjectileHitFlags.IntendedTarget);
        }
    }
}