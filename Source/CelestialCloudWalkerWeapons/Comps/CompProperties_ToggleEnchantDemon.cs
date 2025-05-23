﻿using RimWorld;
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

        private int resourceCostTimer = 0;

        private Gene_BasicResource _ResourceGene;
        private Gene_BasicResource ResourceGene
        {
            get
            {
                if (_ResourceGene != null)
                    return _ResourceGene;

                if (Props.enchantDef.resourceGene != null)
                {
                    _ResourceGene = (Gene_BasicResource)parent.pawn.genes.GetGene(Props.enchantDef.resourceGene);
                    if (_ResourceGene != null)
                        return _ResourceGene;
                }

                _ResourceGene = parent.pawn.genes.GetFirstGeneOfType<Gene_BasicResource>();
                return _ResourceGene;
            }
        }

        private BloodDemonArtsGene BloodDemonArtsGene => this.parent.pawn.genes.GetFirstGeneOfType<BloodDemonArtsGene>();

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
                    if (ResourceGene != null)
                    {
                        ResourceGene.Consume(Props.enchantDef.resourceCostPerTick);
                    }

                    if (ShouldCancel())
                    {
                        this.OnToggleOff();
                    }

                    resourceCostTimer = 0;
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
                return Props.enchantDef.resourceCostPerTick > 0 && ResourceGene.Has(GetChannelCost());
            }
            return true;
        }

        private float GetChannelCost()
        {
            return Props.enchantDef != null ? Props.enchantDef.resourceCostPerTick : 5f;
        }

        private bool ShouldCancel()
        {
            if (ResourceGene != null)
            {
                return Props.enchantDef.resourceCostPerTick > 0 && !ResourceGene.Has(GetChannelCost());
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