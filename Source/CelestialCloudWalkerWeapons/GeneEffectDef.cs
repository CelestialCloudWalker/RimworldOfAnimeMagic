using RimWorld;
using System.Collections.Generic;
using Verse;
using Talented;

namespace Talented
{
    public class GeneEffectDef : UpgradeEffectDef
    {
        public List<GeneDef> genes;

        public override string Description => $"You gain the genes.";

        public override UpgradeEffect CreateEffect()
        {
            return new GeneEffect
            {
                genes = genes
            };
        }
    }
}