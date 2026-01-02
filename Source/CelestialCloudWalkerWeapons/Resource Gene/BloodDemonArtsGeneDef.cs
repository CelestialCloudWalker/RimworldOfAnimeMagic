using System.Collections.Generic;
using Talented;
using UnityEngine;
using Verse;

namespace AnimeArsenal
{
    public class BloodDemonArtsGeneDef : TalentedGeneDef
    {
        public HediffDef exhaustionHediff;
        public float exhaustionPerTick = 0.1f;
        public int ticksBeforeExhaustionStart = 2500;
        public int ticksPerExhaustionIncrease = 1250;
        public int exhausationCooldownTicks = 2500;
        public List<Color> skinTintChoices;

        public List<GeneDef> allowedBreathingGenes;
        public BloodDemonArtsGeneDef()
        {
            geneClass = typeof(BloodDemonArtsGene);
            this.resourceGizmoType = typeof(GeneGizmoBlood);
        }
    }
}
