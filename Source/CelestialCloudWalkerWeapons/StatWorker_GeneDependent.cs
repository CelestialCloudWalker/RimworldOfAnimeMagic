using RimWorld;
using Verse;
using System.Collections.Generic;

public class StatWorker_GeneDependent : StatWorker
{
    public override bool ShouldShowFor(StatRequest req)
    {
        // First check if this is a pawn request
        if (req.HasThing && req.Thing is Pawn pawn)
        {
            // Check if pawn has genes system
            if (pawn.genes == null)
                return false;

            // Cast to our custom StatDef to access the gene requirements
            StatDef_GeneDependent geneDependentStat = this.stat as StatDef_GeneDependent;
            if (geneDependentStat == null)
                return base.ShouldShowFor(req);

            // Check if any required genes are present
            if (!geneDependentStat.requiredGenes.NullOrEmpty())
            {
                foreach (GeneDef requiredGene in geneDependentStat.requiredGenes)
                {
                    if (!pawn.genes.HasGene(requiredGene))
                        return false;
                }
            }

            // Check if any of the "any required genes" are present
            if (!geneDependentStat.requiredGenesAny.NullOrEmpty())
            {
                bool hasAnyRequired = false;
                foreach (GeneDef requiredGene in geneDependentStat.requiredGenesAny)
                {
                    if (pawn.genes.HasGene(requiredGene))
                    {
                        hasAnyRequired = true;
                        break;
                    }
                }
                if (!hasAnyRequired)
                    return false;
            }

            return true;
        }

        // For non-pawn things, use default behavior
        return base.ShouldShowFor(req);
    }
}