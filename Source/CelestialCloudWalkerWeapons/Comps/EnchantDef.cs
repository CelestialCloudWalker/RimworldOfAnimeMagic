using Talented;
using Verse;

namespace AnimeArsenal
{
    public class EnchantDef : Def
    {
        public HediffDef enchantHediff;
        //the ResourceGeneDef if any that powers this
        public BasicResourceGeneDef resourceGene;
        //how often resource is deducted
        public int ticksBetweenCost = 1250;
        //how much is deducted
        public float resourceCostPerTick = 1.0f;
    }
}
