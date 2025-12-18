using RimWorld;
using System.Collections.Generic;
using Verse;

namespace AnimeArsenal
{
    public class AbilityLevelData
    {
        public string labelSuffix;
        public string description;
        public float experienceRequired = 0f;

        public float rangeMultiplier = 1f;
        public float cooldownMultiplier = 1f;
        public float resourceCostMultiplier = 1f;

        public VerbProperties verbProperties;

        public bool cumulativeEffects = false;
        public List<CompProperties_AbilityEffect> levelComps;
    }
}