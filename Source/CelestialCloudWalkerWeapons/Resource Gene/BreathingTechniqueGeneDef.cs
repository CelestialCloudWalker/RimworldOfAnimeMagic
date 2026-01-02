using Talented;
using Verse;
using RimWorld;
using System.Collections.Generic;

namespace AnimeArsenal
{
    public class BreathingTechniqueGeneDef : TalentedGeneDef
    {
        public float exhaustionPerTick = 0.001f;
        public HediffDef exhaustionHediff;
        public int ticksBeforeExhaustionStart = 1000;
        public int ticksPerExhaustionIncrease = 1000;
        public int exhausationCooldownTicks = 2000;

        public bool scaleWithBreathing = true;

        public bool canCoexistWithDemon = false;

        public BreathingTechniqueGeneDef()
        {
            this.geneClass = typeof(BreathingTechniqueGene);
        }
    }
}