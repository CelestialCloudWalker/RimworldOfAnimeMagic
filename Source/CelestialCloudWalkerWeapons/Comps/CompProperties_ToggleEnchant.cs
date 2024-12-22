using RimWorld;
using Verse;

namespace AnimeArsenal
{
    public class CompProperties_ToggleEnchant : CompProperties_AbilityEffect
    {
        public EnchantDef enchantDef;

        public CompProperties_ToggleEnchant()
        {
            compClass = typeof(CompAbilityEffect_ToggleEnchant);
        }
    }

    public class CompAbilityEffect_ToggleEnchant : CompAbilityEffect_Toggleable
    {
        new CompProperties_ToggleEnchant Props => (CompProperties_ToggleEnchant)props;

        private int resourceCostTimer = 0;

        private Resource_Gene _ResourceGene;
        private Resource_Gene ResourceGene
        {
            get
            {
                if (_ResourceGene != null)
                    return _ResourceGene;

                if (Props.enchantDef.resourceGene != null)
                {
                    _ResourceGene = (Resource_Gene)parent.pawn.genes.GetGene(Props.enchantDef.resourceGene);
                    if (_ResourceGene != null)
                        return _ResourceGene;
                }

                _ResourceGene = parent.pawn.genes.GetFirstGeneOfType<Resource_Gene>();
                return _ResourceGene;
            }
        }

        private BreathingTechniqueGene BreathingTechniqueGene => this.parent.pawn.genes.GetFirstGeneOfType<BreathingTechniqueGene>();

        public override void OnToggleOff()
        {
            RemoveHediff(this.parent.pawn);
        }

        public override void OnToggleOn()
        {
            ApplyHediff(this.parent.pawn);
        }

        public override void CompTick()
        {
            base.CompTick();
            if (IsActive)
            {
                resourceCostTimer += 1;

                if (resourceCostTimer >= Props.enchantDef.ticksBetweenCost)
                {
                    //make sure it isnt null
                    if (ResourceGene != null)
                    {
                        //actually consume the resource 
                        ResourceGene.ConsumeAstralPulse(Props.enchantDef.resourceCostPerTick);
                    }


                    //check if the last consumption had used up all the available resource
                    if (ShouldCancel())
                    {
                        this.OnToggleOff();
                    }

                    //reset timer for the next go around
                    resourceCostTimer = 0;
                }

                if (BreathingTechniqueGene != null)
                {
                    BreathingTechniqueGene.TickActiveExhaustion();
                }
            }
            else
            {
                if (BreathingTechniqueGene != null)
                {
                    BreathingTechniqueGene.TickExhausted();
                    BreathingTechniqueGene.ReduceExhaustionBuildup();
                }

            }
        }



        public override bool CanStart()
        {
            if (Props.enchantDef == null)
            {
                return false;
            }

            if (ResourceGene != null)
            {
                return Props.enchantDef.resourceCostPerTick > 0 && ResourceGene.HasAstralPulse(GetChannelCost());
            }
            return true;
        }

        private float GetChannelCost()
        {
            return Props.enchantDef != null ? Props.enchantDef.resourceCostPerTick * (BreathingTechniqueGene.isExhausted ? 2f : 1f) : 5f;
        }

        private bool ShouldCancel()
        {
            if (ResourceGene != null)
            {
                return Props.enchantDef.resourceCostPerTick > 0 && !ResourceGene.HasAstralPulse(GetChannelCost());
            }

            return false;
        }

        private void ApplyHediff(Pawn Pawn)
        {
            Pawn.health.GetOrAddHediff(Props.enchantDef.enchantHediff);
            Messages.Message("Added " + Props.enchantDef.enchantHediff.label + " to " + Pawn.Label,
                MessageTypeDefOf.NeutralEvent);
        }

        private void RemoveHediff(Pawn Pawn)
        {
            Hediff existingHediff = Pawn.health.hediffSet.GetFirstHediffOfDef(Props.enchantDef.enchantHediff);

            if (existingHediff != null)
            {
                Pawn.health.RemoveHediff(existingHediff);
                Messages.Message("Removed " + Props.enchantDef.enchantHediff.label + " from " + Pawn.Label,
                    MessageTypeDefOf.NeutralEvent);
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref resourceCostTimer, "resourceCostTimer", 0);
        }
    }
}