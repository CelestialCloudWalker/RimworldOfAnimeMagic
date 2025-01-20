using Talented;
using UnityEngine;
using Verse;

namespace AnimeArsenal
{
    public class BreathingTechniqueGene : Gene_TalentBase
    {
        new BreathingTechniqueGeneDef Def => (BreathingTechniqueGeneDef)def;

        private int timeUntilExhaustedTimer = 0;
        public bool isExhausted = false;
        private int exhaustionCooldownRemaining = 0;
        private int exhaustionHediffTimer = 0;


        public virtual float ExhaustionProgress
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




        public void TickExhausted()
        {
            if (isExhausted)
            {
                exhaustionCooldownRemaining--;

                if (exhaustionCooldownRemaining <= 0)
                {
                    OnExhaustionEnded();

                }
            }
        }

        public void ReduceExhaustionBuildup()
        {
            if (timeUntilExhaustedTimer > 0)
            {
                timeUntilExhaustedTimer--;
            }

            if (exhaustionHediffTimer > 0)
            {
                exhaustionHediffTimer--;
            }
        }

        public void TickActiveExhaustion()
        {
            timeUntilExhaustedTimer++;

            if (timeUntilExhaustedTimer >= Def.ticksBeforeExhaustionStart)
            {
                timeUntilExhaustedTimer = 0;
                OnExhaustionStarted();
            }
            exhaustionHediffTimer++;

            if (exhaustionHediffTimer >= Def.ticksPerExhaustionIncrease)
            {
                if (Def.exhaustionHediff != null && ShouldApplyExhausation())
                {
                    Hediff hediff = this.pawn.health.GetOrAddHediff(Def.exhaustionHediff);

                    if (hediff != null)
                    {
                        hediff.Severity += Def.exhaustionPerTick;
                    }
                }

                exhaustionHediffTimer = 0;
            }
        }

        public virtual bool ShouldApplyExhausation()
        {
            return true;
        }

        private void OnExhaustionStarted()
        {
            isExhausted = true;
            exhaustionCooldownRemaining = Def.exhausationCooldownTicks;
        }

        private void OnExhaustionEnded()
        {
            exhaustionCooldownRemaining = 0;
            isExhausted = false;
        }


        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref timeUntilExhaustedTimer, "timeUntilExhaustedTimer", 0);
            Scribe_Values.Look(ref isExhausted, "isExhausted", false);
            Scribe_Values.Look(ref exhaustionCooldownRemaining, "exhaustionCooldownRemaining", 0);
            Scribe_Values.Look(ref exhaustionHediffTimer, "exhaustionHediffTimer", 0);
        }
    }
}