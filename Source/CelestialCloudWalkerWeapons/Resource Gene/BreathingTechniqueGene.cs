using EMF;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace AnimeArsenal
{
    public class BreathingTechniqueGene : Gene_BasicResource
    {
        new public BreathingTechniqueGeneDef Def => def as BreathingTechniqueGeneDef;

        private int timeUntilExhaustedTimer = 0;
        public bool isExhausted = false;
        private int exhaustionCooldownRemaining = 0;
        internal float lastKnownMax = -1f;
        private int regenTickCounter = 0;
        private int lingerTicksRemaining = 0;
        private const int LingerDuration = 2500;

        private float bonusMaxBreath = 0f;

        private int totalKills = 0;
        private int totalDemonKills = 0;
        private HashSet<int> unlockedAbilityIndices = new HashSet<int>();
        private bool hasSpecialized = false;
        private BreathingProgressionExtension progressionExt;

        public int TotalKills => totalKills;
        public int TotalDemonKills => totalDemonKills;
        public bool HasSpecialized => hasSpecialized;
        public void SetSpecialized(bool value) => hasSpecialized = value;
        public HashSet<int> GetUnlockedAbilityIndices() => new HashSet<int>(unlockedAbilityIndices);
        public float GetCurrentRegenAmountPublic() => GetCurrentRegenAmount();
        public int GetCurrentRegenTicksPublic() => GetCurrentRegenTicks();

        public bool IsStyleGene => Def?.primaryResourceDef == null;

        private BreathingTechniqueGene GetPotentialGene()
        {
            if (pawn?.genes == null) return null;
            foreach (var gene in pawn.genes.GenesListForReading)
                if (gene.Active && gene is BreathingTechniqueGene btg && !btg.IsStyleGene)
                    return btg;
            return null;
        }

        private BreathingTechniqueGene GetStyleGene()
        {
            if (pawn?.genes == null) return null;
            foreach (var gene in pawn.genes.GenesListForReading)
                if (gene.Active && gene is BreathingTechniqueGene btg && btg.IsStyleGene)
                    return btg;
            return null;
        }

        public override float Max
        {
            get
            {
                if (pawn == null) return base.Max;

                StatDef maxStatToUse = Def?.maxStat;
                if (IsStyleGene && maxStatToUse == null)
                {
                    var potGene = GetPotentialGene();
                    maxStatToUse = potGene?.Def?.maxStat;
                }

                float currentMax = maxStatToUse != null
                    ? pawn.GetStatValue(maxStatToUse, true)
                    : base.Max;

                if (Def?.scaleWithBreathing == true)
                    currentMax *= pawn.health.capacities.GetLevel(PawnCapacityDefOf.Breathing);

                float cap = progressionExt?.maxBreathCap ?? 500f;
                currentMax = Mathf.Min(currentMax + bonusMaxBreath, cap);

                if (lastKnownMax != currentMax)
                {
                    lastKnownMax = currentMax;
                    this.SetMax(currentMax);
                }
                return currentMax;
            }
        }

        private float GetCurrentRegenAmount()
        {
            StatDef stat = Def?.regenStat;
            return stat != null ? pawn.GetStatValue(stat, true) : base.RegenAmount;
        }

        private int GetCurrentRegenTicks()
        {
            StatDef stat = Def?.regenTicks;
            if (stat != null)
            {
                float speed = GetCurrentRegenSpeed();
                return Mathf.RoundToInt(pawn.GetStatValue(stat, true) / Mathf.Max(speed, 0.01f));
            }
            return base.RegenTicks;
        }

        private float GetCurrentRegenSpeed()
        {
            StatDef stat = Def?.regenSpeedStat;
            return stat != null ? pawn.GetStatValue(stat, true) : base.RegenSpeed;
        }

        public float InitialValue => Max * 0.5f;

        public virtual float ExhaustionProgress
        {
            get
            {
                if (Def == null) return 0f;
                if (isExhausted)
                    return Mathf.Clamp01((float)exhaustionCooldownRemaining / Def.exhausationCooldownTicks);
                return Mathf.Clamp01((float)timeUntilExhaustedTimer / Def.ticksBeforeExhaustionStart);
            }
        }


        public bool CanSpecialize()
        {
            if (hasSpecialized || progressionExt == null) return false;
            if (progressionExt.availableSpecializations == null ||
                progressionExt.availableSpecializations.Count == 0) return false;
            return MeetsKillRequirement(
                progressionExt.killsRequiredForSpecialization,
                progressionExt.demonKillsRequiredForSpecialization,
                progressionExt.specializationUnlockMode);
        }

        public List<GeneDef> GetUnlockedSpecializations()
        {
            if (progressionExt?.availableSpecializations == null)
                return new List<GeneDef>();
            if (!progressionExt.unlockSpecializationsProgressively)
                return new List<GeneDef>(progressionExt.availableSpecializations);
            var unlocked = new List<GeneDef>();
            for (int i = 0; i < progressionExt.availableSpecializations.Count; i++)
            {
                int threshold = 0;
                if (progressionExt.specializationUnlockThresholds != null &&
                    i < progressionExt.specializationUnlockThresholds.Count)
                    threshold = progressionExt.specializationUnlockThresholds[i];
                if (totalKills >= threshold)
                    unlocked.Add(progressionExt.availableSpecializations[i]);
            }
            return unlocked;
        }

        public void ApplySpecialization(GeneDef specializationGene)
        {
            if (pawn?.genes == null || specializationGene == null) return;
            var existingStyle = GetStyleGene();
            if (existingStyle != null)
                pawn.genes.RemoveGene(existingStyle);
            Gene newGene = pawn.genes.AddGene(specializationGene, false);
            if (newGene is BreathingTechniqueGene styleGene && styleGene.IsStyleGene)
            {
                hasSpecialized = true;
                Messages.Message(
                    $"{pawn.Name.ToStringShort} has mastered {specializationGene.label}!",
                    pawn, MessageTypeDefOf.PositiveEvent);
            }
            else
            {
                Log.Error($"[AnimeArsenal] {specializationGene.defName} is not a valid style BreathingTechniqueGene!");
            }
        }


        public void NotifyKill(Pawn victim)
        {
            if (victim == null) return;
            if (IsStyleGene)
            {
                GetPotentialGene()?.NotifyKill(victim);
                return;
            }

            totalKills++;

            bool isDemon = victim.genes?.GenesListForReading
                .Any(g => g.def is BloodDemonArtsGeneDef) ?? false;

            if (isDemon)
            {
                totalDemonKills++;
                if (progressionExt?.breathRestoredPerDemonKill > 0f)
                    Value = Mathf.Min(Value + progressionExt.breathRestoredPerDemonKill, Max);
                ApplyMaxBreathBonus(isDemonKill: true);
            }
            else
            {
                if (progressionExt?.breathRestoredPerKill > 0f)
                    Value = Mathf.Min(Value + progressionExt.breathRestoredPerKill, Max);
                ApplyMaxBreathBonus(isDemonKill: false);
            }

            CheckForAbilityUnlocks();
        }

        private void ApplyMaxBreathBonus(bool isDemonKill)
        {
            if (progressionExt == null) return;
            float increase = isDemonKill
                ? progressionExt.maxBreathIncreasePerDemonKill
                : progressionExt.maxBreathIncreasePerKill;
            if (increase <= 0f) return;
            float cap = progressionExt.maxBreathCap;
            bonusMaxBreath = Mathf.Min(bonusMaxBreath + increase, cap);
            lastKnownMax = -1f; 
        }

        private bool MeetsKillRequirement(int killsNeeded, int demonKillsNeeded, BreathingKillTrackerMode mode)
        {
            if (mode == BreathingKillTrackerMode.AnyPawn) return totalKills >= killsNeeded;
            if (mode == BreathingKillTrackerMode.DemonOnly) return totalDemonKills >= demonKillsNeeded;
            if (mode == BreathingKillTrackerMode.Both) return totalKills >= killsNeeded || totalDemonKills >= demonKillsNeeded;
            return false;
        }

        private void CheckForAbilityUnlocks()
        {
            if (progressionExt?.abilityUnlocks == null) return;
            for (int i = 0; i < progressionExt.abilityUnlocks.Count; i++)
            {
                if (unlockedAbilityIndices.Contains(i)) continue;
                var unlock = progressionExt.abilityUnlocks[i];
                if (MeetsKillRequirement(unlock.killsRequired, unlock.demonKillsRequired, progressionExt.levelUpMode))
                    GrantAbilityUnlock(unlock, i);
            }
        }

        private void GrantAbilityUnlock(BreathingAbilityUnlock unlock, int index)
        {
            if (pawn?.abilities == null) return;
            if (unlock.ability != null)
            {
                pawn.abilities.GainAbility(unlock.ability);
                Messages.Message(
                    string.Format(unlock.unlockMessage, pawn.Name.ToStringShort, unlock.ability.label),
                    pawn, MessageTypeDefOf.PositiveEvent);
            }
            if (unlock.hediff != null)
                pawn.health.GetOrAddHediff(unlock.hediff);
            unlockedAbilityIndices.Add(index);
        }


        private void CheckAndRemoveConflictingDemonGenes()
        {
            if (pawn?.genes == null) return;
            if (Def?.canCoexistWithDemon == true) return;
            var demonGenesToRemove = new List<Gene>();
            foreach (var gene in pawn.genes.GenesListForReading)
            {
                if (!(gene.def is BloodDemonArtsGeneDef demonDef)) continue;
                bool isAllowed = demonDef.allowedBreathingGenes != null &&
                                 demonDef.allowedBreathingGenes.Contains(def);
                if (!isAllowed)
                    demonGenesToRemove.Add(gene);
            }
            if (demonGenesToRemove.Count == 0) return;
            if (Def?.demonHybridGene != null)
                TrySwapToHybrid(demonGenesToRemove);
            else
            {
                foreach (var gene in demonGenesToRemove)
                    pawn.genes.RemoveGene(gene);
                Messages.Message(
                    $"{pawn.Name.ToStringShort} lost their demon powers upon learning breathing techniques!",
                    pawn, MessageTypeDefOf.NegativeEvent);
            }
        }

        private void TrySwapToHybrid(List<Gene> demonGenesToReplace)
        {
            BloodDemonArtsGene demonGene = demonGenesToReplace.OfType<BloodDemonArtsGene>().FirstOrDefault();
            DemonStateTransfer transfer = demonGene != null ? new DemonStateTransfer(demonGene) : null;
            int killSnapshot = totalKills;
            int demonKillSnapshot = totalDemonKills;
            foreach (var gene in demonGenesToReplace)
                pawn.genes.RemoveGene(gene);
            pawn.genes.RemoveGene(this);
            Gene newGene = pawn.genes.AddGene(Def.demonHybridGene, false);
            if (newGene is HybridDemonBreathGene hybrid)
            {
                transfer?.ApplyTo(hybrid);
                hybrid.SetKillCounts(killSnapshot, demonKillSnapshot);
                Messages.Message(
                    $"{pawn.Name.ToStringShort} has transcended humanity — demon blood and breathing technique have fused into {Def.demonHybridGene.label}!",
                    pawn, MessageTypeDefOf.PositiveEvent);
            }
            else
            {
                Log.Error($"[AnimeArsenal] demonHybridGene {Def.demonHybridGene.defName} is not a HybridDemonBreathGene!");
            }
        }


        public override void PostAdd()
        {
            base.PostAdd();
            IsRegenEnabled = false;
            if (Value <= 0f) Value = Max * 0.5f;
            progressionExt = def?.GetModExtension<BreathingProgressionExtension>();
            if (!IsStyleGene)
                CheckAndRemoveConflictingDemonGenes();
        }

        public override void PostMake()
        {
            base.PostMake();
            if (Value <= 0f) Value = InitialValue;
        }


        public override void Tick()
        {
            base.Tick();
            if (IsStyleGene) return;

            if (pawn.IsHashIntervalTick(250))
            {
                float _ = Max;
            }

            bool recovering = isExhausted || exhaustionCooldownRemaining > 0 || lingerTicksRemaining > 0;
            if (!recovering && !pawn.Dead && !pawn.Downed)
            {
                regenTickCounter++;
                if (regenTickCounter >= GetCurrentRegenTicks())
                {
                    Value = Mathf.Min(Value + GetCurrentRegenAmount(), Max);
                    regenTickCounter = 0;
                }
            }
        }

        public void TickExhausted()
        {
            if (!isExhausted) return;
            exhaustionCooldownRemaining--;
            if (exhaustionCooldownRemaining <= 0)
                OnExhaustionEnded();
        }

        public void TickLinger()
        {
            if (lingerTicksRemaining <= 0) return;
            lingerTicksRemaining--;

            if (Def?.exhaustionHediff != null && pawn != null)
            {
                Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(Def.exhaustionHediff);
                if (hediff != null)
                {
                    float progress = (float)lingerTicksRemaining / LingerDuration;
                    hediff.Severity = Mathf.Lerp(0.01f, Def.exhaustionPerTick * 10f, progress);
                }
            }

            if (lingerTicksRemaining <= 0)
                RemoveExhaustionHediff();
        }

        public void TickActiveExhaustion()
        {
            if (Def == null) return;

            lingerTicksRemaining = 0;

            if (!isExhausted)
            {
                timeUntilExhaustedTimer++;
                if (timeUntilExhaustedTimer >= Def.ticksBeforeExhaustionStart)
                {
                    timeUntilExhaustedTimer = 0;
                    OnExhaustionStarted();
                }

                if (Def.exhaustionHediff != null && ShouldApplyExhaustion())
                {
                    float progress = (float)timeUntilExhaustedTimer / Def.ticksBeforeExhaustionStart;
                    Hediff hediff = pawn.health.GetOrAddHediff(Def.exhaustionHediff);
                    if (hediff != null)
                        hediff.Severity = Mathf.Lerp(0f, Def.exhaustionPerTick * 10f, progress);
                }
            }
            else
            {
                if (Def.exhaustionHediff != null && ShouldApplyExhaustion())
                {
                    Hediff hediff = pawn.health.GetOrAddHediff(Def.exhaustionHediff);
                    if (hediff != null)
                        hediff.Severity = Def.exhaustionPerTick * 10f;
                }
            }
        }

        public void StartLingerEffect()
        {
            if (Def?.exhaustionHediff == null) return;
            Hediff existing = pawn.health.hediffSet.GetFirstHediffOfDef(Def.exhaustionHediff);
            if (existing == null) return;
            existing.Severity = Def.exhaustionPerTick * 10f;
            lingerTicksRemaining = LingerDuration;
        }

        public void ReduceExhaustionBuildup()
        {
            if (timeUntilExhaustedTimer > 0) timeUntilExhaustedTimer--;
        }

        public virtual bool ShouldApplyExhaustion() =>
            pawn != null && !pawn.Dead && !pawn.Downed;

        private void OnExhaustionStarted()
        {
            isExhausted = true;
            exhaustionCooldownRemaining = Def?.exhausationCooldownTicks ?? 0;
        }

        private void OnExhaustionEnded()
        {
            exhaustionCooldownRemaining = 0;
            isExhausted = false;
            timeUntilExhaustedTimer = 0;
            regenTickCounter = 0;
            RemoveExhaustionHediff();
        }

        private void RemoveExhaustionHediff()
        {
            if (Def?.exhaustionHediff == null || pawn == null) return;
            Hediff existing = pawn.health.hediffSet.GetFirstHediffOfDef(Def.exhaustionHediff);
            if (existing != null)
                pawn.health.RemoveHediff(existing);
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            if (IsStyleGene) yield break;

            foreach (var gizmo in base.GetGizmos())
                yield return gizmo;

            if (CanSpecialize())
            {
                var specs = GetUnlockedSpecializations();
                if (specs.Count > 0)
                {
                    yield return new Command_Action
                    {
                        defaultLabel = "Choose Breathing Style",
                        defaultDesc = $"Choose a breathing style to master.\n\nTotal Kills: {totalKills}\nDemon Kills: {totalDemonKills}\nStyles Available: {specs.Count}",
                        icon = ContentFinder<Texture2D>.Get("UI/Icons/ComingSoon", false) ?? BaseContent.BadTex,
                        action = () => Find.WindowStack.Add(new Dialog_BreathingSpecialization(this, specs))
                    };
                }
            }

            if (Prefs.DevMode && DebugSettings.godMode)
            {
                string label = !string.IsNullOrEmpty(Def?.resourceLabel) ? Def.resourceLabel : "Breath";

                yield return new Command_Action { defaultLabel = "DEV: +10 " + label, action = () => Value += 10f };
                yield return new Command_Action { defaultLabel = "DEV: -10 " + label, action = () => Value -= 10f };
                yield return new Command_Action { defaultLabel = "DEV: Fill " + label, action = () => Value = Max };
                yield return new Command_Action { defaultLabel = "DEV: Empty " + label, action = () => Value = 0f };
                yield return new Command_Action
                {
                    defaultLabel = $"DEV: +1 Kill (Total: {totalKills})",
                    action = () =>
                    {
                        totalKills++;
                        ApplyMaxBreathBonus(isDemonKill: false);
                        CheckForAbilityUnlocks();
                    }
                };
                yield return new Command_Action
                {
                    defaultLabel = $"DEV: +1 Demon Kill (Total: {totalDemonKills})",
                    action = () =>
                    {
                        totalDemonKills++;
                        totalKills++;
                        ApplyMaxBreathBonus(isDemonKill: true);
                        CheckForAbilityUnlocks();
                    }
                };
                yield return new Command_Action
                {
                    defaultLabel = $"DEV: +5 Max Breath (Bonus: {bonusMaxBreath:F1})",
                    action = () =>
                    {
                        bonusMaxBreath += 5f;
                        lastKnownMax = -1f;
                    }
                };

                if (Def?.exhaustionHediff != null)
                {
                    yield return new Command_Action
                    {
                        defaultLabel = "DEV: Add Exhaustion",
                        action = () =>
                        {
                            Hediff h = pawn.health.GetOrAddHediff(Def.exhaustionHediff);
                            if (h != null) h.Severity += Def.exhaustionPerTick * 10;
                        }
                    };
                    yield return new Command_Action
                    {
                        defaultLabel = "DEV: Remove Exhaustion",
                        action = () =>
                        {
                            Hediff h = pawn.health.hediffSet.GetFirstHediffOfDef(Def.exhaustionHediff);
                            if (h != null) pawn.health.RemoveHediff(h);
                            lingerTicksRemaining = 0;
                        }
                    };
                }

                yield return new Command_Action
                {
                    defaultLabel = "DEV: Force Exhaustion",
                    action = () =>
                    {
                        isExhausted = true;
                        exhaustionCooldownRemaining = Def?.exhausationCooldownTicks ?? 0;
                    }
                };
                yield return new Command_Action
                {
                    defaultLabel = "DEV: End Exhaustion",
                    action = () =>
                    {
                        isExhausted = false;
                        exhaustionCooldownRemaining = 0;
                        timeUntilExhaustedTimer = 0;
                        lingerTicksRemaining = 0;
                        RemoveExhaustionHediff();
                    }
                };
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref regenTickCounter, "regenTickCounter", 0);
            Scribe_Values.Look(ref timeUntilExhaustedTimer, "timeUntilExhaustedTimer", 0);
            Scribe_Values.Look(ref isExhausted, "isExhausted", false);
            Scribe_Values.Look(ref exhaustionCooldownRemaining, "exhaustionCooldownRemaining", 0);
            Scribe_Values.Look(ref lingerTicksRemaining, "lingerTicksRemaining", 0);
            Scribe_Values.Look(ref lastKnownMax, "lastKnownMax", -1f);
            Scribe_Values.Look(ref bonusMaxBreath, "bonusMaxBreath", 0f);
            Scribe_Values.Look(ref totalKills, "totalKills", 0);
            Scribe_Values.Look(ref totalDemonKills, "totalDemonKills", 0);
            Scribe_Values.Look(ref hasSpecialized, "hasSpecialized", false);

            if (Scribe.mode == LoadSaveMode.Saving)
            {
                List<int> list = new List<int>(unlockedAbilityIndices);
                Scribe_Collections.Look(ref list, "unlockedAbilityIndices", LookMode.Value);
            }
            else if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                List<int> list = null;
                Scribe_Collections.Look(ref list, "unlockedAbilityIndices", LookMode.Value);
                if (list != null) unlockedAbilityIndices = new HashSet<int>(list);
            }

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (unlockedAbilityIndices == null)
                    unlockedAbilityIndices = new HashSet<int>();
                progressionExt = def?.GetModExtension<BreathingProgressionExtension>();
                IsRegenEnabled = false;
            }
        }
    }
}