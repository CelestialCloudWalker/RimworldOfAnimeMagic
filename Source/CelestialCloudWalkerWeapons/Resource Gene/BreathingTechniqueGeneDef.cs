using Talented;
using Verse;
namespace AnimeArsenal
{
    public class BreathingTechniqueGeneDef : TalentedGeneDef
    {
        public HediffDef exhaustionHediff;
        public float exhaustionPerTick = 0.1f;
        public int ticksBeforeExhaustionStart = 2500;
        public int ticksPerExhaustionIncrease = 1250;
        public int exhausationCooldownTicks = 2500;
        public bool scaleWithBreathing = false;

        public BreathingTechniqueGeneDef()
        {
            geneClass = typeof(BreathingTechniqueGene);
            this.resourceGizmoType = typeof(GeneGizmoBreath);
        }
    }
}