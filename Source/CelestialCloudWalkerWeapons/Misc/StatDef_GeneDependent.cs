using RimWorld;
using Verse;
using System.Collections.Generic;

public class StatDef_GeneDependent : StatDef
{
    
    public List<GeneDef> requiredGenes;

    
    public List<GeneDef> requiredGenesAny;

    public StatDef_GeneDependent()
    {
        
        this.workerClass = typeof(StatWorker_GeneDependent);
    }
}