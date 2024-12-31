using RimWorld;
using Verse;

namespace AnimeArsenal
{
    public class AstralCompAbilityEffect_LaunchProjectile : CompAbilityEffect_LaunchProjectile
    {
        new CompProperties_AbilityLaunchProjectile Props => (CompProperties_AbilityLaunchProjectile)props;

        // Remove resourceGene variable as it's no longer needed
        // private Resource_Gene resourceGene;   // No longer needed

        public AstralCompAbilityEffect_LaunchProjectile()
        {
            // No need to get Resource_Gene anymore
            // resourceGene = parent.pawn.genes?.GetFirstGeneOfType<Resource_Gene>();
        }

        // Override GizmoDisabled to simplify logic
        public override bool GizmoDisabled(out string reason)
        {
            // If you don't need to check resource, just allow the ability to be used
            reason = null;
            return false; // No restriction on the ability usage
        }

        public override void Apply(Verse.LocalTargetInfo target, Verse.LocalTargetInfo dest)
        {
            // Apply the ability (launch projectile, etc.)
            base.Apply(target, dest);

            // Since we no longer need to consume any resources, just leave this empty
            // No need to consume any resource now
            // If you had any logic related to resource consumption, it's removed
        }
    }
}
