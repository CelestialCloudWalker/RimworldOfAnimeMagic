using RimWorld;
using Verse;
using System.Collections.Generic;

public class StatWorker_GeneDependent : StatWorker
{
    public override bool ShouldShowFor(StatRequest req)
    {
        
        if (req.HasThing && req.Thing is Pawn pawn)
        {
            
            if (pawn.genes == null)
                return false;

            
            StatDef_GeneDependent geneDependentStat = this.stat as StatDef_GeneDependent;
            if (geneDependentStat == null)
                return base.ShouldShowFor(req);

            
            if (!geneDependentStat.requiredGenes.NullOrEmpty())
            {
                foreach (GeneDef requiredGene in geneDependentStat.requiredGenes)
                {
                    if (!pawn.genes.HasActiveGene(requiredGene))
                        return false;
                }
            }

            
            if (!geneDependentStat.requiredGenesAny.NullOrEmpty())
            {
                bool hasAnyRequired = false;
                foreach (GeneDef requiredGene in geneDependentStat.requiredGenesAny)
                {
                    if (pawn.genes.HasActiveGene(requiredGene))
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

        
        return base.ShouldShowFor(req);
    }
}