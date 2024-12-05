using RimWorld;
using Verse;

namespace CelestialCloudWalkerWeapons
{
    public class CompProperties_ResourceCost : CompProperties_AbilityEffect
    {
        public ResourceGeneDef resourceGeneDef;
        public float resourceCost = 20f;

        public CompProperties_ResourceCost()
        {
            compClass = typeof(CompAbilityEffect_ResourceCost);
        }
    }


    public class CompAbilityEffect_ResourceCost : CompAbilityEffect
    {
        public new CompProperties_ResourceCost Props
        {
            get
            {
                return (CompProperties_ResourceCost)this.props;
            }
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);


            Gene foundGene = this.parent.pawn.genes.GetGene(Props.resourceGeneDef);

            if (foundGene != null && foundGene is Resource_Gene resourceGene)
            {

                if (resourceGene.HasAstralPulse(Props.resourceCost))
                {
                    resourceGene.ConsumeAstralPulse(Props.resourceCost);
                }
                else
                {
                    //show an error or log something or not!
                }
            }

        }
    }
}
