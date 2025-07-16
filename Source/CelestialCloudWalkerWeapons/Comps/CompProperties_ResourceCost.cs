using RimWorld;
using Talented;
using Verse;

namespace AnimeArsenal
{
    public class CompProperties_ResourceCost : CompProperties_AbilityEffect
    {
        public BasicResourceGeneDef resourceGeneDef;
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
        public override bool AICanTargetNow(LocalTargetInfo target)
        {

            return true;
        }
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);


            Gene foundGene = this.parent.pawn.genes.GetGene(Props.resourceGeneDef);

            if (foundGene != null && foundGene is Gene_BasicResource resourceGene)
            {

                if (resourceGene.Has(Props.resourceCost))
                {
                    resourceGene.Consume(Props.resourceCost);
                }
                else
                {
                  
                }
            }

        }
    }
}
