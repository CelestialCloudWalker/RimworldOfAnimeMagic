using System;
using System.Collections.Generic;
using Talented;
using UnityEngine;
using Verse;
using RimWorld;

namespace AnimeArsenal
{
    public enum DemonRank
    {
        WeakDemon = 0,
        Demon = 1,
        LowerRankSix = 2,
        LowerRankFive = 3,
        LowerRankFour = 4,
        LowerRankThree = 5,
        LowerRankTwo = 6,
        LowerRankOne = 7,
        UpperRankSix = 8,
        UpperRankFive = 9,
        UpperRankFour = 10,
        UpperRankThree = 11,
        UpperRankTwo = 12,
        UpperRankOne = 13,
    }

    public class DemonProgressionExtension : DefModExtension
    {
        public DemonRank startingRank = DemonRank.WeakDemon;
        public List<int> pawnsRequiredPerRank = new List<int> { 3, 7, 15 };
        public bool canProgressThroughTalents = true;

        public StatDef bloodPoolStat; 
        public float bloodPoolBonusPerPawnEaten = 5f;
        public float bloodRestoredPerPawnEaten = 15f;

        public List<float> sunlightDamagePerTick = new List<float> { 2.5f, 1.8f, 1.0f, 0.6f };
        public List<float> sunlightDamageThreshold = new List<float> { 20f, 45f, 65f, 100f };
        public List<int> sunlightTicksBetweenDamage = new List<int> { 120, 150, 200, 300 };
        public List<int> sunlightTicksToReset = new List<int> { 2000, 3000, 4000, 6000 };
        public List<float> sunlightMinCoverage = new List<float> { 0.45f, 0.50f, 0.60f, 0.70f };

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

    public class BloodDemonArtsGene : Gene_TalentBase
    {
        new BloodDemonArtsGeneDef Def => (BloodDemonArtsGeneDef)def;

        private int timeUntilExhaustedTimer = 0;
        public bool isExhausted = false;
        private int exhaustionCooldownRemaining = 0;
        private int exhaustionHediffTimer = 0;

        private DemonRank currentRank = DemonRank.WeakDemon;
        private int totalPawnsEaten = 0;
        private DemonProgressionExtension progressionExt;

        private float lastKnownMax = -1f;

        public override float Max
        {
            get
            {
                if (Def?.maxStat == null)
                {
                    return 100f;
                }
                if (pawn == null)
                {
                    return 100f;
                }

                float currentMax = pawn.GetStatValue(Def.maxStat, true);

                if (lastKnownMax != currentMax)
                {
                    lastKnownMax = currentMax;
                    
                    this.SetMax(currentMax);
                }

                return currentMax;
            }
        }

        public float MinValue => 0f;
        public float MaxValue => Max;
        public float InitialValue => Max * 0.5f;

        public virtual float ExhaustionProgress
        {
            get
            {
                if (Def?.exhausationCooldownTicks == null || Def?.ticksBeforeExhaustionStart == null)
                    return 0f;

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

        public DemonRank CurrentRank => currentRank;
        public int TotalPawnsEaten => totalPawnsEaten;

        public override void PostAdd()
        {
            base.PostAdd();

            if (Value <= 0f)
            {
                Value = Max * 0.5f;
            }

            progressionExt = def?.GetModExtension<DemonProgressionExtension>();
            if (progressionExt != null)
            {
                currentRank = progressionExt.startingRank;
                UpdateModExtensionValues();
            }
        }

        public override void PostMake()
        {
            base.PostMake();

            if (Value <= 0f)
            {
                Value = InitialValue;
            }
        }

        public override void Tick()
        {
            base.Tick();

            if (pawn.IsHashIntervalTick(250))
            {
                CheckForRankUp();

                float currentMax = Max;
            }
        }

        private void CheckForRankUp()
        {
            if (progressionExt == null) return;

            int rankIndex = (int)currentRank;
            if (rankIndex >= progressionExt.pawnsRequiredPerRank.Count) return;

            int pawnsNeeded = progressionExt.pawnsRequiredPerRank[rankIndex];
            if (totalPawnsEaten >= pawnsNeeded)
            {
                RankUp();
            }
        }

        public void RankUp()
        {
            if ((int)currentRank >= 13) return;

            currentRank = (DemonRank)((int)currentRank + 1);
            UpdateModExtensionValues();

            pawn.health.capacities.Notify_CapacityLevelsDirty();
            ForceResourceSync();

            Messages.Message($"{pawn.Name.ToStringShort} has evolved to {currentRank}!",
                           pawn, MessageTypeDefOf.PositiveEvent);
        }

        public void AddPawnEaten()
        {
            Log.Message($"[DEBUG] AddPawnEaten called - BEFORE: totalPawnsEaten={totalPawnsEaten}, Value={Value}, Max={Max}");

            totalPawnsEaten++;

            pawn.health.capacities.Notify_CapacityLevelsDirty();
            ForceResourceSync();

            if (progressionExt?.bloodRestoredPerPawnEaten > 0)
            {
                float oldValue = Value;
                Value += progressionExt.bloodRestoredPerPawnEaten;
                Log.Message($"[DEBUG] Added {progressionExt.bloodRestoredPerPawnEaten} blood. Value: {oldValue} -> {Value}");
            }

            Log.Message($"[DEBUG] AddPawnEaten completed - AFTER: totalPawnsEaten={totalPawnsEaten}, Value={Value}, Max={Max}");
        }

        private void ForceResourceSync()
        {
            float currentMax = pawn.GetStatValue(Def.maxStat, true);
            lastKnownMax = currentMax;
            this.SetMax(currentMax);

            Log.Message($"[DEBUG] ForceResourceSync - Set max to {currentMax}, Current Value: {Value}");
        }

        public void ForceRankUp()
        {
            if (progressionExt?.canProgressThroughTalents == true)
            {
                RankUp();
            }
        }

        public float GetStatOffset(StatDef stat)
        {
            if (progressionExt?.bloodPoolStat == stat)
            {
                return totalPawnsEaten * progressionExt.bloodPoolBonusPerPawnEaten;
            }
            return 0f;
        }

        private void UpdateModExtensionValues()
        {
            try
            {
                if (def == null || progressionExt == null) return;

                int rankIndex = (int)currentRank;

                var sunlightExt = def.GetModExtension<SunlightDamageExtension>();
                if (sunlightExt != null && progressionExt != null)
                {
                    sunlightExt.damagePerTick = GetValueAtRank(progressionExt.sunlightDamagePerTick, rankIndex);
                    sunlightExt.damageThresholdBeforeDeath = GetValueAtRank(progressionExt.sunlightDamageThreshold, rankIndex);
                    sunlightExt.ticksBetweenDamage = (int)GetValueAtRank(progressionExt.sunlightTicksBetweenDamage, rankIndex);
                    sunlightExt.ticksToResetDamage = (int)GetValueAtRank(progressionExt.sunlightTicksToReset, rankIndex);
                    sunlightExt.minimumCoverageForProtection = GetValueAtRank(progressionExt.sunlightMinCoverage, rankIndex);
                }

                var regenExt = def.GetModExtension<RegenerationExtension>();
                if (regenExt != null && progressionExt != null)
                {
                    regenExt.healingMultiplier = GetValueAtRank(progressionExt.regenHealingMultiplier, rankIndex);
                    regenExt.ticksBetweenHealing = (int)GetValueAtRank(progressionExt.regenTicksBetweenHealing, rankIndex);
                    regenExt.healingPerTick = GetValueAtRank(progressionExt.regenHealingPerTick, rankIndex);
                    regenExt.instantLimbRegeneration = GetBoolAtRank(progressionExt.regenInstantLimb, rankIndex);
                    regenExt.instantOrganRegeneration = GetBoolAtRank(progressionExt.regenInstantOrgan, rankIndex);
                    regenExt.canRegenerateOrgans = GetBoolAtRank(progressionExt.regenCanRegenerateOrgans, rankIndex);
                    regenExt.canRegenerateHeart = GetBoolAtRank(progressionExt.regenCanRegenerateHeart, rankIndex);
                    regenExt.scarHealChance = GetValueAtRank(progressionExt.regenScarHealChance, rankIndex);
                    regenExt.scarHealInterval = (int)GetValueAtRank(progressionExt.regenScarHealInterval, rankIndex);
                    regenExt.resourceCostPerHeal = GetValueAtRank(progressionExt.regenResourceCostPerHeal, rankIndex);
                    regenExt.resourceCostPerLimbRegen = GetValueAtRank(progressionExt.regenResourceCostPerLimb, rankIndex);
                    regenExt.resourceCostPerOrganRegen = GetValueAtRank(progressionExt.regenResourceCostPerOrgan, rankIndex);
                    regenExt.minimumResourcesRequired = GetValueAtRank(progressionExt.regenMinResourcesRequired, rankIndex);
                }

                var bodyDisappearExt = def.GetModExtension<BodyDisappearExtension>();
                if (bodyDisappearExt != null && progressionExt != null)
                {
                    bodyDisappearExt.leaveAshFilth = GetBoolAtRank(progressionExt.bodyDisappearLeaveAsh, rankIndex);
                    bodyDisappearExt.filthAmount = (int)GetValueAtRank(progressionExt.bodyDisappearFilthAmount, rankIndex);
                    bodyDisappearExt.playEffect = GetBoolAtRank(progressionExt.bodyDisappearPlayEffect, rankIndex);
                    if (progressionExt.bodyDisappearMessage != null && rankIndex < progressionExt.bodyDisappearMessage.Count)
                    {
                        bodyDisappearExt.disappearMessage = progressionExt.bodyDisappearMessage[rankIndex];
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[AnimeArsenal] Error in UpdateModExtensionValues(): {ex.Message}");
            }
        }

        private float GetValueAtRank<T>(List<T> values, int rank) where T : struct
        {
            if (values == null || values.Count == 0) return 0f;
            rank = Math.Min(rank, values.Count - 1);
            return Convert.ToSingle(values[rank]);
        }

        private bool GetBoolAtRank(List<bool> values, int rank)
        {
            if (values == null || values.Count == 0) return false;
            rank = Math.Min(rank, values.Count - 1);
            return values[rank];
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
            if (Def == null) return;

            timeUntilExhaustedTimer++;
            if (timeUntilExhaustedTimer >= Def.ticksBeforeExhaustionStart)
            {
                timeUntilExhaustedTimer = 0;
                OnExhaustionStarted();
            }
            exhaustionHediffTimer++;
            if (exhaustionHediffTimer >= Def.ticksPerExhaustionIncrease)
            {
                if (Def.exhaustionHediff != null && ShouldApplyExhaustion())
                {
                    Hediff hediff = pawn.health.GetOrAddHediff(Def.exhaustionHediff);
                    if (hediff != null)
                    {
                        hediff.Severity += Def.exhaustionPerTick;
                    }
                }
                exhaustionHediffTimer = 0;
            }
        }

        public virtual bool ShouldApplyExhaustion()
        {
            return pawn != null && !pawn.Dead && !pawn.Downed;
        }

        private void OnExhaustionStarted()
        {
            isExhausted = true;
            exhaustionCooldownRemaining = Def?.exhausationCooldownTicks ?? 0;
        }

        private void OnExhaustionEnded()
        {
            exhaustionCooldownRemaining = 0;
            isExhausted = false;
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }

            if (Prefs.DevMode && DebugSettings.godMode)
            {
                string resourceLabel = !string.IsNullOrEmpty(Def?.resourceLabel) ? Def.resourceLabel : "Blood";

                yield return new Command_Action
                {
                    defaultLabel = "DEV: +10 " + resourceLabel,
                    defaultDesc = "Add 10 " + resourceLabel.ToLower() + " (Debug)",
                    action = () =>
                    {
                        Value += 10f;
                    }
                };

                yield return new Command_Action
                {
                    defaultLabel = "DEV: -10 " + resourceLabel,
                    defaultDesc = "Remove 10 " + resourceLabel.ToLower() + " (Debug)",
                    action = () =>
                    {
                        Value -= 10f;
                    }
                };

                yield return new Command_Action
                {
                    defaultLabel = "DEV: Fill " + resourceLabel,
                    defaultDesc = "Fill " + resourceLabel.ToLower() + " to max (Debug)",
                    action = () =>
                    {
                        ForceResourceSync();
                        Value = Max;
                    }
                };

                yield return new Command_Action
                {
                    defaultLabel = "DEV: Empty " + resourceLabel,
                    defaultDesc = "Empty " + resourceLabel.ToLower() + " to 0 (Debug)",
                    action = () =>
                    {
                        Value = 0f;
                    }
                };

                if (Def?.exhaustionHediff != null)
                {
                    yield return new Command_Action
                    {
                        defaultLabel = "DEV: Add Exhaustion",
                        defaultDesc = "Add exhaustion hediff (Debug)",
                        action = () =>
                        {
                            Hediff hediff = pawn.health.GetOrAddHediff(Def.exhaustionHediff);
                            if (hediff != null)
                            {
                                hediff.Severity += Def.exhaustionPerTick * 10;
                            }
                        }
                    };

                    yield return new Command_Action
                    {
                        defaultLabel = "DEV: Remove Exhaustion",
                        defaultDesc = "Remove exhaustion hediff (Debug)",
                        action = () =>
                        {
                            Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(Def.exhaustionHediff);
                            if (hediff != null)
                            {
                                pawn.health.RemoveHediff(hediff);
                            }
                        }
                    };
                }

                yield return new Command_Action
                {
                    defaultLabel = "DEV: Force Exhaustion",
                    defaultDesc = "Force exhaustion state (Debug)",
                    action = () =>
                    {
                        isExhausted = true;
                        exhaustionCooldownRemaining = Def?.exhausationCooldownTicks ?? 0;
                    }
                };

                yield return new Command_Action
                {
                    defaultLabel = "DEV: End Exhaustion",
                    defaultDesc = "End exhaustion state (Debug)",
                    action = () =>
                    {
                        isExhausted = false;
                        exhaustionCooldownRemaining = 0;
                        timeUntilExhaustedTimer = 0;
                        exhaustionHediffTimer = 0;
                    }
                };

                yield return new Command_Action
                {
                    defaultLabel = $"DEV: Force Rank Up",
                    defaultDesc = $"Force rank up to next level (Current: {currentRank})",
                    action = () => ForceRankUp()
                };

                yield return new Command_Action
                {
                    defaultLabel = $"DEV: Add Pawn Eaten",
                    defaultDesc = $"Add 1 eaten pawn (Current: {totalPawnsEaten})",
                    action = () => AddPawnEaten()
                };
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref timeUntilExhaustedTimer, "timeUntilExhaustedTimer", 0);
            Scribe_Values.Look(ref isExhausted, "isExhausted", false);
            Scribe_Values.Look(ref exhaustionCooldownRemaining, "exhaustionCooldownRemaining", 0);
            Scribe_Values.Look(ref exhaustionHediffTimer, "exhaustionHediffTimer", 0);

            Scribe_Values.Look(ref currentRank, "currentRank", DemonRank.WeakDemon);
            Scribe_Values.Look(ref totalPawnsEaten, "totalPawnsEaten", 0);
            Scribe_Values.Look(ref lastKnownMax, "lastKnownMax", -1f);
        }
    }
}