using Talented;
using UnityEngine;
using Verse;
using RimWorld;
using System.Collections.Generic;

namespace AnimeArsenal
{
    public class BreathingTechniqueGene : Gene_TalentBase
    {
        new BreathingTechniqueGeneDef Def => (BreathingTechniqueGeneDef)def;
        private int timeUntilExhaustedTimer = 0;
        public bool isExhausted = false;
        private int exhaustionCooldownRemaining = 0;
        private int exhaustionHediffTimer = 0;

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

                float currentMax = pawn.GetStatValue(Def.maxStat);

                if (Def.scaleWithBreathing)
                {
                    float breathingCapacity = pawn.health.capacities.GetLevel(PawnCapacityDefOf.Breathing);
                    currentMax *= breathingCapacity;
                }

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

            if (Value <= 0f)
            {
                Value = Max * 0.5f;
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
                float currentMax = Max;
            }
        }

        private void ForceResourceSync()
        {
            float currentMax = Max;
            lastKnownMax = currentMax;
            this.SetMax(currentMax);

            Log.Message($"[DEBUG] BreathingTechnique ForceResourceSync - Set max to {currentMax}, Current Value: {Value}");
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

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var gizmo in base.GetGizmos())
            {
                if (gizmo is Command_Action cmd && Prefs.DevMode)
                {
                    string label = cmd.defaultLabel?.ToLower() ?? "";
                    if (label.Contains("dev:") || label.Contains("debug") ||
                        label.Contains("refund") || label.Contains("reset"))
                    {
                        continue;
                    }
                }
                yield return gizmo;
            }

            if (Prefs.DevMode)
            {
                yield return new Command_Action
                {
                    defaultLabel = $"DEV: {this.Label} Gain Level",
                    defaultDesc = "Gain 1 Level (Debug)",
                    action = () => GainLevel(1)
                };

                yield return new Command_Action
                {
                    defaultLabel = $"DEV: {this.Label} Max Experience",
                    defaultDesc = "Fill Experience Bar (Debug)",
                    action = () => GainExperience(MaxExperienceForLevel(CurrentLevel) - CurrentExperience - 0.1f)
                };

                foreach (var treeData in AvailableTrees())
                {
                    var treeDef = treeData.treeDef;
                    var handler = treeData.handler;

                    string treeName = !string.IsNullOrEmpty(treeDef.label) ? treeDef.label : treeDef.defName;

                    yield return new Command_Action
                    {
                        defaultLabel = $"DEV: Reset {treeName} Tree",
                        defaultDesc = $"Reset all talents in the {treeName} tree and refund spent points",
                        action = () =>
                        {
                            ResetTreeTracker.AllowCustomTreeReset = true;
                            handler.ResetTree();
                            ResetTreeTracker.AllowCustomTreeReset = false;
                        }
                    };
                }
            }

            if (Prefs.DevMode && DebugSettings.godMode)
            {
                string resourceLabel = !string.IsNullOrEmpty(Def?.resourceLabel) ? Def.resourceLabel : "Breath";

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
                        Log.Message($"[DEBUG] Fill command - Set Value to {Value}, Max is {Max}");
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

                yield return new Command_Action
                {
                    defaultLabel = "DEV: Force Resource Sync",
                    defaultDesc = "Force resource max to sync with stat",
                    action = () =>
                    {
                        ForceResourceSync();
                        Log.Message($"[DEBUG] Manual sync - Value: {Value}, Max: {Max}");
                    }
                };

                if (Def.exhaustionHediff != null)
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
                        exhaustionCooldownRemaining = Def.exhausationCooldownTicks;
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
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref timeUntilExhaustedTimer, "timeUntilExhaustedTimer", 0);
            Scribe_Values.Look(ref isExhausted, "isExhausted", false);
            Scribe_Values.Look(ref exhaustionCooldownRemaining, "exhaustionCooldownRemaining", 0);
            Scribe_Values.Look(ref exhaustionHediffTimer, "exhaustionHediffTimer", 0);
            Scribe_Values.Look(ref lastKnownMax, "lastKnownMax", -1f);
        }
    }
}