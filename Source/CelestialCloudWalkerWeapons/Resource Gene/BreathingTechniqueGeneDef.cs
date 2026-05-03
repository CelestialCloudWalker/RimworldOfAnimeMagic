using RimWorld;
using UnityEngine;
using Verse;
using EMF;

namespace AnimeArsenal
{
    public class BreathingTechniqueGeneDef : EMF.BasicResourceGeneDef
    {
        public float exhaustionPerTick = 0.001f;
        public HediffDef exhaustionHediff;
        public int ticksBeforeExhaustionStart = 1000;
        public int ticksPerExhaustionIncrease = 1000;
        public int exhausationCooldownTicks = 2000;

        public bool scaleWithBreathing = true;
        public bool canCoexistWithDemon = false;
        public GeneDef demonHybridGene;

        public StatDef maxStat;
        public StatDef regenTicks;
        public StatDef regenStat;
        public StatDef regenSpeedStat;
        public StatDef costMult;

        public Color styleBarColor = Color.white;

        public BreathingTechniqueGeneDef()
        {
            geneClass = typeof(BreathingTechniqueGene);
        }
    }
}