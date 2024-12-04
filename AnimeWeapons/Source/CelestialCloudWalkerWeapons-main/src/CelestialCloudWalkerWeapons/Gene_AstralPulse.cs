using RimWorld;
using System;
using Verse;
using UnityEngine;
using System.Collections.Generic;

namespace CelestialCloudWalkerWeapons
{
    public class Gene_AstralPulse : Gene_Resource, IGeneResourceDrain
    {
        public bool AstralPulse = true;

        public Gene_Resource Resource => this;

        public Pawn Pawn => pawn;

        public bool CanOffset
        {
            get
            {
                if (Active)
                {
                    return !pawn.Deathresting;
                }
                return false;
            }
        }


        private int CurrentTick = 0;

        public override float Value
        {
            get => base.Value;
            set => base.Value = Mathf.Clamp(value, 0f, Max);
        }

        public float ValueCostMultiplied => Value * CostMult;

        public string DisplayLabel => Label + " (" + "Gene".Translate() + ")";
        public float ResourceLossPerDay => def.resourceLossPerDay;
        public override float InitialResourceMax => Pawn.GetStatValue(CelestialDefof.AstralPulse);
        public override float MinLevelForAlert => 0.15f;
        public override float MaxLevelOffset => 0.1f;

        private float lastMax;
        public override float Max
        {
            get
            {
                float currentMax = Pawn.GetStatValue(CelestialDefof.AstralPulse_Max, true);
                if (currentMax != lastMax)
                {
                    lastMax = currentMax;
                    ForceBaseMaxUpdate(currentMax);
                }
                return currentMax;
            }
        }
        protected override Color BarColor => new ColorInt(3, 3, 138).ToColor;
        protected override Color BarHighlightColor => new ColorInt(42, 42, 145).ToColor;


        public override int ValueForDisplay => Mathf.RoundToInt(Value);
        public override int MaxForDisplay => Mathf.RoundToInt(Max);

        public float RegenMod => Pawn.GetStatValue(CelestialDefof.AstralPulse_RegenRate, true, 100);
        public int RegenTicks => Mathf.RoundToInt(Pawn.GetStatValue(CelestialDefof.AstralPulse_RegenTicks, true, 100));
        public float CostMult => Pawn.GetStatValue(CelestialDefof.AstralPulse_Cost, true, 100);


        public float TotalUsedAstralPulse = 0;


        public override void PostAdd()
        {
            if (ModLister.CheckBiotech("Hemogen"))
            {
                base.PostAdd();
                Reset();
            }

        }

        public override void Notify_IngestedThing(Thing thing, int numTaken)
        {
            if (thing.def.IsMeat)
            {
                IngestibleProperties ingestible = thing.def.ingestible;
                if (ingestible != null && ingestible.sourceDef?.race?.Humanlike == true)
                {
                    RestoreAstralPulse(AMConstants.HumanConsumeRestoreBaseAmount * thing.GetStatValue(StatDefOf.Nutrition) * (float)numTaken);
                }
            }
        }
        private void ForceBaseMaxUpdate(float newMax)
        {
            // Force the base class to update its max value
            this.SetMax(newMax);
        }


        public void ConsumeAstralPulse(float Amount)
        {
            if (!ModsConfig.BiotechActive)
            {
                return;
            }

            TotalUsedAstralPulse += Amount;
            Value -= Amount * CostMult;
        }

        public void RestoreAstralPulse(float Amount)
        {
            if (!ModsConfig.BiotechActive)
            {
                return;
            }

            Value += Amount;
        }

        public bool HasAstralPulse(float Amount)
        {
            if (!ModsConfig.BiotechActive)
            {
                return false;
            }


            return Value >= Amount * CostMult;
        }


        public override void Tick()
        {
            base.Tick();

            CurrentTick++;
            if (CurrentTick >= RegenTicks)
            {
                //Log.Message($"Regenerating {RegenMod} after {RegenTicks} ticks.");
                RestoreAstralPulse(RegenMod);
                ResetRegenTicks();
            }
        }


        public void ResetRegenTicks()
        {
            CurrentTick = 0;
        }

        public override void SetTargetValuePct(float val)
        {
            targetValue = Mathf.Clamp(val * Max, 0f, Max - MaxLevelOffset);
        }

        public bool ShouldConsumeHemogenNow()
        {
            return Value < targetValue;
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }
            foreach (Gizmo resourceDrainGizmo in GeneResourceDrainUtility.GetResourceDrainGizmos(this))
            {
                yield return resourceDrainGizmo;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref AstralPulse, "AstralPulse", defaultValue: true);
            Scribe_Values.Look(ref CurrentTick, "currentRegenTick", defaultValue: 0);
            Scribe_Values.Look(ref TotalUsedAstralPulse, "TotalUsedAstralPulse", defaultValue: 0);
        }
    }
}
