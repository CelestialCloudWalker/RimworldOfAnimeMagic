using RimWorld;
using Verse;
using System.Linq;

namespace AnimeArsenal
{
    public class CompProperties_AbilityDemonifyWorkSlave : CompProperties_AbilityEffect
    {
        public CompProperties_AbilityDemonifyWorkSlave()
        {
            compClass = typeof(CompAbility_DemonifyWorkSlave);
        }
    }

    public class CompAbility_DemonifyWorkSlave : CompAbilityEffect
    {
        private const string BLOOD_DEMON_ART_GENE = "BloodDemonArt";

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            if (target.Pawn != null)
            {
                Pawn targetPawn = target.Pawn;

                // Add the demon work slave hediff
                Hediff demonSlaveHediff = HediffMaker.MakeHediff(DefDatabase<HediffDef>.GetNamed("DemonWorkSlaveHediff"), targetPawn);
                targetPawn.health.AddHediff(demonSlaveHediff);

                // Get the comp and set the master
                if (demonSlaveHediff.TryGetComp<HediffComp_DemonWorkSlaveEffect>() is HediffComp_DemonWorkSlaveEffect slaveComp)
                {
                    slaveComp.SetSlaveMaster(parent.pawn);
                }

                // Add the Blood Demon Art gene if the pawn doesn't already have it
                if (targetPawn.genes != null)
                {
                    GeneDef bloodDemonArtGene = DefDatabase<GeneDef>.GetNamed(BLOOD_DEMON_ART_GENE);

                    // Check if pawn already has the gene
                    if (!targetPawn.genes.HasGene(bloodDemonArtGene))
                    {
                        // Add the Blood Demon Art gene
                        Gene gene = targetPawn.genes.AddGene(bloodDemonArtGene, true);

                        // Initialize gene resource if it exists
                        if (gene is Resource_Gene resourceGene)
                        {
                            // Enable the resource
                            resourceGene.EnableResource = true;

                            // Set initial value to 50% of max
                            float maxResource = resourceGene.Max;
                            resourceGene.Value = maxResource * 0.5f;

                            // Reset regeneration ticks
                            resourceGene.ResetRegenTicks();
                        }
                    }
                }
            }
        }
    }
}