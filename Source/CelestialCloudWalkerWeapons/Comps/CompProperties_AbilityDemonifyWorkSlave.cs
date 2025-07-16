using RimWorld;
using Verse;
using System.Linq;
using Talented;

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

                
                Hediff demonSlaveHediff = HediffMaker.MakeHediff(DefDatabase<HediffDef>.GetNamed("DemonWorkSlaveHediff"), targetPawn);
                targetPawn.health.AddHediff(demonSlaveHediff);

                
                if (demonSlaveHediff.TryGetComp<HediffComp_DemonWorkSlaveEffect>() is HediffComp_DemonWorkSlaveEffect slaveComp)
                {
                    slaveComp.SetSlaveMaster(parent.pawn);
                }

                if (targetPawn.genes != null)
                {
                    GeneDef bloodDemonArtGene = DefDatabase<GeneDef>.GetNamed(BLOOD_DEMON_ART_GENE);

                    if (!targetPawn.genes.HasActiveGene(bloodDemonArtGene))
                    {
                        Gene gene = targetPawn.genes.AddGene(bloodDemonArtGene, true);

                        if (gene is Gene_BasicResource resourceGene)
                        {
                            resourceGene.EnableResource = true;

                            float maxResource = resourceGene.Max;
                            resourceGene.Value = maxResource * 0.5f;

                            resourceGene.ResetRegenTicks();
                        }
                    }
                }
            }
        }
    }
}