using Verse;

namespace AnimeArsenal
{
    public class EnchantDef : Def
    {
        public HediffDef enchantHediff;
        public HediffDef exhaustionHediff;
        //the ResourceGeneDef if any that powers this
        public ResourceGeneDef resourceGene;
        //how much exhaustaion severity is gained every ticksPerExhaustionIncrease ticks (if appropiate)
        public float exhaustionPerTick = 0.1f;
        //an hour of it being active before it sets in
        public int ticksBeforeExhaustionStart = 2500;
        //once it sets in it ticks every half an hour
        public int ticksPerExhaustionIncrease = 1250;
        //how long before exhaustion stops
        public int exhausationCooldownTicks = 2500;
        //how often resource is deducted
        public int ticksBetweenCost = 1250;
        //how much is deducted
        public float resourceCostPerTick = 1.0f;
    }
}
