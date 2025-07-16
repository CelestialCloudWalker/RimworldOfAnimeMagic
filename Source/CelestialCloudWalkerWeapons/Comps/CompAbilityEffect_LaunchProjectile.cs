using RimWorld;
using Verse;

namespace AnimeArsenal
{
    public class AstralCompAbilityEffect_LaunchProjectile : CompAbilityEffect_LaunchProjectile
    {
        new CompProperties_AbilityLaunchProjectile Props => (CompProperties_AbilityLaunchProjectile)props;


        public AstralCompAbilityEffect_LaunchProjectile()
        {
        }

        
        public override bool GizmoDisabled(out string reason)
        {
            
            reason = null;
            return false; 
        }

        public override void Apply(Verse.LocalTargetInfo target, Verse.LocalTargetInfo dest)
        {
            base.Apply(target, dest);
        }
    }
}
