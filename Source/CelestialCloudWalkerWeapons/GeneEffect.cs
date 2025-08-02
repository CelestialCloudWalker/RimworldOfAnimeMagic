using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Talented;

namespace Talented
{
    public class GeneEffect : UpgradeEffect
    {
        public List<GeneDef> genes;

        private List<Gene> grantedGenes = new List<Gene>();

        protected override bool IsEffectAppliedTo(Pawn pawn)
        {
            if (pawn.genes == null) return false;
            grantedGenes.RemoveAll(gene => gene == null || !pawn.genes.GenesListForReading.Contains(gene));
            return genes.All(geneDef =>
                grantedGenes.Any(g => g.def == geneDef) ||
                pawn.genes.GetGene(geneDef) != null);
        }

        protected override void Apply(Pawn pawn)
        {
            base.Apply(pawn);
            if (pawn.genes == null)
                pawn.genes = new Pawn_GeneTracker(pawn);

            foreach (var geneDef in genes)
            {
                if (!grantedGenes.Any(g => g.def == geneDef))
                {
                    Gene addedGene = pawn.genes.AddGene(geneDef, xenogene: true);
                    if (addedGene != null)
                    {
                        grantedGenes.Add(addedGene);
                    }
                }
            }
        }

        protected override void RemoveExistingEffects(Pawn pawn)
        {
            if (pawn.genes != null)
            {
                foreach (var geneDef in genes)
                {
                    var existingGene = pawn.genes.GetGene(geneDef);
                    if (existingGene != null)
                    {
                        pawn.genes.RemoveGene(existingGene);
                    }
                }
            }
        }

        protected override void Remove(Pawn pawn)
        {
            if (pawn.genes != null)
            {
                foreach (var gene in grantedGenes)
                {
                    if (gene != null)
                        pawn.genes.RemoveGene(gene);
                }
            }
            grantedGenes.Clear();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref genes, "genes", LookMode.Def);
            Scribe_Collections.Look(ref grantedGenes, "grantedGenes", LookMode.Reference);
        }
    }
}