﻿//using RimWorld;
//using System.Collections.Generic;
//using UnityEngine;
//using Verse;
//using static HarmonyLib.Code;

//namespace AnimeArsenal
//{
//    public class Resource_Gene : Gene_Resource, IGeneResourceDrain
//    {
//        public ResourceGeneDef Def => def != null ? (ResourceGeneDef)def : null;

//        public bool EnableResource = true;
//        public Gene_Resource Resource => this;
//        public Pawn Pawn => pawn;

//        private Color? originalSkinColor;

//        public override void PostAdd()
//        {
//            base.PostAdd();
//            ApplyCustomColor(Pawn);
//            Reset();
//        }

//        public override void PostRemove()
//        {
//            base.PostRemove();
//            RestoreOriginalColor(Pawn);
//        }

//        private void ApplyCustomColor(Pawn pawn)
//        {
//            if (Def?.skinTintChoices == null || Def.skinTintChoices.Count == 0)
//                return;


//            if (originalSkinColor == null)
//            {
//                originalSkinColor = pawn.story.skinColorOverride ?? pawn.story.SkinColorBase;
//            }


//            pawn.story.skinColorOverride = Def.skinTintChoices.RandomElement();
//        }

//        private void RestoreOriginalColor(Pawn pawn)
//        {
//            if (originalSkinColor != null)
//            {
//                pawn.story.skinColorOverride = originalSkinColor;
//                originalSkinColor = null;
//            }
//        }

//        public override void ExposeData()
//        {
//            base.ExposeData();
//            Scribe_Values.Look(ref originalSkinColor, "originalSkinColor", null);
//        }


//        public bool CanOffset
//        {
//            get
//            {
//                if (Active)
//                {
//                    return !pawn.Deathresting;
//                }
//                return false;
//            }
//        }

//        private int CurrentTick = 0;

//        public override float Value
//        {
//            get => base.Value;
//            set => base.Value = Mathf.Clamp(value, 0f, Max);
//        }

//        public float ValueCostMultiplied => Value * CostMult;
//        public string DisplayLabel => Def.resourceName + " (" + "Gene".Translate() + ")";
//        public float ResourceLossPerDay => def?.resourceLossPerDay ?? 0f;
//        public override float InitialResourceMax => Def?.maxStat != null ? Pawn.GetStatValue(Def.maxStat) : 10f;
//        public override float MinLevelForAlert => 0.15f;
//        public override float MaxLevelOffset => 0.1f;

//        private float lastMax;
//        public override float Max
//        {
//            get
//            {
//                if (Def?.maxStat == null) return 10f;
//                float currentMax = Pawn.GetStatValue(Def.maxStat, true);
//                if (currentMax != lastMax)
//                {
//                    lastMax = currentMax;
//                    ForceBaseMaxUpdate(currentMax);
//                }
//                return currentMax;
//            }
//        }

//        protected override Color BarColor => Def?.barColor ?? new ColorInt(3, 3, 138).ToColor;
//        protected override Color BarHighlightColor => new ColorInt(42, 42, 145).ToColor;

//        public override int ValueForDisplay => Mathf.RoundToInt(Value);
//        public override int MaxForDisplay => Mathf.RoundToInt(Max);

//        public float RegenAmount => Def?.regenStat != null ? Pawn.GetStatValue(Def.regenStat, true, 100) : 1f;
//        public float RegenSpeed => Def?.regenSpeedStat != null ? Pawn.GetStatValue(Def.regenSpeedStat, true, 100) : 1f;
//        public int RegenTicks => Def?.regenTicks != null ? Mathf.RoundToInt(Pawn.GetStatValue(Def.regenTicks, true, 100)) : 1250;
//        public float CostMult => Def?.costMult != null ? Pawn.GetStatValue(Def.costMult, true, 100) : 1f;

//        public float TotalResourceUsed = 0;

//        private void ForceBaseMaxUpdate(float newMax)
//        {
//            this.SetMax(newMax);
//        }

//        public void ConsumeAstralPulse(float Amount)
//        {
//            if (!ModsConfig.BiotechActive) return;
//            TotalResourceUsed += Amount;
//            Value -= Amount * CostMult;
//        }

//        public void RestoreAstralPulse(float Amount)
//        {
//            if (!ModsConfig.BiotechActive) return;
//            Value += Amount;
//        }

//        public bool HasAstralPulse(float Amount)
//        {
//            if (!ModsConfig.BiotechActive) return false;
//            return Value >= Amount * CostMult;
//        }

//        public override void Tick()
//        {
//            base.Tick();
//            CurrentTick++;
//            if (CurrentTick >= RegenTicks)
//            {
//                RestoreAstralPulse(RegenAmount);
//                ResetRegenTicks();
//            }
//        }

//        public void ResetRegenTicks()
//        {
//            CurrentTick = 0;
//        }

//        public override void SetTargetValuePct(float val)
//        {
//            targetValue = Mathf.Clamp(val * Max, 0f, Max - MaxLevelOffset);
//        }

//        public bool ShouldConsumeHemogenNow()
//        {
//            return Value < targetValue;
//        }

//        public override IEnumerable<Gizmo> GetGizmos()
//        {
//            foreach (Gizmo gizmo in base.GetGizmos())
//            {
//                yield return gizmo;
//            }
//            foreach (Gizmo resourceDrainGizmo in GeneResourceDrainUtility.GetResourceDrainGizmos(this))
//            {
//                yield return resourceDrainGizmo;
//            }
//        }
//    }
//}