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
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            if (target.Pawn == null) return;

            var pawn = target.Pawn;

            var hediff = HediffMaker.MakeHediff(DefDatabase<HediffDef>.GetNamed("DemonWorkSlaveHediff"), pawn);
            pawn.health.AddHediff(hediff);

            var slaveComp = hediff.TryGetComp<HediffComp_DemonWorkSlaveEffect>();
            slaveComp?.SetSlaveMaster(parent.pawn);

            if (pawn.genes != null)
            {
                var bloodDemonGene = DefDatabase<GeneDef>.GetNamed("BloodDemonArt");
                if (!pawn.genes.HasActiveGene(bloodDemonGene))
                {
                    var gene = pawn.genes.AddGene(bloodDemonGene, true);
                    if (gene is Gene_BasicResource resourceGene)
                    {
                        resourceGene.EnableResource = true;
                        resourceGene.Value = resourceGene.Max * 0.5f;
                        resourceGene.ResetRegenTicks();
                    }
                }
            }
        }
    }
}