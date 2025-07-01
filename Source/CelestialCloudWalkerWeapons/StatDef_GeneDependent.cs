using RimWorld;
using Verse;
using System.Collections.Generic;

public class StatDef_GeneDependent : StatDef
{
    // All genes in this list must be present
    public List<GeneDef> requiredGenes;

    // At least one gene from this list must be present
    public List<GeneDef> requiredGenesAny;

    public StatDef_GeneDependent()
    {
        // Set the worker class by default
        this.workerClass = typeof(StatWorker_GeneDependent);
    }
}