using RimWorld;
using Talented;
using Verse;

namespace AnimeArsenal
{
    public class CompProperties_ToggleEnchantDemon : CompProperties_AbilityEffect
    {
        public EnchantDef enchantDef;

        public CompProperties_ToggleEnchantDemon()
        {
            compClass = typeof(CompAbilityEffect_ToggleEnchantDemon);
        }
    }

    public class CompAbilityEffect_ToggleEnchantDemon : CompAbilityEffect_Toggleable
    {
        new CompProperties_ToggleEnchantDemon Props => (CompProperties_ToggleEnchantDemon)props;

        private int tickCounter = 0;
        private Gene_BasicResource cachedResourceGene;

        private Gene_BasicResource GetResourceGene()
        {
            if (cachedResourceGene != null)
                return cachedResourceGene;

            if (Props.enchantDef.resourceGene != null)
            {
                cachedResourceGene = (Gene_BasicResource)parent.pawn.genes.GetGene(Props.enchantDef.resourceGene);
                if (cachedResourceGene != null)
                    return cachedResourceGene;
            }

            cachedResourceGene = parent.pawn.genes.GetFirstGeneOfType<Gene_BasicResource>();
            return cachedResourceGene;
        }

        private BloodDemonArtsGene DemonGene => this.parent.pawn.genes.GetFirstGeneOfType<BloodDemonArtsGene>();

        public override void OnToggleOff()
        {
            var hediff = parent.pawn.health.hediffSet.GetFirstHediffOfDef(Props.enchantDef.enchantHediff);
            if (hediff != null)
            {
                parent.pawn.health.RemoveHediff(hediff);
                Messages.Message("Removed " + Props.enchantDef.enchantHediff.label + " from " + parent.pawn.Label,
                    MessageTypeDefOf.NeutralEvent);
            }
        }

        public override void OnToggleOn()
        {
            parent.pawn.health.GetOrAddHediff(Props.enchantDef.enchantHediff);
            Messages.Message("Added " + Props.enchantDef.enchantHediff.label + " to " + parent.pawn.Label,
                MessageTypeDefOf.NeutralEvent);
        }

        public override void CompTick()
        {
            base.CompTick();
            if (!IsActive) return;

            tickCounter++;
            if (tickCounter >= Props.enchantDef.ticksBetweenCost)
            {
                var resourceGene = GetResourceGene();
                if (resourceGene != null)
                {
                    resourceGene.Consume(Props.enchantDef.resourceCostPerTick);
                }

                if (NeedToCancel())
                {
                    this.OnToggleOff();
                }

                tickCounter = 0;
            }
        }

        public override bool CanStart()
        {
            if (Props.enchantDef == null) return false;

            var resourceGene = GetResourceGene();
            if (resourceGene != null)
            {
                float cost = Props.enchantDef.resourceCostPerTick;
                return cost > 0 && resourceGene.Has(cost);
            }
            return true;
        }

        private bool NeedToCancel()
        {
            var resourceGene = GetResourceGene();
            if (resourceGene != null)
            {
                float cost = Props.enchantDef.resourceCostPerTick;
                return cost > 0 && !resourceGene.Has(cost);
            }
            return false;
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref tickCounter, "tickCounter", 0);
        }
    }
}