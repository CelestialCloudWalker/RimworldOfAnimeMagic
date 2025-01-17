using Talented;
using Verse;

namespace AnimeArsenal
{
    public class BreathingTechniqueGeneDef : BasicResourceGeneDef
    {
        public HediffDef exhaustionHediff;
        //how much exhaustaion severity is gained every ticksPerExhaustionIncrease ticks (if appropiate)
        public float exhaustionPerTick = 0.1f;
        //an hour of it being active before it sets in
        public int ticksBeforeExhaustionStart = 2500;
        //once it sets in it ticks every half an hour
        public int ticksPerExhaustionIncrease = 1250;
        //how long before exhaustion stops
        public int exhausationCooldownTicks = 2500;

        public BreathingTechniqueGeneDef()
        {
            geneClass = typeof(BreathingTechniqueGene);
            this.resourceGizmoType = typeof(GeneGizmoBreath);
        }
    }
}
