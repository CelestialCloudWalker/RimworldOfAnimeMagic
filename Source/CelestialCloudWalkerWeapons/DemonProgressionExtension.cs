using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace AnimeArsenal
{
    public class DemonProgressionExtension : DefModExtension
    {
        public DemonRank startingRank = DemonRank.WeakDemon;
        public List<int> pawnsRequiredPerRank = new List<int> { 3, 7, 15 };
        public bool canProgressThroughTalents = true;
        public List<RoyalTitleDef> rankTitles;
        public FactionDef titleFaction;
        public StatDef bloodPoolStat;
        public float bloodPoolBonusPerPawnEaten = 5f;
        public float bloodRestoredPerPawnEaten = 15f;
        public int pawnsRequiredForSpecialization = 50;
        public List<GeneDef> availableSpecializations;
        public bool unlockSpecializationsProgressively = true;
        public List<int> specializationUnlockThresholds;

        public List<float> sunlightDamagePerTick = new List<float> { 2.5f, 1.8f, 1.0f, 0.6f };
        public List<float> sunlightDamageThreshold = new List<float> { 20f, 45f, 65f, 100f };
        public List<int> sunlightTicksBetweenDamage = new List<int> { 120, 150, 200, 300 };
        public List<int> sunlightTicksToReset = new List<int> { 2000, 3000, 4000, 6000 };
        public List<float> sunlightMinCoverage = new List<float> { 0.45f, 0.50f, 0.60f, 0.70f };
        public List<float> sunlightTolerancePool = new List<float> { 15f, 25f, 45f, 75f };

        public List<float> regenHealingMultiplier = new List<float> { 1.5f, 2.2f, 3.5f, 5.0f };
        public List<int> regenTicksBetweenHealing = new List<int> { 120, 90, 70, 40 };
        public List<float> regenHealingPerTick = new List<float> { 0.5f, 0.7f, 1.2f, 2.0f };
        public List<bool> regenInstantLimb = new List<bool> { false, false, true, true };
        public List<bool> regenInstantOrgan = new List<bool> { false, false, false, true };
        public List<bool> regenCanRegenerateOrgans = new List<bool> { false, true, true, true };
        public List<bool> regenCanRegenerateHeart = new List<bool> { false, false, true, true };
        public List<float> regenScarHealChance = new List<float> { 0.1f, 0.15f, 0.25f, 0.4f };
        public List<int> regenScarHealInterval = new List<int> { 3000, 2200, 1800, 1000 };
        public List<float> regenResourceCostPerHeal = new List<float> { 2f, 1.5f, 1.2f, 0.8f };
        public List<float> regenResourceCostPerLimb = new List<float> { 100f, 75f, 40f, 25f };
        public List<float> regenResourceCostPerOrgan = new List<float> { 200f, 150f, 80f, 50f };
        public List<float> regenMinResourcesRequired = new List<float> { 0.2f, 0.15f, 0.12f, 0.05f };
        public List<AbilityUnlock> abilityUnlocks;
        public List<bool> bodyDisappearLeaveAsh = new List<bool> { true, true, true, true };
        public List<int> bodyDisappearFilthAmount = new List<int> { 1, 2, 3, 4 };
        public List<bool> bodyDisappearPlayEffect = new List<bool> { true, true, true, true };
        public List<string> bodyDisappearMessage = new List<string>
        {
            "Basic demon body turned to ash!",
            "Strong demon body turned to ash!",
            "Lower Moon demon body turned to ash!",
            "Upper Moon demon body turned to ash!"
        };
    }
}
