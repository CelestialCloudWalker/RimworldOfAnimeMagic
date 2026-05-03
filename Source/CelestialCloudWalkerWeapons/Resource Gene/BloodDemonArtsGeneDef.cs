using RimWorld;
using System.Collections.Generic;
using EMF;
using UnityEngine;
using Verse;

namespace AnimeArsenal
{
    public class BloodDemonArtsGeneDef : EMF.BasicResourceGeneDef
    {
        public HediffDef exhaustionHediff;
        public StatDef maxStat;
        public float exhaustionPerTick = 0.1f;
        public int ticksBeforeExhaustionStart = 2500;
        public int ticksPerExhaustionIncrease = 1250;
        public int exhausationCooldownTicks = 2500;
        public List<Color> skinTintChoices;
        public float consciousnessPerPawnEaten = 0f;
        public float movingPerPawnEaten = 0f;
        public float manipulationPerPawnEaten = 0f;
        public float talkingPerPawnEaten = 0f;
        public float eatingPerPawnEaten = 0f;
        public float sightPerPawnEaten = 0f;
        public float hearingPerPawnEaten = 0f;
        public float breathingPerPawnEaten = 0f;
        public float bloodFiltrationPerPawnEaten = 0f;
        public float bloodPumpingPerPawnEaten = 0f;
        public float metabolismPerPawnEaten = 0f;
        public new List<StatModifier> statOffsets;
        public new List<StatModifier> statFactors;
        public float regenAmountBase = 5f;
        public float regenAmountPerPawnEaten = 0.05f;
        public float regenAmountMax = 50f;
        public float regenTicksBase = 2500f;
        public float regenTicksReductionPerPawnEaten = 5f;
        public float regenTicksMin = 500f;

        public List<float> bodyPartHealthPerRank;
        public float bodyPartHealthBaseMultiplier = 1f;
        public float bodyPartHealthBonusPerPawnEaten = 0f;
        public float bodyPartHealthMaxMultiplier = 0f; 


        public List<GeneDef> allowedBreathingGenes;
        public BloodDemonArtsGeneDef()
        {
            geneClass = typeof(BloodDemonArtsGene);
            this.resourceGizmoType = typeof(GeneGizmoBlood);
        }
    }
}
