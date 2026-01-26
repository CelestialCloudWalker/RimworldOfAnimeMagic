using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Talented;
using UnityEngine;
using Verse;
using Verse.AI;

namespace AnimeArsenal
{
    public class BloodDemonArtsGene : Gene_TalentBase
    {
        new BloodDemonArtsGeneDef Def => (BloodDemonArtsGeneDef)def;

        private int timeUntilExhaustedTimer = 0;
        public bool isExhausted = false;
        private int exhaustionCooldownRemaining = 0;
        private int exhaustionHediffTimer = 0;
        private bool hasSpecialized = false;
        private float currentSanity = 100f;
        private int ticksSinceLastMeal = 0;
        private int lastSanityWarning = 0;
        private DemonSanityExtension sanityExt;
        public bool HasSpecialized => hasSpecialized;
        private DemonRank currentRank = DemonRank.WeakDemon;
        private int totalPawnsEaten = 0;
        private DemonProgressionExtension progressionExt;
        private HashSet<int> unlockedAbilityIndices = new HashSet<int>();

        internal float lastKnownMax = -1f;
        public float CurrentSanity => currentSanity;
        public float MaxSanity => sanityExt?.maxSanity ?? 100f;
        public float SanityPercent => currentSanity / MaxSanity;
        public float RegenMultiplier => sanityExt?.sanityToRegenMultiplier?.Evaluate(currentSanity) ?? 1f;
        public float DamageMultiplier => sanityExt?.sanityToDamageMultiplier?.Evaluate(currentSanity) ?? 1f;
        public void SetSpecialized(bool value)
        {
            hasSpecialized = value;
        }

        public HashSet<int> GetUnlockedAbilityIndices()
        {
            return new HashSet<int>(unlockedAbilityIndices);
        }
        public void SetUnlockedAbilities(HashSet<int> indices)
        {
            unlockedAbilityIndices = new HashSet<int>(indices);
        }
        private void CheckForAbilityUnlocks()
        {
            if (progressionExt?.abilityUnlocks == null || progressionExt.abilityUnlocks.Count == 0)
                return;

            for (int i = 0; i < progressionExt.abilityUnlocks.Count; i++)
            {
                if (unlockedAbilityIndices.Contains(i))
                    continue;

                AbilityUnlock unlock = progressionExt.abilityUnlocks[i];

                if (totalPawnsEaten >= unlock.pawnsRequired)
                {
                    GrantAbilityUnlock(unlock, i);
                }
            }

        }

        private void GrantAbilityUnlock(AbilityUnlock unlock, int index)
        {
            if (pawn?.abilities == null)
                return;

            if (unlock.ability != null)
            {
                pawn.abilities.GainAbility(unlock.ability);

                string message = string.Format(unlock.unlockMessage,
                    pawn.Name.ToStringShort,
                    unlock.ability.label);

                Messages.Message(message, pawn, MessageTypeDefOf.PositiveEvent);
            }

            if (unlock.hediff != null)
            {
                Hediff hediff = pawn.health.GetOrAddHediff(unlock.hediff);

                if (hediff != null && !string.IsNullOrEmpty(unlock.unlockMessage))
                {
                    string message = string.Format(unlock.unlockMessage,
                        pawn.Name.ToStringShort,
                        unlock.hediff.label);

                    Messages.Message(message, pawn, MessageTypeDefOf.PositiveEvent);
                }
            }

            unlockedAbilityIndices.Add(index);

        }
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
        public void SetPawnsEaten(int count)
        {
            totalPawnsEaten = count;
        }

        public void SetRank(DemonRank rank)
        {
            currentRank = rank;
            UpdateModExtensionValues();
        }

        public bool CanSpecialize()
        {
            if (hasSpecialized) return false;
            if (progressionExt == null) return false;
            if (progressionExt.availableSpecializations == null || progressionExt.availableSpecializations.Count == 0) return false;

            return totalPawnsEaten >= progressionExt.pawnsRequiredForSpecialization && (int)currentRank >= 1;
        }

        public List<GeneDef> GetUnlockedSpecializations()
        {
            if (progressionExt?.availableSpecializations == null)
                return new List<GeneDef>();

            if (!progressionExt.unlockSpecializationsProgressively)
                return new List<GeneDef>(progressionExt.availableSpecializations);

            List<GeneDef> unlocked = new List<GeneDef>();

            for (int i = 0; i < progressionExt.availableSpecializations.Count; i++)
            {
                int threshold = 0;
                if (progressionExt.specializationUnlockThresholds != null &&
                    i < progressionExt.specializationUnlockThresholds.Count)
                {
                    threshold = progressionExt.specializationUnlockThresholds[i];
                }

                if (totalPawnsEaten >= threshold)
                {
                    unlocked.Add(progressionExt.availableSpecializations[i]);
                }
            }

            return unlocked;
        }

        public bool CanAddDemonGene(Pawn pawn)
        {
            if (pawn?.genes != null)
            {
                BloodDemonArtsGeneDef demonDef = def as BloodDemonArtsGeneDef;

                var breathingGene = pawn.genes.GenesListForReading.Find(g =>
                    g.def is BreathingTechniqueGeneDef);

                if (breathingGene != null)
                {
                    if (demonDef?.allowedBreathingGenes != null &&
                        demonDef.allowedBreathingGenes.Contains(breathingGene.def))
                    {
                        return true;
                    }

                    BreathingTechniqueGeneDef breathingDef = breathingGene.def as BreathingTechniqueGeneDef;
                    if (breathingDef?.canCoexistWithDemon == true)
                    {
                        return true;
                    }

                    return false;
                }
            }

            return true;
        }

        public override void PostAdd()
        {
            base.PostAdd();

            if (Value <= 0f)
            {
                Value = Max * 0.5f;
            }

            InitializeProgressionExtension();
            InitializeSanityExtension();
            CheckAndRemoveConflictingBreathingGenes();

            if (ModsConfig.RoyaltyActive && progressionExt?.rankTitles != null)
            {
                GrantRankTitle(DemonRank.WeakDemon);
            }
        }
        private void InitializeSanityExtension()
        {
            if (sanityExt == null)
            {
                sanityExt = def?.GetModExtension<DemonSanityExtension>();
                if (sanityExt != null && currentSanity == 0f)
                {
                    currentSanity = sanityExt.startingSanity;
                }
            }
        }


        private void InitializeProgressionExtension()
        {
            if (progressionExt == null)
            {
                progressionExt = def?.GetModExtension<DemonProgressionExtension>();
                if (progressionExt != null)
                {
                    if (currentRank == DemonRank.WeakDemon && totalPawnsEaten == 0 && Scribe.mode != LoadSaveMode.PostLoadInit)
                    {
                        currentRank = progressionExt.startingRank;
                    }
                    UpdateModExtensionValues();
                }
            }
        }

        private void CheckAndRemoveConflictingBreathingGenes()
        {
            if (pawn?.genes == null) return;

            BloodDemonArtsGeneDef demonDef = def as BloodDemonArtsGeneDef;
            var breathingGenesToRemove = new List<Gene>();

            foreach (var gene in pawn.genes.GenesListForReading)
            {
                if (gene.def is BreathingTechniqueGeneDef breathingDef)
                {
                    bool isAllowed = false;

                    if (demonDef?.allowedBreathingGenes != null &&
                        demonDef.allowedBreathingGenes.Contains(gene.def))
                    {
                        isAllowed = true;
                    }

                    if (breathingDef.canCoexistWithDemon == true)
                    {
                        isAllowed = true;
                    }

                    if (!isAllowed)
                    {
                        breathingGenesToRemove.Add(gene);
                    }
                }
            }

            if (breathingGenesToRemove.Count > 0)
            {
                foreach (var gene in breathingGenesToRemove)
                {
                    if (gene is BreathingTechniqueGene breathingGene)
                    {
                        ResetAllTalentTrees(breathingGene);
                    }

                    pawn.genes.RemoveGene(gene);
                }

                Messages.Message(
                    $"{pawn.Name.ToStringShort} lost all breathing techniques upon becoming a demon!",
                    pawn,
                    MessageTypeDefOf.NegativeEvent
                );
            }
        }

        private void ResetAllTalentTrees(Gene_TalentBase talentGene)
        {
            try
            {
                foreach (var treeData in talentGene.AvailableTrees())
                {
                    var handler = treeData.handler;
                    if (handler != null)
                    {
                        ResetTreeTracker.AllowCustomTreeReset = true;
                        handler.ResetTree();
                        ResetTreeTracker.AllowCustomTreeReset = false;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[AnimeArsenal] Error resetting talent trees: {ex.Message}");
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
                TickSanitySystem();
            }
        }

        private void TickSanitySystem()
        {
            if (sanityExt == null || pawn == null || pawn.Dead) return;

            ticksSinceLastMeal += 250;

            if (pawn.IsHashIntervalTick(sanityExt.ticksBetweenSanityDecay))
            {
                ProcessSanityDecay();
            }

            if (currentSanity <= sanityExt.mentalBreakThreshold)
            {
                TryTriggerCannibalismBreak();
            }
            else if (currentSanity <= sanityExt.criticalSanityThreshold)
            {
                ShowSanityWarning("Critical");
            }
            else if (currentSanity <= sanityExt.lowSanityThreshold)
            {
                ShowSanityWarning("Low");
            }
        }

        private void ProcessSanityDecay()
        {
            if (ticksSinceLastMeal >= sanityExt.ticksSinceLastMealBeforeDecay)
            {
                float decay = sanityExt.sanityDecayPerTick;
                int daysWithoutFood = ticksSinceLastMeal / 60000;
                decay *= (1f + (daysWithoutFood * 0.5f));

                currentSanity -= decay;
                currentSanity = Mathf.Max(0f, currentSanity);

                if (sanityExt.showSanityMotes && Rand.Chance(0.1f) && pawn.Map != null)
                {
                    MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, "Hungry...", Color.red, 2f);
                }

                if (Prefs.DevMode && pawn.IsHashIntervalTick(2500))
                {
                    Log.Message($"[Demon Sanity] {pawn.LabelShort}: Sanity={currentSanity:F1}, DaysWithoutFood={daysWithoutFood}, Decay={decay:F2}");
                }
            }
            else
            {
                float restore = sanityExt.sanityDecayPerTick * 0.25f;
                currentSanity += restore;
                currentSanity = Mathf.Min(MaxSanity, currentSanity);
            }
        }

        private void ShowSanityWarning(string severity)
        {
            if (!sanityExt.warnAtLowSanity) return;

            int currentTick = Find.TickManager.TicksGame;
            if (currentTick - lastSanityWarning < sanityExt.sanityWarningCooldown)
                return;

            Color color = severity == "Critical" ? Color.red : new Color(1f, 0.5f, 0f);

            if (Rand.Chance(0.05f) && pawn.Map != null)
            {
                MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, $"{severity} Hunger!", color, 3f);
                lastSanityWarning = currentTick;
            }
        }

        private void TryTriggerCannibalismBreak()
        {
            if (pawn.InMentalState) return;
            if (sanityExt.cannibalismMentalState == null) return;
            if (pawn.Downed || pawn.Dead) return;
            Pawn victim = FindNearestEdibleHuman();
            if (victim == null) return; 
            if (Rand.Chance(0.95f))
            {
                pawn.mindState.mentalStateHandler.TryStartMentalState(
                    sanityExt.cannibalismMentalState,
                    "Demon hunger overwhelming",
                    forceWake: true
                );

                Messages.Message(
                    $"{pawn.LabelShort}'s demonic hunger has become overwhelming! They're hunting for prey!",
                    pawn,
                    MessageTypeDefOf.ThreatBig
                );
            }
        }

        private Pawn FindNearestEdibleHuman()
        {
            if (pawn.Map == null) return null;

            return GenClosest.ClosestThingReachable(
                pawn.Position,
                pawn.Map,
                ThingRequest.ForGroup(ThingRequestGroup.Pawn),
                PathEndMode.Touch,
                TraverseParms.For(pawn),
                sanityExt.searchRadiusForVictims,
                p => p is Pawn target &&
                     target != pawn &&
                     target.RaceProps.Humanlike &&
                     !target.Dead &&
                     pawn.CanReach(target, PathEndMode.Touch, Danger.Deadly)
            ) as Pawn;
        }

        private void CheckForRankUp()
        {
            if (progressionExt == null) return;

            if ((int)currentRank >= 13) return;

            while ((int)currentRank < 13)
            {
                int rankIndex = (int)currentRank;

                if (rankIndex >= progressionExt.pawnsRequiredPerRank.Count) break;

                int pawnsNeeded = progressionExt.pawnsRequiredPerRank[rankIndex];

                if (totalPawnsEaten >= pawnsNeeded)
                {
                    RankUp();
                }
                else
                {
                    break;
                }
            }
        }

        public void ApplySpecialization(GeneDef specializationGene)
        {
            if (pawn?.genes == null || specializationGene == null)
                return;

            DemonStateTransfer transferData = new DemonStateTransfer(this);

            pawn.genes.RemoveGene(this);

            Gene newGene = pawn.genes.AddGene(specializationGene, false);

            if (newGene is BloodDemonArtsGene specializedGene)
            {
                transferData.ApplyTo(specializedGene);
                specializedGene.SetSpecialized(true);

                Messages.Message(
                    $"{pawn.Name.ToStringShort} has specialized into {specializationGene.label}!",
                    pawn,
                    MessageTypeDefOf.PositiveEvent
                );
            }
            else
            {
                Log.Error($"[AnimeArsenal] Specialization gene {specializationGene.defName} is not a BloodDemonArtsGene!");
            }
        }

        public void RankUp()
        {
            if ((int)currentRank >= 13) return;
            DemonRank previousRank = currentRank;
            currentRank = (DemonRank)((int)currentRank + 1);
            UpdateModExtensionValues();

            pawn.health.capacities.Notify_CapacityLevelsDirty();
            ForceResourceSync();

            GrantRankTitle(previousRank);

            Messages.Message($"{pawn.Name.ToStringShort} has evolved to {currentRank}!",
                           pawn, MessageTypeDefOf.PositiveEvent);
        }
        private void GrantRankTitle(DemonRank previousRank)
        {
            if (!ModsConfig.RoyaltyActive)
                return;

            if (progressionExt?.rankTitles == null || progressionExt.rankTitles.Count == 0)
                return;

            if (pawn?.royalty == null)
                return;

            if (Current.Game == null)
                return;

            if (Find.FactionManager == null)
                return;

            int rankIndex = (int)currentRank;

            if (rankIndex == 0)
                return;

            int titleIndex = rankIndex - 1;

            if (titleIndex < 0 || titleIndex >= progressionExt.rankTitles.Count || progressionExt.rankTitles[titleIndex] == null)
                return;

            Faction demonFaction = null;

            if (progressionExt.titleFaction != null)
            {
                demonFaction = Find.FactionManager.FirstFactionOfDef(progressionExt.titleFaction);
            }

            if (demonFaction == null)
                return;

            RoyalTitleDef newTitle = progressionExt.rankTitles[titleIndex];

            int previousRankIndex = (int)previousRank;
            if (previousRankIndex > 0)
            {
                int previousTitleIndex = previousRankIndex - 1;
                if (previousTitleIndex >= 0 && previousTitleIndex < progressionExt.rankTitles.Count)
                {
                    RoyalTitleDef oldTitle = progressionExt.rankTitles[previousTitleIndex];
                    if (oldTitle != null)
                    {
                        try
                        {
                            var currentTitleInFaction = pawn.royalty.GetCurrentTitle(demonFaction);
                            if (currentTitleInFaction == oldTitle)
                            {
                                pawn.royalty.SetTitle(demonFaction, null, false, false, false);
                            }
                        }
                        catch
                        {

                        }
                    }
                }
            }

            try
            {
                pawn.royalty.SetTitle(demonFaction, newTitle, false, false, false);
            }
            catch (Exception ex)
            {
                Log.Warning($"[AnimeArsenal] Could not grant title {newTitle.defName} to {pawn.Name}: {ex.Message}");
            }
        }
        public void AddPawnEaten()
        {
            totalPawnsEaten++;

            ticksSinceLastMeal = 0;

            if (sanityExt != null)
            {
                currentSanity += sanityExt.sanityRestoredPerPawnEaten;
                currentSanity = Mathf.Min(MaxSanity, currentSanity);

                if (sanityExt.showSanityMotes && pawn.Map != null)
                {
                    MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, "Satiated", Color.green, 3f);
                }
            }

            ForceResourceSync();

            pawn.health.capacities.Notify_CapacityLevelsDirty();

            if (progressionExt?.bloodRestoredPerPawnEaten > 0)
            {
                Value += progressionExt.bloodRestoredPerPawnEaten;
            }

            CheckForRankUp();
            CheckForAbilityUnlocks();
        }

        public void ForceResourceSync()
        {
            float currentMax = pawn.GetStatValue(Def.maxStat, true);
            lastKnownMax = currentMax;
            this.SetMax(currentMax);
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
                    sunlightExt.sunTolerancePool = GetValueAtRank(progressionExt.sunlightTolerancePool, rankIndex);
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

            if (CanSpecialize())
            {
                List<GeneDef> availableSpecs = GetUnlockedSpecializations();

                if (availableSpecs.Count > 0)
                {
                    yield return new Command_Action
                    {
                        defaultLabel = "Choose Blood Demon Arts",
                        defaultDesc = $"Choose a Blood Demon Art.\n\nPawns Consumed: {totalPawnsEaten}\nBlood Demon Arts Available: {availableSpecs.Count}",
                        icon = ContentFinder<Texture2D>.Get("UI/Icons/ComingSoon", false) ?? BaseContent.BadTex,
                        action = () =>
                        {
                            Find.WindowStack.Add(new Dialog_DemonSpecialization(this, availableSpecs));
                        }
                    };
                }
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
                }; yield return new Command_Action
                {
                    defaultLabel = "DEV: -25 Sanity",
                    defaultDesc = $"Reduce sanity (Current: {currentSanity:F1})",
                    action = () =>
                    {
                        currentSanity -= 25f;
                        currentSanity = Mathf.Max(0f, currentSanity);
                    }
                };

                yield return new Command_Action
                {
                    defaultLabel = "DEV: +25 Sanity",
                    defaultDesc = $"Restore sanity (Current: {currentSanity:F1})",
                    action = () =>
                    {
                        currentSanity += 25f;
                        currentSanity = Mathf.Min(MaxSanity, currentSanity);
                    }
                };

                yield return new Command_Action
                {
                    defaultLabel = "DEV: Trigger Hunger Break",
                    defaultDesc = "Force cannibalism mental state",
                    action = () =>
                    {
                        if (sanityExt?.cannibalismMentalState != null)
                        {
                            pawn.mindState.mentalStateHandler.TryStartMentalState(
                                sanityExt.cannibalismMentalState,
                                "Debug forced",
                                forceWake: true
                            );
                        }
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
            Scribe_Values.Look(ref hasSpecialized, "hasSpecialized", false);
            Scribe_Values.Look(ref currentSanity, "currentSanity", 100f);
            Scribe_Values.Look(ref ticksSinceLastMeal, "ticksSinceLastMeal", 0);
            Scribe_Values.Look(ref lastSanityWarning, "lastSanityWarning", 0);

            if (Scribe.mode == LoadSaveMode.Saving)
            {
                List<int> unlockedList = new List<int>(unlockedAbilityIndices);
                Scribe_Collections.Look(ref unlockedList, "unlockedAbilityIndices", LookMode.Value);
            }
            else if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                List<int> unlockedList = null;
                Scribe_Collections.Look(ref unlockedList, "unlockedAbilityIndices", LookMode.Value);

                if (unlockedList != null)
                {
                    unlockedAbilityIndices = new HashSet<int>(unlockedList);
                }
            }

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (unlockedAbilityIndices == null)
                    unlockedAbilityIndices = new HashSet<int>();

                if (sanityExt == null)
                {
                    sanityExt = def?.GetModExtension<DemonSanityExtension>();
                }

                if (progressionExt == null)
                {
                    progressionExt = def?.GetModExtension<DemonProgressionExtension>();
                }

                ForceResourceSync();
            }
        }
    }
}