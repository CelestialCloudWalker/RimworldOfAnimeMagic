using RimWorld;
using RimWorld.QuestGen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace AnimeArsenal
{
    public class QuestNode_GetGene : QuestNode
    {
        public List<GeneOption> genes;
        public SurvivalSettings survivalSettings;

        protected override bool TestRunInt(Slate slate)
        {
            if (genes == null || !genes.Any())
            {
                Log.Warning("QuestNode_GetGene: genes list is null or empty");
                return false;
            }

            if (survivalSettings == null)
            {
                Log.Warning("QuestNode_GetGene: survivalSettings is null");
                return false;
            }

            return true;
        }

        protected override void RunInt()
        {
            Slate slate = QuestGen.slate;
            QuestGen.quest.AddPart(new QuestPart_GetGene
            {
                inSignalEnable = QuestGenUtility.HardcodedSignalWithQuestID("ColonistsReturned"),
                genes = genes,
                survivalSettings = survivalSettings
            });
        }
    }

    public class QuestPart_GetGene : QuestPart
    {
        public string inSignalEnable;
        public List<GeneOption> genes;
        public SurvivalSettings survivalSettings;

        public override void Notify_QuestSignalReceived(Signal signal)
        {
            base.Notify_QuestSignalReceived(signal);
            if (signal.tag == inSignalEnable && quest?.PartsListForReading != null)
            {
                var lendPart = quest.PartsListForReading.OfType<QuestPart_LendColonistsToFaction>().FirstOrDefault();
                if (lendPart?.LentColonistsListForReading != null)
                {
                    List<Pawn> survivors = new List<Pawn>();
                    List<Pawn> casualties = new List<Pawn>();

                    var originalPawns = lendPart.LentColonistsListForReading.ToList();

                    foreach (Pawn pawn in originalPawns)
                    {
                        if (SurvivesTrialRoll(pawn))
                        {
                            survivors.Add(pawn);
                            if (pawn.genes != null)
                            {
                                TryGiveRandomGene(pawn);
                            }
                        }
                        else
                        {
                            casualties.Add(pawn);
                        }
                    }

                    foreach (Pawn casualty in casualties)
                    {
                        HandleCasualtyDestruction(casualty);

                        Messages.Message($"{casualty.LabelShort} did not survive the Final Selection and was consumed by demons on Mount Sagiri.",
                                       MessageTypeDefOf.PawnDeath, false);
                    }

                    lendPart.LentColonistsListForReading.Clear();
                    if (survivors.Any())
                    {
                        lendPart.LentColonistsListForReading.AddRange(survivors);

                        string survivorNames = survivors.Count == 1 ? survivors[0].LabelShort :
                                             string.Join(", ", survivors.Take(survivors.Count - 1).Select(p => p.LabelShort)) +
                                             " and " + survivors.Last().LabelShort;
                        Messages.Message($"{survivorNames} survived the Final Selection and begins their breathing training!",
                                       MessageTypeDefOf.PositiveEvent, false);
                    }
                    else
                    {
                        Messages.Message("No one survived the Final Selection. The demons have claimed them all.",
                                       MessageTypeDefOf.ThreatBig, false);

                    }
                }
            }
        }

        private void HandleCasualtyDestruction(Pawn casualty)
        {
            try
            {
                if (casualty == null || casualty.Destroyed)
                {
                    return;
                }

                if (Find.WorldPawns.Contains(casualty))
                {
                    Find.WorldPawns.RemovePawn(casualty);
                }

                if (!casualty.Dead)
                {
                    casualty.Kill(new DamageInfo(DamageDefOf.Deterioration, 9999f, 999f, -1f, null, null, null, DamageInfo.SourceCategory.ThingOrUnknown));
                }

                if (casualty.Corpse != null && !casualty.Corpse.Destroyed)
                {
                    if (casualty.Corpse.Spawned)
                    {
                        casualty.Corpse.DeSpawn(DestroyMode.Vanish);
                    }

                    casualty.Corpse.Destroy(DestroyMode.Vanish);
                }

                if (casualty.Faction != null)
                {
                    casualty.SetFaction(null);
                }

                if (Find.ColonistBar.GetColonistsInOrder().Contains(casualty))
                {
                    Find.ColonistBar.MarkColonistsDirty();
                }

                Log.Message($"Successfully processed casualty: {casualty.LabelShort}");
            }
            catch (Exception ex)
            {
                Log.Error($"Error processing casualty {casualty?.LabelShort}: {ex}");
            }
        }

        private bool SurvivesTrialRoll(Pawn pawn)
        {
            if (survivalSettings == null)
            {
                return Rand.Chance(0.7f);
            }

            float survivalChance = survivalSettings.baseSurvivalChance;

            if (survivalSettings.skillBonuses != null)
            {
                foreach (var skillBonus in survivalSettings.skillBonuses)
                {
                    if (pawn.skills.GetSkill(skillBonus.skillDef).Level >= skillBonus.minimumLevel)
                    {
                        survivalChance += skillBonus.bonus;
                    }
                }
            }

            if (survivalSettings.traitModifiers != null)
            {
                foreach (var traitModifier in survivalSettings.traitModifiers)
                {
                    if (pawn.story.traits.HasTrait(traitModifier.traitDef))
                    {
                        survivalChance += traitModifier.modifier;
                    }
                }
            }

            survivalChance = Mathf.Clamp(survivalChance, survivalSettings.minimumSurvivalChance, survivalSettings.maximumSurvivalChance);

            Log.Message($"Final Selection survival roll for {pawn.LabelShort}: {survivalChance * 100f:F1}% chance");

            return Rand.Chance(survivalChance);
        }

        private void TryGiveRandomGene(Pawn pawn)
        {
            if (genes == null || !genes.Any())
                return;

            var availableGenes = genes.Where(g => !pawn.genes.HasActiveGene(g.geneDef)).ToList();
            if (!availableGenes.Any())
            {
                Messages.Message($"{pawn.LabelShort} already has all available genes!", MessageTypeDefOf.NeutralEvent);
                return;
            }

            float totalWeight = availableGenes.Sum(g => g.weight);
            float roll = Rand.Range(0f, totalWeight);
            float currentWeight = 0f;

            foreach (var geneOption in availableGenes)
            {
                currentWeight += geneOption.weight;
                if (roll <= currentWeight)
                {
                    pawn.genes.AddGene(geneOption.geneDef, false);
                    Messages.Message($"{pawn.LabelShort} gained the {geneOption.geneDef.label} gene from surviving the Final Selection!",
                                   MessageTypeDefOf.PositiveEvent);
                    break;
                }
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref inSignalEnable, "inSignalEnable");
            Scribe_Collections.Look(ref genes, "genes", LookMode.Deep);
            Scribe_Deep.Look(ref survivalSettings, "survivalSettings");
        }
    }

    public class GeneOption : IExposable
    {
        public GeneDef geneDef;
        public float weight = 1f;

        public void ExposeData()
        {
            Scribe_Defs.Look(ref geneDef, "geneDef");
            Scribe_Values.Look(ref weight, "weight", 1f);
        }
    }

    public class SurvivalSettings : IExposable
    {
        public float baseSurvivalChance = 0.7f;
        public float minimumSurvivalChance = 0.05f;
        public float maximumSurvivalChance = 0.95f;
        public List<SkillBonus> skillBonuses;
        public List<TraitModifier> traitModifiers;

        public void ExposeData()
        {
            Scribe_Values.Look(ref baseSurvivalChance, "baseSurvivalChance", 0.7f);
            Scribe_Values.Look(ref minimumSurvivalChance, "minimumSurvivalChance", 0.05f);
            Scribe_Values.Look(ref maximumSurvivalChance, "maximumSurvivalChance", 0.95f);
            Scribe_Collections.Look(ref skillBonuses, "skillBonuses", LookMode.Deep);
            Scribe_Collections.Look(ref traitModifiers, "traitModifiers", LookMode.Deep);
        }
    }

    public class SkillBonus : IExposable
    {
        public SkillDef skillDef;
        public int minimumLevel;
        public float bonus;

        public void ExposeData()
        {
            Scribe_Defs.Look(ref skillDef, "skillDef");
            Scribe_Values.Look(ref minimumLevel, "minimumLevel");
            Scribe_Values.Look(ref bonus, "bonus");
        }
    }

    public class TraitModifier : IExposable
    {
        public TraitDef traitDef;
        public float modifier;

        public void ExposeData()
        {
            Scribe_Defs.Look(ref traitDef, "traitDef");
            Scribe_Values.Look(ref modifier, "modifier");
        }
    }
}