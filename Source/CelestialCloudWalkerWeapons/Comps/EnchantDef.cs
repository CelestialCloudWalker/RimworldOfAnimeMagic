using Talented;
using Verse;

namespace AnimeArsenal
{
    public class EnchantDef : Def
    {
        public HediffDef enchantHediff;
        public BasicResourceGeneDef resourceGene;
        public int ticksBetweenCost = 1250;
        public float resourceCostPerTick = 1.0f;
    }
}
