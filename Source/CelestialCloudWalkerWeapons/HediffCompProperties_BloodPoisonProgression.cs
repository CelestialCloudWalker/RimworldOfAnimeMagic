using RimWorld;
using UnityEngine;
using Verse;

namespace AnimeArsenal
{
    public class HediffCompProperties_BloodPoisonProgression : HediffCompProperties
    {
        public float baseGainPerInterval = 0.08f;

        public float minGainPerInterval = 0.02f;

        public SkillDef resistSkill;

        public int maxSkillLevel = 20;

        public int intervalTicks = 100;

        public HediffCompProperties_BloodPoisonProgression()
        {
            compClass = typeof(HediffComp_BloodPoisonProgression);
        }
    }

    public class HediffComp_BloodPoisonProgression : HediffComp
    {
        HediffCompProperties_BloodPoisonProgression Props =>
            (HediffCompProperties_BloodPoisonProgression)props;

        private int ticker = 0;

        public override void CompPostTick(ref float severityAdjustment)
        {
            ticker++;
            if (ticker < Props.intervalTicks) return;
            ticker = 0;

            float skillFactor = 0f;

            if (Props.resistSkill != null && Pawn?.skills != null)
            {
                SkillRecord skill = Pawn.skills.GetSkill(Props.resistSkill);
                if (skill != null)
                    skillFactor = Mathf.Clamp01(skill.Level / (float)Props.maxSkillLevel);
            }

            float gain = Mathf.Lerp(Props.baseGainPerInterval,
                Props.minGainPerInterval, skillFactor);
            severityAdjustment += gain;
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref ticker, "ticker", 0);
        }
    }
}