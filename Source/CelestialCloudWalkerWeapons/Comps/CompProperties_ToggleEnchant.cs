using EMF;
using RimWorld;
using System.Linq;
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
        private bool ranOutOfResources = false;

        private Effecter _ambientEffecter;
        private Effecter _exhaustionEffecter;

        private Gene_BasicResource _ResourceGene;
        private Gene_BasicResource ResourceGene
        {
            get
            {
                if (_ResourceGene != null) return _ResourceGene;
                if (Props.enchantDef.resourceGene != null)
                {
                    _ResourceGene = (Gene_BasicResource)parent.pawn.genes.GetGene(Props.enchantDef.resourceGene);
                    if (_ResourceGene != null) return _ResourceGene;
                }
                _ResourceGene = parent.pawn.genes.GetFirstGeneOfType<Gene_BasicResource>();
                return _ResourceGene;
            }
        }

        private BreathingTechniqueGene BreathingTechniqueGene =>
            parent.pawn.genes.GenesListForReading
                .OfType<BreathingTechniqueGene>()
                .FirstOrDefault(g => !g.IsStyleGene);

        private void ForceOff()
        {
            if (!IsActive) return;
            IsActive = false;
            OnToggleOff();
        }

        public override void OnToggleOff()
        {
            RemoveHediff(parent.pawn);
            SpawnOneShot(Props.enchantDef.deactivateEffecter);
            CleanupEffecter(ref _ambientEffecter);
            CleanupEffecter(ref _exhaustionEffecter);
            BreathingTechniqueGene?.StartLingerEffect();
        }

        public override void OnToggleOn()
        {
            ranOutOfResources = false;
            ApplyHediff(parent.pawn);
            SpawnOneShot(Props.enchantDef.activateEffecter);
        }

        public override void CompTick()
        {
            base.CompTick();

            if (IsActive)
            {
                resourceCostTimer++;

                if (resourceCostTimer >= Props.enchantDef.ticksBetweenCost)
                {
                    resourceCostTimer = 0;

                    if (ResourceGene != null && Props.enchantDef.resourceCostPerTick > 0)
                    {
                        if (!ResourceGene.Has(Props.enchantDef.resourceCostPerTick))
                        {
                            ranOutOfResources = true;
                            ForceOff();
                            return;
                        }
                        ResourceGene.Consume(Props.enchantDef.resourceCostPerTick);
                    }
                }

                BreathingTechniqueGene?.TickActiveExhaustion();

                bool isExhausted = BreathingTechniqueGene?.isExhausted ?? false;
                if (isExhausted)
                {
                    CleanupEffecter(ref _ambientEffecter);
                    MaintainEffecter(ref _exhaustionEffecter, Props.enchantDef.exhaustionEffecter);
                }
                else
                {
                    CleanupEffecter(ref _exhaustionEffecter);
                    MaintainEffecter(ref _ambientEffecter, Props.enchantDef.ambientEffecter);
                }
            }
            else
            {
                CleanupEffecter(ref _ambientEffecter);
                CleanupEffecter(ref _exhaustionEffecter);

                var btGene = BreathingTechniqueGene;
                if (btGene != null)
                {
                    btGene.TickExhausted();
                    btGene.TickLinger();
                    btGene.ReduceExhaustionBuildup();
                }

                if (ranOutOfResources && ResourceGene != null
                    && ResourceGene.Has(Props.enchantDef.resourceCostPerTick))
                {
                    ranOutOfResources = false;
                }
            }
        }

        private void MaintainEffecter(ref Effecter effecter, EffecterDef def)
        {
            if (def == null) return;
            if (effecter == null)
                effecter = def.Spawn();
            effecter.EffectTick(parent.pawn, parent.pawn);
        }

        private void CleanupEffecter(ref Effecter effecter)
        {
            effecter?.Cleanup();
            effecter = null;
        }

        private void SpawnOneShot(EffecterDef def)
        {
            if (def == null) return;
            def.Spawn(parent.pawn, parent.pawn.Map).Cleanup();
        }

        public override bool CanStart()
        {
            if (Props.enchantDef == null) return false;
            if (ranOutOfResources) return false;

            if (ResourceGene != null)
                return Props.enchantDef.resourceCostPerTick > 0
                    && ResourceGene.Has(Props.enchantDef.resourceCostPerTick);

            return true;
        }

        private void ApplyHediff(Pawn pawn)
        {
            pawn.health.GetOrAddHediff(Props.enchantDef.enchantHediff);
            Messages.Message("Added " + Props.enchantDef.enchantHediff.label + " to " + pawn.Label,
                MessageTypeDefOf.NeutralEvent);
        }

        private void RemoveHediff(Pawn pawn)
        {
            Hediff existing = pawn.health.hediffSet.GetFirstHediffOfDef(Props.enchantDef.enchantHediff);
            if (existing != null)
            {
                pawn.health.RemoveHediff(existing);
                Messages.Message("Removed " + Props.enchantDef.enchantHediff.label + " from " + pawn.Label,
                    MessageTypeDefOf.NeutralEvent);
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref resourceCostTimer, "resourceCostTimer", 0);
            Scribe_Values.Look(ref ranOutOfResources, "ranOutOfResources", false);
            Scribe_Values.Look(ref IsActive, "isActive", false);
        }
    }
}