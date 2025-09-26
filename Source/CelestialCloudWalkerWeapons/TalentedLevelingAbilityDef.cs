using EMF;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Talented;
using UnityEngine;
using Verse;

namespace AnimeArsenal
{
    public class TalentedLevelingAbilityDef : AbilityDef
    {
        public List<AbilityLevelData> levels = new List<AbilityLevelData>();
        public float baseExperienceGain = 1f;
        public bool showExperienceInTooltip = true;


        public string requiredGeneDefName;
        public float resourceCost = 1f;

        public TalentedLevelingAbilityDef()
        {
            abilityClass = typeof(TalentedLevelingAbility);
        }

        public AbilityLevelData GetLevelData(int level)
        {
            if (levels.NullOrEmpty() || level < 0)
                return null;

            int clampedLevel = Mathf.Min(level, levels.Count - 1);
            return levels[clampedLevel];
        }

        public int GetMaxLevel() => levels?.Count ?? 1;
    }

    public class TalentedLevelingAbility : Ability, IExposable
    {
        private float experience = 0f;
        private int currentLevel = 0;
        private List<AbilityComp> levelComps;

        private Gene_TalentBase talentGene;

        public TalentedLevelingAbilityDef TalentedLevelingDef => (TalentedLevelingAbilityDef)def;
        public float Experience => experience;
        public int CurrentLevel => currentLevel;
        public int MaxLevel => TalentedLevelingDef.GetMaxLevel();

        public TalentedLevelingAbility() : base() { }
        public TalentedLevelingAbility(Pawn pawn) : base(pawn) { }
        public TalentedLevelingAbility(Pawn pawn, Precept sourcePrecept) : base(pawn, sourcePrecept) { }
        public TalentedLevelingAbility(Pawn pawn, AbilityDef def) : base(pawn, def) { }
        public TalentedLevelingAbility(Pawn pawn, Precept sourcePrecept, AbilityDef def) : base(pawn, sourcePrecept, def) { }

        private bool initialized = false;

        private void EnsureInitialized()
        {
            if (!initialized && pawn != null)
            {
                FindTalentGene();
                InitializeLevelComps();
                initialized = true;
            }
        }

        private void FindTalentGene()
        {
            if (pawn?.genes?.GenesListForReading == null) return;

            foreach (var gene in pawn.genes.GenesListForReading)
            {
                if (gene is Gene_TalentBase talentBase &&
                    gene.def.defName == TalentedLevelingDef.requiredGeneDefName)
                {
                    talentGene = talentBase;
                    break;
                }
            }
        }

        private void InitializeLevelComps()
        {
            if (currentLevel == 0)
            {
                levelComps = null;
                return;
            }

            var levelData = TalentedLevelingDef.GetLevelData(currentLevel);
            if (levelData?.levelComps != null && levelData.levelComps.Any())
            {
                levelComps = new List<AbilityComp>();
                if (levelData.cumulativeEffects && base.comps != null)
                {
                    levelComps.AddRange(base.comps);
                }
                foreach (var compProps in levelData.levelComps)
                {
                    try
                    {
                        var comp = (AbilityComp)System.Activator.CreateInstance(compProps.compClass);
                        comp.parent = this;
                        comp.Initialize(compProps);
                        levelComps.Add(comp);
                    }
                    catch (System.Exception e)
                    {
                        Log.Error($"Failed to create level comp: {e}");
                    }
                }
            }
            else
            {
                levelComps = null;
            }
        }

        public override string Tooltip
        {
            get
            {
                string baseTooltip = base.Tooltip;
                var levelData = TalentedLevelingDef.GetLevelData(currentLevel);
                if (levelData != null && !levelData.labelSuffix.NullOrEmpty())
                {
                    string originalLabel = def.LabelCap;
                    string newLabel = originalLabel + levelData.labelSuffix;
                    if (baseTooltip.StartsWith(originalLabel))
                    {
                        baseTooltip = newLabel + baseTooltip.Substring(originalLabel.Length);
                    }
                }

                if (TalentedLevelingDef.showExperienceInTooltip)
                {
                    float nextLevelExp = GetExperienceForNextLevel();

                    baseTooltip += $"\n\nLevel: {currentLevel + 1}/{MaxLevel}";

                    if (currentLevel < MaxLevel - 1)
                    {
                        baseTooltip += $"\nExperience: {experience:F0}/{nextLevelExp:F0}";
                        float percent = (experience / nextLevelExp) * 100f;
                        baseTooltip += $" ({percent:F1}%)";
                    }
                    else
                    {
                        baseTooltip += "\nMax Level Reached";
                    }
                }

                return baseTooltip;
            }
        }

        public override AcceptanceReport CanCast
        {
            get
            {
                EnsureInitialized();

                var baseResult = base.CanCast;
                if (!baseResult) return baseResult;

                if (talentGene == null)
                {
                    FindTalentGene();
                    if (talentGene == null)
                        return "Missing required gene";
                }

                // Check resource availability for both gene types
                if (talentGene is BreathingTechniqueGene breathingGene)
                {
                    if (breathingGene.Value < TalentedLevelingDef.resourceCost)
                        return "Not enough breath";
                }
                else if (talentGene is BloodDemonArtsGene bloodGene)
                {
                    if (bloodGene.Value < TalentedLevelingDef.resourceCost)
                        return "Not enough blood";
                }

                var effectiveComps = GetEffectiveComps();
                if (effectiveComps != null)
                {
                    foreach (var comp in effectiveComps)
                    {
                        if (!comp.CanCast)
                            return comp.CanCast;
                    }
                }

                return true;
            }
        }

        private List<AbilityComp> GetEffectiveComps()
        {
            if (currentLevel == 0 || levelComps == null)
            {
                return base.comps;
            }
            else
            {
                return levelComps;
            }
        }

        public List<CompAbilityEffect> GetEffectiveEffectComps()
        {
            var effectiveComps = GetEffectiveComps();
            if (effectiveComps == null)
                return new List<CompAbilityEffect>();

            return effectiveComps.OfType<CompAbilityEffect>().ToList();
        }

        public override bool Activate(LocalTargetInfo target, LocalTargetInfo dest)
        {
            EnsureInitialized();

            var canCast = this.CanCast;
            if (!canCast)
            {
                Log.Warning($"TalentedLevelingAbility - Cannot cast {def.defName}: {canCast.Reason}");
                return false;
            }

            ConsumeResource();

            if (this.def.hostile && this.pawn.mindState != null)
            {
                this.pawn.mindState.lastCombatantTick = Find.TickManager.TicksGame;
            }

            var effectComps = GetEffectiveEffectComps();

            Log.Message($"EffectComps count: {effectComps.Count}, Level: {currentLevel}, LevelComps count: {levelComps?.Count ?? 0}");

            if (effectComps.Any())
            {
                List<LocalTargetInfo> affectedTargets = this.GetAffectedTargets(target).ToList();
                foreach (var comp in effectComps)
                {
                    comp.Apply(target, dest);
                }
            }

            GainExperience(TalentedLevelingDef.baseExperienceGain);
            talentGene?.GainExperience(TalentedLevelingDef.baseExperienceGain);

            return true;
        }

        protected virtual void ConsumeResource()
        {
            if (talentGene is BreathingTechniqueGene breathingGene)
            {
                // For breathing technique, use direct value subtraction (works fine)
                breathingGene.Value -= TalentedLevelingDef.resourceCost;
            }
            else if (talentGene is BloodDemonArtsGene bloodGene)
            {
                // For blood demon, use the specialized consumption method
                bloodGene.ConsumeBlood(TalentedLevelingDef.resourceCost);
            }
        }

        public void GainExperience(float amount)
        {
            if (currentLevel >= MaxLevel - 1)
                return;

            experience += amount;

            float requiredForNext = GetExperienceForNextLevel();
            if (experience >= requiredForNext)
            {
                LevelUp();
            }
        }

        private float GetExperienceForNextLevel()
        {
            if (currentLevel >= MaxLevel - 1)
                return float.MaxValue;

            var nextLevelData = TalentedLevelingDef.GetLevelData(currentLevel + 1);
            return nextLevelData?.experienceRequired ?? 100f;
        }

        private void LevelUp()
        {
            if (currentLevel >= MaxLevel - 1)
                return;

            currentLevel++;
            var newLevelData = TalentedLevelingDef.GetLevelData(currentLevel);

            Messages.Message(
                $"{pawn.Name.ToStringShort}'s {def.LabelCap} reached level {currentLevel + 1}!",
                pawn,
                MessageTypeDefOf.PositiveEvent
            );

            InitializeLevelComps();

            float usedExp = GetExperienceForNextLevel();
            experience = Mathf.Max(0, experience - usedExp);
            if (currentLevel < MaxLevel - 1 && experience >= GetExperienceForNextLevel())
            {
                LevelUp();
            }
        }

        public override IEnumerable<Command> GetGizmos()
        {
            foreach (var item in base.GetGizmos())
            {
                yield return item;
            }

            if (DebugSettings.godMode)
            {
                yield return new Command_Action()
                {
                    defaultLabel = $"Level up {this.def.LabelCap}",
                    defaultDesc = "level up",
                    action = () =>
                    {
                        LevelUp();
                    }
                };
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref experience, "experience", 0f);
            Scribe_Values.Look(ref currentLevel, "currentLevel", 0);

            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {

                if (this.Id == -1)
                {
                    this.Id = Find.UniqueIDsManager.GetNextAbilityID();
                }
                initialized = false;
            }

            if (levelComps != null)
            {
                for (int i = 0; i < levelComps.Count; i++)
                {
                    levelComps[i].PostExposeData();
                }
            }
        }
    }
}