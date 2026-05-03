using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EMF;
using UnityEngine;
using Verse;
using Verse.AI;

namespace AnimeArsenal
{
    public class BloodDemonArtsGene : Gene_BasicResource
    {
        new BloodDemonArtsGeneDef Def => def as BloodDemonArtsGeneDef;

        private int timeUntilExhaustedTimer = 0;
        public bool isExhausted = false;
        private int exhaustionCooldownRemaining = 0;
        private int exhaustionHediffTimer = 0;
        private bool hasSpecialized = false;
        private float currentSanity = 100f;
        private int ticksSinceLastMeal = 0;
        private int lastSanityWarning = 0;
        private DemonSanityExtension sanityExt;
        public List<float> bodyPartHealthPerRank;
        public float bodyPartHealthBaseMultiplier = 1f;
        public float bodyPartHealthBonusPerPawnEaten = 0.02f;
        public float bodyPartHealthMaxMultiplier = 3f;
        public bool HasSpecialized => hasSpecialized;
        private DemonRank currentRank = DemonRank.WeakDemon;
        private int totalPawnsEaten = 0;
        private DemonProgressionExtension progressionExt;
        private HashSet<int> unlockedAbilityIndices = new HashSet<int>();

        private int ownRegenCounter = 0;

        internal float lastKnownMax = -1f;
        public float CurrentSanity => currentSanity;
        public float MaxSanity => sanityExt?.maxSanity ?? 100f;
        public float SanityPercent => currentSanity / MaxSanity;
        public float RegenMultiplier => sanityExt?.sanityToRegenMultiplier?.Evaluate(currentSanity) ?? 1f;
        public float DamageMultiplier => sanityExt?.sanityToDamageMultiplier?.Evaluate(currentSanity) ?? 1f;

        public override float Max
        {
            get
            {
                if (pawn == null) return base.Max;

                float currentMax = Def?.maxStat != null
                    ? pawn.GetStatValue(Def.maxStat, true)
                    : base.Max;

                if (progressionExt?.bloodPoolBonusPerPawnEaten > 0f)
                    currentMax += totalPawnsEaten * progressionExt.bloodPoolBonusPerPawnEaten;

                if (lastKnownMax != currentMax)
                {
                    lastKnownMax = currentMax;
                    this.SetMax(currentMax);
                }
                return currentMax;
            }
        }

        private int ComputeRegenTicks()
        {
            if (Def == null) return 2500;
            float ticks = Def.regenTicksBase - (totalPawnsEaten * Def.regenTicksReductionPerPawnEaten);
            return Mathf.RoundToInt(Mathf.Max(ticks, Def.regenTicksMin));
        }

        private float ComputeRegenAmount()
        {
            if (Def == null) return 5f;
            float amount = Def.regenAmountBase + (totalPawnsEaten * Def.regenAmountPerPawnEaten);
            amount *= RegenMultiplier;
            return Mathf.Min(amount, Def.regenAmountMax);
        }

        public void SetSpecialized(bool value) => hasSpecialized = value;

        public HashSet<int> GetUnlockedAbilityIndices() => new HashSet<int>(unlockedAbilityIndices);

        public void SetUnlockedAbilities(HashSet<int> indices)
        {
            unlockedAbilityIndices = new HashSet<int>(indices);
        }

        public float MinValue => 0f;
        public float MaxValue => Max;
        public float InitialValue => Max * 0.5f;

        public virtual float ExhaustionProgress
        {
            get
            {
                if (Def == null) return 0f;
                if (isExhausted)
                    return Mathf.Clamp01((float)exhaustionCooldownRemaining / (float)Def.exhausationCooldownTicks);
                return Mathf.Clamp01((float)timeUntilExhaustedTimer / (float)Def.ticksBeforeExhaustionStart);
            }
        }

        public DemonRank CurrentRank => currentRank;
        public int TotalPawnsEaten => totalPawnsEaten;
        public void SetPawnsEaten(int count) => totalPawnsEaten = count;

        public void SetRank(DemonRank rank)
        {
            currentRank = rank;
            UpdateModExtensionValues();
        }

        private void CheckForAbilityUnlocks()
        {
            if (progressionExt?.abilityUnlocks == null || progressionExt.abilityUnlocks.Count == 0)
                return;

            for (int i = 0; i < progressionExt.abilityUnlocks.Count; i++)
            {
                if (unlockedAbilityIndices.Contains(i)) continue;
                AbilityUnlock unlock = progressionExt.abilityUnlocks[i];
                if (totalPawnsEaten >= unlock.pawnsRequired)
                    GrantAbilityUnlock(unlock, i);
            }
        }

        private void GrantAbilityUnlock(AbilityUnlock unlock, int index)
        {
            if (pawn?.abilities == null) return;

            if (unlock.ability != null)
            {
                pawn.abilities.GainAbility(unlock.ability);
                string message = string.Format(unlock.unlockMessage, pawn.Name.ToStringShort, unlock.ability.label);
                Messages.Message(message, pawn, MessageTypeDefOf.PositiveEvent);
            }

            if (unlock.hediff != null)
            {
                Hediff hediff = pawn.health.GetOrAddHediff(unlock.hediff);
                if (hediff != null && !string.IsNullOrEmpty(unlock.unlockMessage))
                {
                    string message = string.Format(unlock.unlockMessage, pawn.Name.ToStringShort, unlock.hediff.label);
                    Messages.Message(message, pawn, MessageTypeDefOf.PositiveEvent);
                }
            }

            unlockedAbilityIndices.Add(index);
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
                    threshold = progressionExt.specializationUnlockThresholds[i];

                if (totalPawnsEaten >= threshold)
                    unlocked.Add(progressionExt.availableSpecializations[i]);
            }
            return unlocked;
        }

        public bool CanAddDemonGene(Pawn pawn)
        {
            if (pawn?.genes != null)
            {
                BloodDemonArtsGeneDef demonDef = def as BloodDemonArtsGeneDef;
                var breathingGene = pawn.genes.GenesListForReading.Find(g => g.def is BreathingTechniqueGeneDef);

                if (breathingGene != null)
                {
                    if (demonDef?.allowedBreathingGenes != null && demonDef.allowedBreathingGenes.Contains(breathingGene.def))
                        return true;

                    BreathingTechniqueGeneDef breathingDef = breathingGene.def as BreathingTechniqueGeneDef;
                    if (breathingDef?.canCoexistWithDemon == true)
                        return true;

                    return false;
                }
            }
            return true;
        }

        public override void PostAdd()
        {
            base.PostAdd();

            IsRegenEnabled = false;

            InitializeProgressionExtension();
            InitializeSanityExtension();

            float _ = Max;

            if (Value <= 0f)
                Value = Max * 0.5f;

            CheckAndRemoveConflictingBreathingGenes();
            UpdateFoodNeed();

            if (ModsConfig.RoyaltyActive && progressionExt?.rankTitles != null)
                GrantRankTitle(DemonRank.WeakDemon);
        }

        public override void PostMake()
        {
            base.PostMake();
            if (Value <= 0f)
                Value = InitialValue;
        }

        private void InitializeSanityExtension()
        {
            if (sanityExt == null)
            {
                sanityExt = def?.GetModExtension<DemonSanityExtension>();
                if (sanityExt != null && currentSanity == 0f)
                    currentSanity = sanityExt.startingSanity;
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
                        currentRank = progressionExt.startingRank;

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

                    if (demonDef?.allowedBreathingGenes != null && demonDef.allowedBreathingGenes.Contains(gene.def))
                        isAllowed = true;

                    if (breathingDef.canCoexistWithDemon == true)
                        isAllowed = true;

                    if (!isAllowed)
                        breathingGenesToRemove.Add(gene);
                }
            }

            if (breathingGenesToRemove.Count > 0)
            {
                foreach (var gene in breathingGenesToRemove)
                    pawn.genes.RemoveGene(gene);

                Messages.Message(
                    $"{pawn.Name.ToStringShort} lost all breathing techniques upon becoming a demon!",
                    pawn, MessageTypeDefOf.NegativeEvent);
            }
        }

        private int lastThresholdCheckTick = 0;

        public override void Tick()
        {
            base.Tick();

            if (pawn.IsHashIntervalTick(250))
            {
                float _ = Max;
                CheckForRankUp();
                TickSanitySystem();
            }

            if (!IsLocked && Active && !pawn.Deathresting)
            {
                ownRegenCounter++;
                if (ownRegenCounter >= ComputeRegenTicks())
                {
                    Value = Mathf.Min(Value + ComputeRegenAmount(), Max);
                    ownRegenCounter = 0;
                }
            }

            if (Find.TickManager.TicksGame - lastThresholdCheckTick > 250)
            {
                lastThresholdCheckTick = Find.TickManager.TicksGame;
                ApplySanityThresholdEffects();
            }
        }

        private void ApplySanityThresholdEffects()
        {
            var sanityExt = def.GetModExtension<DemonSanityExtension>();
            if (sanityExt?.thresholdEffects == null || pawn == null) return;

            float currentSanity = CurrentSanity;

            foreach (var effect in sanityExt.thresholdEffects)
            {
                bool shouldHaveEffect = currentSanity <= effect.sanityThreshold;

                if (effect.hediffToApply != null)
                {
                    Hediff existingHediff = pawn.health.hediffSet.GetFirstHediffOfDef(effect.hediffToApply);
                    if (shouldHaveEffect && existingHediff == null)
                        pawn.health.AddHediff(effect.hediffToApply);
                    else if (!shouldHaveEffect && existingHediff != null)
                        pawn.health.RemoveHediff(existingHediff);
                }

                if (effect.thoughtToApply != null && pawn.needs?.mood?.thoughts?.memories != null)
                {
                    bool hasThought = pawn.needs.mood.thoughts.memories.GetFirstMemoryOfDef(effect.thoughtToApply) != null;
                    if (shouldHaveEffect && !hasThought)
                        pawn.needs.mood.thoughts.memories.TryGainMemory(effect.thoughtToApply);
                    else if (!shouldHaveEffect && hasThought)
                        pawn.needs.mood.thoughts.memories.RemoveMemoriesOfDef(effect.thoughtToApply);
                }
            }
        }

        private void TickSanitySystem()
        {
            if (sanityExt == null || pawn == null || pawn.Dead) return;

            ticksSinceLastMeal += 250;

            if (pawn.IsHashIntervalTick(sanityExt.ticksBetweenSanityDecay))
            {
                ProcessSanityDecay();
                UpdateFoodNeed();
            }

            if (currentSanity <= sanityExt.mentalBreakThreshold)
                TryTriggerCannibalismBreak();
            else if (currentSanity <= sanityExt.criticalSanityThreshold)
                ShowSanityWarning("Critical");
            else if (currentSanity <= sanityExt.lowSanityThreshold)
                ShowSanityWarning("Low");
        }

        private void UpdateFoodNeed()
        {
            if (pawn?.needs?.food == null) return;
            pawn.needs.food.CurLevel = SanityPercent;
        }

        private void ProcessSanityDecay()
        {
            int hungerThreshold = sanityExt.ticksSinceLastMealBeforeDecay;

            if (sanityExt.useCustomHungerRate && sanityExt.daysUntilHungry > 0)
                hungerThreshold = (int)(sanityExt.daysUntilHungry * 60000f);

            if (ticksSinceLastMeal >= hungerThreshold)
            {
                float decay = sanityExt.sanityDecayPerTick * sanityExt.hungerRateMultiplier;
                int daysWithoutFood = ticksSinceLastMeal / 60000;
                decay *= (1f + (daysWithoutFood * 0.5f));

                currentSanity -= decay;
                currentSanity = Mathf.Max(0f, currentSanity);

                if (sanityExt.showSanityMotes && Rand.Chance(0.1f) && pawn.Map != null)
                    MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, "Hungry...", Color.red, 2f);

                if (Prefs.DevMode && pawn.IsHashIntervalTick(2500))
                {
                    Log.Message($"[Demon Sanity] {pawn.LabelShort}: Sanity={currentSanity:F1}, " +
                               $"DaysWithoutFood={daysWithoutFood}, Decay={decay:F2}, " +
                               $"TicksSinceMeal={ticksSinceLastMeal}/{hungerThreshold}");
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
            if (currentTick - lastSanityWarning < sanityExt.sanityWarningCooldown) return;

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
                    forceWake: true);

                Messages.Message(
                    $"{pawn.LabelShort}'s demonic hunger has become overwhelming! They're hunting for prey!",
                    pawn, MessageTypeDefOf.ThreatBig);
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
                    RankUp();
                else
                    break;
            }
        }

        public void ApplySpecialization(GeneDef specializationGene)
        {
            if (pawn?.genes == null || specializationGene == null) return;

            DemonStateTransfer transferData = new DemonStateTransfer(this);
            pawn.genes.RemoveGene(this);

            Gene newGene = pawn.genes.AddGene(specializationGene, false);

            if (newGene is BloodDemonArtsGene specializedGene)
            {
                transferData.ApplyTo(specializedGene);
                specializedGene.SetSpecialized(true);

                Messages.Message(
                    $"{pawn.Name.ToStringShort} has specialized into {specializationGene.label}!",
                    pawn, MessageTypeDefOf.PositiveEvent);
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
            NotifyBodyPartHealthChanged();
            pawn.health.capacities.Notify_CapacityLevelsDirty();
            float _ = Max;
            GrantRankTitle(previousRank);

            Messages.Message($"{pawn.Name.ToStringShort} has evolved to {currentRank}!",
                pawn, MessageTypeDefOf.PositiveEvent);
        }

        private void NotifyBodyPartHealthChanged()
        {
            if (pawn?.health?.hediffSet == null) return;

            try
            {
                pawn.health.hediffSet.DirtyCache();
                pawn.health.capacities.Notify_CapacityLevelsDirty();
                pawn.health.summaryHealth.Notify_HealthChanged();

                foreach (var part in pawn.RaceProps.body.AllParts)
                    pawn.health.hediffSet.GetPartHealth(part);

                if (pawn.Spawned)
                    pawn.Drawer.renderer.SetAllGraphicsDirty();

                PortraitsCache.SetDirty(pawn);

                if (Prefs.DevMode)
                    Log.Message($"[AnimeArsenal] {pawn.LabelShort} body part health updated. Pawns eaten: {totalPawnsEaten}, Rank: {currentRank}");
            }
            catch (Exception ex)
            {
                Log.Warning($"[AnimeArsenal] Error updating body part health: {ex.Message}");
            }
        }

        public float GetCapacityOffset(PawnCapacityDef capacity)
        {
            BloodDemonArtsGeneDef demonDef = def as BloodDemonArtsGeneDef;
            if (demonDef == null) return 0f;

            float perPawnBonus = 0f;

            if (capacity == PawnCapacityDefOf.Consciousness)
                perPawnBonus = demonDef.consciousnessPerPawnEaten;
            else if (capacity == PawnCapacityDefOf.Moving)
                perPawnBonus = demonDef.movingPerPawnEaten;
            else if (capacity == PawnCapacityDefOf.Manipulation)
                perPawnBonus = demonDef.manipulationPerPawnEaten;
            else if (capacity == PawnCapacityDefOf.Talking)
                perPawnBonus = demonDef.talkingPerPawnEaten;
            else if (capacity.defName == "Eating")
                perPawnBonus = demonDef.eatingPerPawnEaten;
            else if (capacity == PawnCapacityDefOf.Sight)
                perPawnBonus = demonDef.sightPerPawnEaten;
            else if (capacity == PawnCapacityDefOf.Hearing)
                perPawnBonus = demonDef.hearingPerPawnEaten;
            else if (capacity == PawnCapacityDefOf.Breathing)
                perPawnBonus = demonDef.breathingPerPawnEaten;
            else if (capacity == PawnCapacityDefOf.BloodFiltration)
                perPawnBonus = demonDef.bloodFiltrationPerPawnEaten;
            else if (capacity == PawnCapacityDefOf.BloodPumping)
                perPawnBonus = demonDef.bloodPumpingPerPawnEaten;
            else if (capacity.defName == "Metabolism")
                perPawnBonus = demonDef.metabolismPerPawnEaten;

            return totalPawnsEaten * perPawnBonus;
        }

        private void GrantRankTitle(DemonRank previousRank)
        {
            if (!ModsConfig.RoyaltyActive) return;
            if (progressionExt?.rankTitles == null || progressionExt.rankTitles.Count == 0) return;
            if (pawn?.royalty == null) return;
            if (Current.Game == null) return;
            if (Find.FactionManager == null) return;

            int rankIndex = (int)currentRank;
            if (rankIndex == 0) return;

            int titleIndex = rankIndex - 1;
            if (titleIndex < 0 || titleIndex >= progressionExt.rankTitles.Count || progressionExt.rankTitles[titleIndex] == null)
                return;

            Faction demonFaction = null;
            if (progressionExt.titleFaction != null)
                demonFaction = Find.FactionManager.FirstFactionOfDef(progressionExt.titleFaction);

            if (demonFaction == null) return;

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
                                pawn.royalty.SetTitle(demonFaction, null, false, false, false);
                        }
                        catch { }
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
            if (progressionExt == null)
                progressionExt = def?.GetModExtension<DemonProgressionExtension>();

            if (sanityExt == null)
                sanityExt = def?.GetModExtension<DemonSanityExtension>();

            totalPawnsEaten++;
            ticksSinceLastMeal = 0;
            ownRegenCounter = 0;

            if (sanityExt != null)
            {
                currentSanity += sanityExt.sanityRestoredPerPawnEaten;
                currentSanity = Mathf.Min(MaxSanity, currentSanity);
                UpdateFoodNeed();

                if (sanityExt.showSanityMotes && pawn.Map != null)
                    MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, "Satiated", Color.green, 3f);
            }

            float _ = Max;

            NotifyBodyPartHealthChanged();
            pawn.health.capacities.Notify_CapacityLevelsDirty();

            if (progressionExt?.bloodRestoredPerPawnEaten > 0)
                Value += progressionExt.bloodRestoredPerPawnEaten;

            CheckForRankUp();
            CheckForAbilityUnlocks();
        }

        public void ForceResourceSync()
        {
            if (pawn == null) return;
            float _ = Max;
        }

        public void ForceRankUp()
        {
            if (progressionExt?.canProgressThroughTalents == true)
                RankUp();
        }

        public float GetStatOffset(StatDef stat) => 0f;

        private void UpdateModExtensionValues()
        {
            try
            {
                if (def == null || progressionExt == null) return;

                int rankIndex = (int)currentRank;

                var sunlightExt = def.GetModExtension<SunlightDamageExtension>();
                if (sunlightExt != null)
                {
                    sunlightExt.damagePerTick = GetValueAtRank(progressionExt.sunlightDamagePerTick, rankIndex);
                    sunlightExt.damageThresholdBeforeDeath = GetValueAtRank(progressionExt.sunlightDamageThreshold, rankIndex);
                    sunlightExt.ticksBetweenDamage = (int)GetValueAtRank(progressionExt.sunlightTicksBetweenDamage, rankIndex);
                    sunlightExt.ticksToResetDamage = (int)GetValueAtRank(progressionExt.sunlightTicksToReset, rankIndex);
                    sunlightExt.minimumCoverageForProtection = GetValueAtRank(progressionExt.sunlightMinCoverage, rankIndex);
                    sunlightExt.sunTolerancePool = GetValueAtRank(progressionExt.sunlightTolerancePool, rankIndex);
                }

                var regenExt = def.GetModExtension<RegenerationExtension>();
                if (regenExt != null)
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
                if (bodyDisappearExt != null)
                {
                    bodyDisappearExt.leaveAshFilth = GetBoolAtRank(progressionExt.bodyDisappearLeaveAsh, rankIndex);
                    bodyDisappearExt.filthAmount = (int)GetValueAtRank(progressionExt.bodyDisappearFilthAmount, rankIndex);
                    bodyDisappearExt.playEffect = GetBoolAtRank(progressionExt.bodyDisappearPlayEffect, rankIndex);
                    if (progressionExt.bodyDisappearMessage != null && rankIndex < progressionExt.bodyDisappearMessage.Count)
                        bodyDisappearExt.disappearMessage = progressionExt.bodyDisappearMessage[rankIndex];
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
                    OnExhaustionEnded();
            }
        }

        public void ReduceExhaustionBuildup()
        {
            if (timeUntilExhaustedTimer > 0) timeUntilExhaustedTimer--;
            if (exhaustionHediffTimer > 0) exhaustionHediffTimer--;
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
                        hediff.Severity += Def.exhaustionPerTick;
                }
                exhaustionHediffTimer = 0;
            }
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
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var gizmo in base.GetGizmos())
                yield return gizmo;

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
                        action = () => Find.WindowStack.Add(new Dialog_DemonSpecialization(this, availableSpecs))
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
                    action = () => Value += 10f
                };

                yield return new Command_Action
                {
                    defaultLabel = "DEV: -10 " + resourceLabel,
                    defaultDesc = "Remove 10 " + resourceLabel.ToLower() + " (Debug)",
                    action = () => Value -= 10f
                };

                yield return new Command_Action
                {
                    defaultLabel = "DEV: Check HealthScale",
                    defaultDesc = "Check if HealthScale patch is working",
                    action = () =>
                    {
                        float healthScale = pawn.HealthScale;
                        BloodDemonArtsGeneDef demonDef = def as BloodDemonArtsGeneDef;
                        float expectedMultiplier = 1f;

                        if (demonDef?.bodyPartHealthBonusPerPawnEaten > 0f)
                        {
                            expectedMultiplier = demonDef.bodyPartHealthBaseMultiplier +
                                                 (totalPawnsEaten * demonDef.bodyPartHealthBonusPerPawnEaten);
                            if (demonDef.bodyPartHealthMaxMultiplier > 0f)
                                expectedMultiplier = System.Math.Min(expectedMultiplier, demonDef.bodyPartHealthMaxMultiplier);
                        }

                        Messages.Message(
                            $"HealthScale: {healthScale:F2}\nExpected Multiplier: {expectedMultiplier:F2}\n" +
                            $"Pawns Eaten: {totalPawnsEaten}\nBase: {demonDef?.bodyPartHealthBaseMultiplier:F2}\n" +
                            $"Bonus Per Pawn: {demonDef?.bodyPartHealthBonusPerPawnEaten:F3}",
                            MessageTypeDefOf.NeutralEvent);
                    }
                };

                yield return new Command_Action
                {
                    defaultLabel = "DEV: Show Body Part HP",
                    defaultDesc = "Show detailed body part health values",
                    action = () =>
                    {
                        if (pawn?.health?.hediffSet?.GetNotMissingParts() == null) return;

                        StringBuilder sb = new StringBuilder();
                        sb.AppendLine($"=== Body Part HP for {pawn.LabelShort} ===");
                        sb.AppendLine($"Rank: {currentRank}  |  Pawns Eaten: {totalPawnsEaten}");

                        float actualHealthScale = pawn.HealthScale;
                        float ourMultiplier = 1f;

                        BloodDemonArtsGeneDef demonDef = def as BloodDemonArtsGeneDef;
                        if (demonDef?.bodyPartHealthPerRank != null && demonDef.bodyPartHealthPerRank.Count > 0)
                        {
                            int ri = System.Math.Min((int)currentRank, demonDef.bodyPartHealthPerRank.Count - 1);
                            ourMultiplier = demonDef.bodyPartHealthPerRank[ri];
                        }
                        else if (demonDef?.bodyPartHealthBonusPerPawnEaten > 0f)
                        {
                            ourMultiplier = demonDef.bodyPartHealthBaseMultiplier + (totalPawnsEaten * demonDef.bodyPartHealthBonusPerPawnEaten);
                            if (demonDef.bodyPartHealthMaxMultiplier > 0f)
                                ourMultiplier = System.Math.Min(ourMultiplier, demonDef.bodyPartHealthMaxMultiplier);
                        }

                        sb.AppendLine($"Pawn HealthScale: {actualHealthScale:F2}  |  Our HP Multiplier: {ourMultiplier:F2}x");

                        var torso = pawn.RaceProps.body.AllParts.FirstOrDefault(p => p.def.defName == "Torso");
                        if (torso != null)
                            sb.AppendLine($"\nTorso — Base: {torso.def.hitPoints:F0}  Scaled: {torso.def.hitPoints * actualHealthScale:F0}");

                        sb.AppendLine();
                        foreach (var part in pawn.health.hediffSet.GetNotMissingParts().Take(10))
                        {
                            float maxHP = part.def.GetMaxHealth(pawn);
                            float curHP = pawn.health.hediffSet.GetPartHealth(part);
                            sb.AppendLine($"{part.Label}: {curHP:F0}/{maxHP:F0} (base {part.def.hitPoints:F0})");
                        }

                        Log.Message(sb.ToString());
                        Messages.Message("Body part HP logged to console", MessageTypeDefOf.NeutralEvent);
                    }
                };

                yield return new Command_Action
                {
                    defaultLabel = "DEV: Show Body HP Multiplier",
                    defaultDesc = $"Current body part HP multiplier\n\nRank: {currentRank}\nPawns Eaten: {totalPawnsEaten}",
                    action = () =>
                    {
                        float multiplier = 1f;
                        if (Def?.bodyPartHealthPerRank != null && Def.bodyPartHealthPerRank.Count > 0)
                        {
                            int ri = System.Math.Min((int)currentRank, Def.bodyPartHealthPerRank.Count - 1);
                            multiplier = Def.bodyPartHealthPerRank[ri];
                        }
                        else if (Def?.bodyPartHealthBonusPerPawnEaten > 0f)
                        {
                            multiplier = Def.bodyPartHealthBaseMultiplier + (totalPawnsEaten * Def.bodyPartHealthBonusPerPawnEaten);
                            if (Def.bodyPartHealthMaxMultiplier > 0f)
                                multiplier = System.Math.Min(multiplier, Def.bodyPartHealthMaxMultiplier);
                        }
                        Messages.Message($"Body Part HP Multiplier: {multiplier:P0} ({multiplier}x)", MessageTypeDefOf.NeutralEvent);
                    }
                };

                yield return new Command_Action
                {
                    defaultLabel = "DEV: Fill " + resourceLabel,
                    defaultDesc = "Fill " + resourceLabel.ToLower() + " to max (Debug)",
                    action = () => { float _ = Max; Value = Max; }
                };

                yield return new Command_Action
                {
                    defaultLabel = "DEV: -25 Sanity",
                    defaultDesc = $"Reduce sanity (Current: {currentSanity:F1})",
                    action = () => { currentSanity -= 25f; currentSanity = Mathf.Max(0f, currentSanity); }
                };

                yield return new Command_Action
                {
                    defaultLabel = "DEV: +25 Sanity",
                    defaultDesc = $"Restore sanity (Current: {currentSanity:F1})",
                    action = () => { currentSanity += 25f; currentSanity = Mathf.Min(MaxSanity, currentSanity); }
                };

                yield return new Command_Action
                {
                    defaultLabel = "DEV: Trigger Hunger Break",
                    defaultDesc = "Force cannibalism mental state",
                    action = () =>
                    {
                        if (sanityExt?.cannibalismMentalState != null)
                            pawn.mindState.mentalStateHandler.TryStartMentalState(sanityExt.cannibalismMentalState, "Debug forced", forceWake: true);
                    }
                };

                yield return new Command_Action
                {
                    defaultLabel = "DEV: Empty " + resourceLabel,
                    defaultDesc = "Empty " + resourceLabel.ToLower() + " to 0 (Debug)",
                    action = () => Value = 0f
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
                            if (hediff != null) hediff.Severity += Def.exhaustionPerTick * 10;
                        }
                    };

                    yield return new Command_Action
                    {
                        defaultLabel = "DEV: Remove Exhaustion",
                        defaultDesc = "Remove exhaustion hediff (Debug)",
                        action = () =>
                        {
                            Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(Def.exhaustionHediff);
                            if (hediff != null) pawn.health.RemoveHediff(hediff);
                        }
                    };
                }

                yield return new Command_Action
                {
                    defaultLabel = "DEV: Force Exhaustion",
                    defaultDesc = "Force exhaustion state (Debug)",
                    action = () => { isExhausted = true; exhaustionCooldownRemaining = Def?.exhausationCooldownTicks ?? 0; }
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

                yield return new Command_Action
                {
                    defaultLabel = "DEV: Check Regen State",
                    action = () =>
                    {
                        Log.Message($"=== Regen State: {pawn.LabelShort} ===");
                        Log.Message($"IsRegenEnabled (EMF): {IsRegenEnabled}  [should be FALSE]");
                        Log.Message($"IsLocked: {IsLocked}  |  Active: {Active}  |  Deathresting: {pawn.Deathresting}");
                        Log.Message($"OwnRegenCounter: {ownRegenCounter} / {ComputeRegenTicks()} ticks");
                        Log.Message($"ComputeRegenAmount: {ComputeRegenAmount():F2}  " +
                                    $"(base {Def?.regenAmountBase} + {totalPawnsEaten}×{Def?.regenAmountPerPawnEaten} × sanity×{RegenMultiplier:F2}, max {Def?.regenAmountMax})");
                        Log.Message($"ComputeRegenTicks: {ComputeRegenTicks()}  " +
                                    $"(base {Def?.regenTicksBase} - {totalPawnsEaten}×{Def?.regenTicksReductionPerPawnEaten}, min {Def?.regenTicksMin})");
                        Log.Message($"Sanity: {currentSanity:F1}/{MaxSanity}  RegenMult: {RegenMultiplier:F2}");
                        Log.Message($"Value: {Value:F1} / {Max:F1}  |  TotalPawnsEaten: {totalPawnsEaten}");
                        Log.Message($"LastKnownMax: {lastKnownMax:F1}");
                        Log.Message($"progressionExt.bloodPoolBonusPerPawnEaten: {progressionExt?.bloodPoolBonusPerPawnEaten}");
                        Log.Message($"Bonus from pawns eaten: {totalPawnsEaten * (progressionExt?.bloodPoolBonusPerPawnEaten ?? 0f):F1}");
                    }
                };

                yield return new Command_Action
                {
                    defaultLabel = "DEV: Diagnose Max",
                    defaultDesc = "Log full Max calculation to console",
                    action = () =>
                    {
                        Log.Message($"=== Max Diagnosis: {pawn.LabelShort} ===");
                        Log.Message($"Def: {Def?.defName ?? "NULL"}");
                        Log.Message($"Def.maxStat: {Def?.maxStat?.defName ?? "NULL"}");
                        float statVal = Def?.maxStat != null ? pawn.GetStatValue(Def.maxStat, true) : -1f;
                        Log.Message($"GetStatValue(maxStat): {statVal:F1}");
                        Log.Message($"progressionExt: {(progressionExt == null ? "NULL" : "OK")}");
                        Log.Message($"bloodPoolBonusPerPawnEaten: {progressionExt?.bloodPoolBonusPerPawnEaten ?? 0f}");
                        Log.Message($"totalPawnsEaten: {totalPawnsEaten}");
                        Log.Message($"Pawn-eaten bonus: {totalPawnsEaten * (progressionExt?.bloodPoolBonusPerPawnEaten ?? 0f):F1}");
                        Log.Message($"lastKnownMax: {lastKnownMax:F1}");
                        Log.Message($"Max (full calculation): {Max:F1}");
                        Log.Message($"base.Max: {base.Max:F1}");
                        Log.Message($"Value: {Value:F1}");
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
            Scribe_Values.Look(ref currentRank, "currentRank", DemonRank.WeakDemon);
            Scribe_Values.Look(ref totalPawnsEaten, "totalPawnsEaten", 0);
            Scribe_Values.Look(ref lastKnownMax, "lastKnownMax", -1f);
            Scribe_Values.Look(ref hasSpecialized, "hasSpecialized", false);
            Scribe_Values.Look(ref currentSanity, "currentSanity", 100f);
            Scribe_Values.Look(ref ticksSinceLastMeal, "ticksSinceLastMeal", 0);
            Scribe_Values.Look(ref lastSanityWarning, "lastSanityWarning", 0);
            Scribe_Values.Look(ref ownRegenCounter, "ownRegenCounter", 0);

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
                    unlockedAbilityIndices = new HashSet<int>(unlockedList);
            }

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (unlockedAbilityIndices == null)
                    unlockedAbilityIndices = new HashSet<int>();

                if (sanityExt == null)
                    sanityExt = def?.GetModExtension<DemonSanityExtension>();

                if (progressionExt == null)
                    progressionExt = def?.GetModExtension<DemonProgressionExtension>();

                if (lastKnownMax > 0f)
                    this.SetMax(lastKnownMax);
                float _ = Max;

                IsRegenEnabled = false;
            }
        }
    }
}