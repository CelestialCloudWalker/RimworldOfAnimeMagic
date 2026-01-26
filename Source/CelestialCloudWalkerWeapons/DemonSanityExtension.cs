using System.Collections.Generic;
using Verse;

namespace AnimeArsenal
{
    public class DemonSanityExtension : DefModExtension
    {
        public int ticksBetweenSanityDecay = 2500;
        public float sanityDecayPerTick = 1f;
        public int ticksSinceLastMealBeforeDecay = 60000;
        public float sanityRestoredPerPawnEaten = 25f;
        public float maxSanity = 100f;
        public float startingSanity = 100f;
        public float lowSanityThreshold = 50f;
        public float criticalSanityThreshold = 25f;
        public float mentalBreakThreshold = 10f;

        public SimpleCurve sanityToRegenMultiplier = new SimpleCurve
        {
            new CurvePoint(0f, 0.25f),
            new CurvePoint(25f, 0.5f),
            new CurvePoint(50f, 0.75f),
            new CurvePoint(75f, 1.0f),
            new CurvePoint(100f, 1.5f)
        };

        public SimpleCurve sanityToDamageMultiplier = new SimpleCurve
        {
            new CurvePoint(0f, 0.5f),
            new CurvePoint(25f, 0.75f),
            new CurvePoint(50f, 0.9f),
            new CurvePoint(75f, 1.0f),
            new CurvePoint(100f, 1.25f)
        };

        public bool showSanityMotes = true;
        public bool warnAtLowSanity = true;
        public int sanityWarningCooldown = 15000;

        public MentalStateDef cannibalismMentalState;
        public float mentalBreakMTBDays = 0.5f;
        public int searchRadiusForVictims = 25;
    }
}