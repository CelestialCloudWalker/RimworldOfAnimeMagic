using RimWorld;
using RimWorld.QuestGen;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace AnimeArsenal
{
    public class QuestNode_GetFlexibleRewards : QuestNode
    {
        public List<FlexibleRewardOption> flexibleRewards;
        public SurvivalSettings survivalSettings;
        public string customSurvivalMessage;
        public string customDeathMessage;
        public string customRewardMessage;

        protected override bool TestRunInt(Slate slate)
        {
            return true;
        }

        protected override void RunInt()
        {
            QuestGen.quest.AddPart(new QuestPart_GetFlexibleRewards
            {
                inSignalEnable = QuestGenUtility.HardcodedSignalWithQuestID("ColonistsReturned"),
                flexibleRewards = flexibleRewards,
                survivalSettings = survivalSettings,
                customSurvivalMessage = customSurvivalMessage,
                customDeathMessage = customDeathMessage,
                customRewardMessage = customRewardMessage
            });
        }
    }

    public class QuestPart_GetFlexibleRewards : QuestPart
    {
        public string inSignalEnable;
        public List<FlexibleRewardOption> flexibleRewards;
        public SurvivalSettings survivalSettings;
        public string customSurvivalMessage;
        public string customDeathMessage;
        public string customRewardMessage;

        public override void Notify_QuestSignalReceived(Signal signal)
        {
            base.Notify_QuestSignalReceived(signal);
            if (signal.tag == inSignalEnable && quest?.PartsListForReading != null)
            {
                var lendPart = quest.PartsListForReading.OfType<QuestPart_LendColonistsToFaction>().FirstOrDefault();
                if (lendPart?.LentColonistsListForReading == null) return;

                List<Pawn> survivors = new List<Pawn>();
                List<Pawn> casualties = new List<Pawn>();
                var originalPawns = lendPart.LentColonistsListForReading.ToList();

                foreach (Pawn pawn in originalPawns)
                {
                    if (SurvivesTrialRoll(pawn))
                    {
                        survivors.Add(pawn);
                        TryGiveFlexibleReward(pawn);
                    }
                    else
                    {
                        casualties.Add(pawn);
                    }
                }

                foreach (Pawn casualty in casualties)
                {
                    HandleCasualtyDestruction(casualty);
                    string deathMessage = !string.IsNullOrEmpty(customDeathMessage)
                        ? customDeathMessage.Replace("{PAWN}", casualty.LabelShort)
                        : $"{casualty.LabelShort} did not survive the trial.";
                    Messages.Message(deathMessage, MessageTypeDefOf.PawnDeath, false);
                }

                lendPart.LentColonistsListForReading.Clear();
                if (survivors.Any())
                {
                    lendPart.LentColonistsListForReading.AddRange(survivors);
                    string survivorNames = survivors.Count == 1 ? survivors[0].LabelShort :
                                         string.Join(", ", survivors.Take(survivors.Count - 1).Select(p => p.LabelShort)) +
                                         " and " + survivors.Last().LabelShort;

                    string survivalMessage = !string.IsNullOrEmpty(customSurvivalMessage)
                        ? customSurvivalMessage.Replace("{PAWNS}", survivorNames)
                        : $"{survivorNames} survived the trial!";
                    Messages.Message(survivalMessage, MessageTypeDefOf.PositiveEvent, false);
                }
                else
                {
                    Messages.Message("No one survived the trial.", MessageTypeDefOf.ThreatBig, false);
                }
            }
        }

        private void HandleCasualtyDestruction(Pawn casualty)
        {
            if (casualty == null || casualty.Destroyed) return;

            if (Find.WorldPawns.Contains(casualty))
                Find.WorldPawns.RemovePawn(casualty);

            if (!casualty.Dead)
                casualty.Kill(new DamageInfo(DamageDefOf.Deterioration, 9999f, 999f, -1f, null, null, null, DamageInfo.SourceCategory.ThingOrUnknown));

            if (casualty.Corpse != null && !casualty.Corpse.Destroyed)
            {
                if (casualty.Corpse.Spawned)
                    casualty.Corpse.DeSpawn(DestroyMode.Vanish);
                casualty.Corpse.Destroy(DestroyMode.Vanish);
            }

            casualty.SetFaction(null);

            if (Find.ColonistBar.GetColonistsInOrder().Contains(casualty))
                Find.ColonistBar.MarkColonistsDirty();
        }

        private bool SurvivesTrialRoll(Pawn pawn)
        {
            if (survivalSettings == null) return Rand.Chance(0.7f);

            float survivalChance = survivalSettings.baseSurvivalChance;

            if (survivalSettings.skillBonuses != null)
            {
                foreach (var skillBonus in survivalSettings.skillBonuses)
                {
                    if (pawn.skills.GetSkill(skillBonus.skillDef).Level >= skillBonus.minimumLevel)
                        survivalChance += skillBonus.bonus;
                }
            }

            if (survivalSettings.traitModifiers != null)
            {
                foreach (var traitModifier in survivalSettings.traitModifiers)
                {
                    if (pawn.story.traits.HasTrait(traitModifier.traitDef))
                        survivalChance += traitModifier.modifier;
                }
            }

            survivalChance = Mathf.Clamp(survivalChance, survivalSettings.minimumSurvivalChance, survivalSettings.maximumSurvivalChance);
            return Rand.Chance(survivalChance);
        }

        private void TryGiveFlexibleReward(Pawn pawn)
        {
            if (flexibleRewards == null || !flexibleRewards.Any()) return;

            var availableRewards = GetAvailableFlexibleRewards(pawn);
            if (!availableRewards.Any())
            {
                Messages.Message($"{pawn.LabelShort} already has all available rewards!", MessageTypeDefOf.NeutralEvent);
                return;
            }

            float totalWeight = availableRewards.Sum(r => r.weight);
            float roll = Rand.Range(0f, totalWeight);
            float currentWeight = 0f;

            foreach (var rewardOption in availableRewards)
            {
                currentWeight += rewardOption.weight;
                if (roll <= currentWeight)
                {
                    GiveRewardToPawn(pawn, rewardOption);
                    break;
                }
            }
        }

        private List<FlexibleRewardOption> GetAvailableFlexibleRewards(Pawn pawn)
        {
            var available = new List<FlexibleRewardOption>();

            foreach (var reward in flexibleRewards)
            {
                bool canGive = true;

                if (reward.rewardType == FlexibleRewardType.Gene && reward.geneDef != null)
                {
                    if (pawn.genes != null && pawn.genes.HasActiveGene(reward.geneDef))
                        canGive = false;
                }

                if (reward.rewardType == FlexibleRewardType.Trait && reward.traitDef != null)
                {
                    if (pawn.story?.traits != null && pawn.story.traits.HasTrait(reward.traitDef))
                        canGive = false;
                }

                if (canGive && reward.requirements != null && reward.requirements.Any())
                {
                    foreach (var requirement in reward.requirements)
                    {
                        if (!MeetsFlexibleRequirement(pawn, requirement))
                        {
                            canGive = false;
                            break;
                        }
                    }
                }

                if (canGive) available.Add(reward);
            }

            return available;
        }

        private bool MeetsFlexibleRequirement(Pawn pawn, FlexibleRewardRequirement requirement)
        {
            switch (requirement.requirementType)
            {
                case FlexibleRequirementType.HasGene:
                    return pawn.genes != null && pawn.genes.HasActiveGene(requirement.geneDef);
                case FlexibleRequirementType.HasTrait:
                    return pawn.story?.traits != null && pawn.story.traits.HasTrait(requirement.traitDef);
                case FlexibleRequirementType.SkillLevel:
                    return pawn.skills.GetSkill(requirement.skillDef).Level >= requirement.minimumValue;
                case FlexibleRequirementType.DoesNotHaveGene:
                    return pawn.genes == null || !pawn.genes.HasActiveGene(requirement.geneDef);
                case FlexibleRequirementType.DoesNotHaveTrait:
                    return pawn.story?.traits == null || !pawn.story.traits.HasTrait(requirement.traitDef);
                default:
                    return true;
            }
        }

        private void GiveRewardToPawn(Pawn pawn, FlexibleRewardOption reward)
        {
            string rewardMessage = "";

            switch (reward.rewardType)
            {
                case FlexibleRewardType.Gene:
                    if (pawn.genes != null && reward.geneDef != null)
                    {
                        pawn.genes.AddGene(reward.geneDef, false);
                        rewardMessage = !string.IsNullOrEmpty(customRewardMessage)
                            ? customRewardMessage.Replace("{PAWN}", pawn.LabelShort).Replace("{REWARD}", reward.geneDef.label)
                            : $"{pawn.LabelShort} gained the {reward.geneDef.label} gene!";
                    }
                    break;
                case FlexibleRewardType.Trait:
                    if (pawn.story?.traits != null && reward.traitDef != null)
                    {
                        int degreeToUse = reward.traitDegree;
                        if (reward.traitDef.degreeDatas != null && reward.traitDef.degreeDatas.Any())
                        {
                            var availableDegrees = reward.traitDef.degreeDatas.Select(d => d.degree).ToList();
                            if (!availableDegrees.Contains(degreeToUse))
                                degreeToUse = availableDegrees.First();
                        }

                        pawn.story.traits.GainTrait(new Trait(reward.traitDef, degreeToUse));
                        rewardMessage = !string.IsNullOrEmpty(customRewardMessage)
                            ? customRewardMessage.Replace("{PAWN}", pawn.LabelShort).Replace("{REWARD}", reward.traitDef.label)
                            : $"{pawn.LabelShort} gained the {reward.traitDef.label} trait!";
                    }
                    break;
            }

            if (!string.IsNullOrEmpty(rewardMessage))
                Messages.Message(rewardMessage, MessageTypeDefOf.PositiveEvent);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref inSignalEnable, "inSignalEnable");
            Scribe_Collections.Look(ref flexibleRewards, "flexibleRewards", LookMode.Deep);
            Scribe_Deep.Look(ref survivalSettings, "survivalSettings");
            Scribe_Values.Look(ref customSurvivalMessage, "customSurvivalMessage");
            Scribe_Values.Look(ref customDeathMessage, "customDeathMessage");
            Scribe_Values.Look(ref customRewardMessage, "customRewardMessage");
        }
    }

    public enum FlexibleRewardType
    {
        Gene,
        Trait
    }

    public enum FlexibleRequirementType
    {
        HasGene,
        HasTrait,
        SkillLevel,
        DoesNotHaveGene,
        DoesNotHaveTrait
    }

    public class FlexibleRewardOption : IExposable
    {
        public FlexibleRewardType rewardType;
        public GeneDef geneDef;
        public TraitDef traitDef;
        public int traitDegree = 0;
        public float weight = 1f;
        public List<FlexibleRewardRequirement> requirements;

        public void ExposeData()
        {
            Scribe_Values.Look(ref rewardType, "rewardType");
            Scribe_Defs.Look(ref geneDef, "geneDef");
            Scribe_Defs.Look(ref traitDef, "traitDef");
            Scribe_Values.Look(ref traitDegree, "traitDegree", 0);
            Scribe_Values.Look(ref weight, "weight", 1f);
            Scribe_Collections.Look(ref requirements, "requirements", LookMode.Deep);
        }
    }

    public class FlexibleRewardRequirement : IExposable
    {
        public FlexibleRequirementType requirementType;
        public GeneDef geneDef;
        public TraitDef traitDef;
        public SkillDef skillDef;
        public int minimumValue;

        public void ExposeData()
        {
            Scribe_Values.Look(ref requirementType, "requirementType");
            Scribe_Defs.Look(ref geneDef, "geneDef");
            Scribe_Defs.Look(ref traitDef, "traitDef");
            Scribe_Defs.Look(ref skillDef, "skillDef");
            Scribe_Values.Look(ref minimumValue, "minimumValue");
        }
    }
}