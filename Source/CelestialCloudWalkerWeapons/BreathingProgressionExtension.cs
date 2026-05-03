using RimWorld;
using System.Collections.Generic;
using Verse;

namespace AnimeArsenal
{
    public class BreathingProgressionExtension : DefModExtension
    {
        public List<int> killsRequiredPerLevel = new List<int> { 5, 15, 30, 60 };
        public List<int> demonKillsRequiredPerLevel = new List<int> { 2, 8, 20, 40 };
        public BreathingKillTrackerMode levelUpMode = BreathingKillTrackerMode.AnyPawn;
        public int killsRequiredForSpecialization = 30;
        public int demonKillsRequiredForSpecialization = 10;
        public BreathingKillTrackerMode specializationUnlockMode = BreathingKillTrackerMode.AnyPawn;
        public List<GeneDef> availableSpecializations;
        public bool unlockSpecializationsProgressively = false;
        public List<int> specializationUnlockThresholds;
        public List<BreathingAbilityUnlock> abilityUnlocks;
        public float breathRestoredPerKill = 10f;
        public float breathRestoredPerDemonKill = 25f;
        public float maxBreathIncreasePerKill = 0.5f;
        public float maxBreathIncreasePerDemonKill = 1.5f;
        public float maxBreathCap = 500f;
    }

    public enum BreathingKillTrackerMode
    {
        AnyPawn,
        DemonOnly,
        Both
    }

    public class BreathingAbilityUnlock
    {
        public int killsRequired = 0;
        public int demonKillsRequired = 0;
        public AbilityDef ability;
        public HediffDef hediff;
        public string unlockMessage = "{0} has unlocked {1}!";
    }
}