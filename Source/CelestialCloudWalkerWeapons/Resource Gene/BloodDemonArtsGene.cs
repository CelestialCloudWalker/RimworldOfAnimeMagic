using RimWorld;
using Talented;
using UnityEngine;
using Verse;
namespace AnimeArsenal
{
    public class BloodDemonArtsGene : BreathingTechniqueGene
    {
        new BloodDemonArtsGeneDef Def => (BloodDemonArtsGeneDef)def;
        private int timeUntilExhaustedTimer = 0;
        private int exhaustionCooldownRemaining = 0;
        private int exhaustionHediffTimer = 0;
        public float regenSpeed = 0.1f;
        private Color? originalSkinColor;
        public override float ExhaustionProgress
        {
            get
            {
                if (isExhausted)
                {
                    return Mathf.Clamp01((float)exhaustionCooldownRemaining / (float)Def.exhausationCooldownTicks);
                }
                else
                {
                    return Mathf.Clamp01((float)timeUntilExhaustedTimer / (float)Def.ticksBeforeExhaustionStart);
                }
            }
        }
        public override void PostAdd()
        {
            base.PostAdd();
            ApplyCustomColor(Pawn);
            Reset();
            // FIXED: Use DefDatabase.GetNamed with errorOnFail: false
            var regenHediffDef = DefDatabase<HediffDef>.GetNamed("BloodDemonRegeneration", false);
            if (regenHediffDef != null)
            {
                var regenHediff = HediffMaker.MakeHediff(regenHediffDef, pawn);
                pawn.health.AddHediff(regenHediff);
            }
        }
        public override void PostRemove()
        {
            base.PostRemove();
            RestoreOriginalColor(Pawn);
            // FIXED: Use DefDatabase.GetNamed with errorOnFail: false
            var regenHediffDef = DefDatabase<HediffDef>.GetNamed("BloodDemonRegeneration", false);
            if (regenHediffDef != null)
            {
                var regenHediff = pawn.health.hediffSet.GetFirstHediffOfDef(regenHediffDef);
                if (regenHediff != null)
                {
                    pawn.health.RemoveHediff(regenHediff);
                }
            }
        }
        private void ApplyCustomColor(Pawn pawn)
        {
            if (Def?.skinTintChoices == null || Def.skinTintChoices.Count == 0)
                return;
            if (originalSkinColor == null)
            {
                originalSkinColor = pawn.story.skinColorOverride ?? pawn.story.SkinColorBase;
            }
            pawn.story.skinColorOverride = Def.skinTintChoices.RandomElement();
        }
        private void RestoreOriginalColor(Pawn pawn)
        {
            if (originalSkinColor != null)
            {
                pawn.story.skinColorOverride = originalSkinColor;
                originalSkinColor = null;
            }
        }
        public override bool ShouldApplyExhausation()
        {
            return false;
        }
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref originalSkinColor, "originalSkinColor", null);
            Scribe_Values.Look(ref timeUntilExhaustedTimer, "timeUntilExhaustedTimer", 0);
            Scribe_Values.Look(ref exhaustionCooldownRemaining, "exhaustionCooldownRemaining", 0);
            Scribe_Values.Look(ref exhaustionHediffTimer, "exhaustionHediffTimer", 0);
        }
    }
}