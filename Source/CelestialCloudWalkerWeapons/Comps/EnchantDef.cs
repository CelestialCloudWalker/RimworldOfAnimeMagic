using Verse;
using EMF;

namespace AnimeArsenal
{
    public class EnchantDef : Def
    {
        public HediffDef enchantHediff;
        public EMF.BasicResourceGeneDef resourceGene;
        public int ticksBetweenCost = 1250;
        public float resourceCostPerTick = 1.0f;
        public EffecterDef activateEffecter;
        public EffecterDef deactivateEffecter;
        public EffecterDef ambientEffecter;
        public EffecterDef exhaustionEffecter;
        public EffecterDef hitEffecter;
    }
}