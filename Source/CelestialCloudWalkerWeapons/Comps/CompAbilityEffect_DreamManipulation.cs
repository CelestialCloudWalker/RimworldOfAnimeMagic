using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace AnimeArsenal
{
    public class CompAbilityEffect_DreamManipulation : CompAbilityEffect
    {
        public new CompProperties_AbilityEffect_DreamManipulation Props => (CompProperties_AbilityEffect_DreamManipulation)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn targetPawn = target.Pawn;
            if (targetPawn == null || targetPawn.Dead || !IsTargetAsleep(targetPawn))
                return;

            ApplyDreamEffect(targetPawn);
        }

        private bool IsTargetAsleep(Pawn pawn)
        {
            if (pawn.CurJob?.def == JobDefOf.LayDown)
                return true;

            if (pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.Anesthetic) != null)
                return true;

            if (pawn.Downed && pawn.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness) < 0.3f)
                return true;

            if (pawn.needs?.rest != null && pawn.needs.rest.CurLevel < 0.1f)
                return true;

            return false;
        }

        private void ApplyDreamEffect(Pawn pawn)
        {
            switch (Props.dreamEffectType)
            {
                case DreamEffectType.Nightmare:
                    ApplyNightmare(pawn);
                    break;
                case DreamEffectType.PleasantDream:
                    ApplyPleasantDream(pawn);
                    break;
                case DreamEffectType.MemoryExtraction:
                    ApplyMemoryExtraction(pawn);
                    break;
                case DreamEffectType.MemoryImplantation:
                    ApplyMemoryImplantation(pawn);
                    break;
                case DreamEffectType.MoodManipulation:
                    ApplyMoodManipulation(pawn);
                    break;
                case DreamEffectType.SkillTransfer:
                    ApplySkillTransfer(pawn);
                    break;
            }

            AddEffects(pawn);

            if (Props.showMessage)
            {
                string effectName = Props.dreamEffectType.ToString().Replace("_", " ");
                Messages.Message($"{pawn.LabelShort} is experiencing {effectName.ToLower()}!",
                    pawn, MessageTypeDefOf.NeutralEvent);
            }
        }

        private void ApplyNightmare(Pawn pawn)
        {
            if (pawn.needs?.mood?.thoughts?.memories != null && Props.nightmareThought != null)
            {
                Thought_Memory thought = (Thought_Memory)ThoughtMaker.MakeThought(Props.nightmareThought);
                pawn.needs.mood.thoughts.memories.TryGainMemory(thought);
            }

            if (Props.nightmareHediff != null)
            {
                Hediff hediff = HediffMaker.MakeHediff(Props.nightmareHediff, pawn);
                hediff.Severity = Props.effectSeverity;
                pawn.health.AddHediff(hediff);
            }

            if (pawn.needs?.rest != null)
            {
                pawn.needs.rest.CurLevel = Math.Max(0, pawn.needs.rest.CurLevel - Props.restPenalty);
            }
        }

        private void ApplyPleasantDream(Pawn pawn)
        {
            if (pawn.needs?.mood?.thoughts?.memories != null && Props.pleasantDreamThought != null)
            {
                Thought_Memory thought = (Thought_Memory)ThoughtMaker.MakeThought(Props.pleasantDreamThought);
                pawn.needs.mood.thoughts.memories.TryGainMemory(thought);
            }

            if (Props.pleasantDreamHediff != null)
            {
                Hediff hediff = HediffMaker.MakeHediff(Props.pleasantDreamHediff, pawn);
                hediff.Severity = Props.effectSeverity;
                pawn.health.AddHediff(hediff);
            }

            if (pawn.needs?.rest != null)
            {
                pawn.needs.rest.CurLevel = Math.Min(1f, pawn.needs.rest.CurLevel + Props.restBonus);
            }
        }

        private void ApplyMemoryExtraction(Pawn pawn)
        {
            var info = new List<string>();

            if (pawn.Faction != null)
                info.Add($"Faction: {pawn.Faction.Name}");

            if (pawn.skills != null)
            {
                var topSkill = pawn.skills.skills.OrderByDescending(s => s.Level).FirstOrDefault();
                if (topSkill != null)
                    info.Add($"Best skill: {topSkill.def.LabelCap} ({topSkill.Level})");
            }

            if (pawn.story?.Childhood != null)
                info.Add($"Childhood: {pawn.story.Childhood.title}");

            if (pawn.story?.traits != null && pawn.story.traits.allTraits.Any())
            {
                var trait = pawn.story.traits.allTraits.First();
                info.Add($"Trait: {trait.LabelCap}");
            }

            if (info.Any())
            {
                string extractedInfo = string.Join(", ", info);
                Messages.Message($"Memory extracted from {pawn.LabelShort}: {extractedInfo}",
                    pawn, MessageTypeDefOf.PositiveEvent);
            }

            if (Props.memoryExtractionHediff != null)
            {
                var existing = pawn.health.hediffSet.GetFirstHediffOfDef(Props.memoryExtractionHediff);
                if (existing != null)
                    pawn.health.RemoveHediff(existing);

                Hediff hediff = HediffMaker.MakeHediff(Props.memoryExtractionHediff, pawn);
                hediff.Severity = Props.effectSeverity;
                pawn.health.AddHediff(hediff);
            }
        }

        private void ApplyMemoryImplantation(Pawn pawn)
        {
            int implanted = 0;

            if (pawn.needs?.mood?.thoughts?.memories != null && Props.implantedMemoryThoughts != null)
            {
                foreach (var thoughtDef in Props.implantedMemoryThoughts)
                {
                    if (thoughtDef != null)
                    {
                        Thought_Memory thought = (Thought_Memory)ThoughtMaker.MakeThought(thoughtDef);
                        pawn.needs.mood.thoughts.memories.TryGainMemory(thought);
                        implanted++;
                    }
                }
            }

            if (implanted > 0)
            {
                Messages.Message($"Implanted {implanted} false memories into {pawn.LabelShort}",
                    pawn, MessageTypeDefOf.NeutralEvent);
            }

            if (Props.memoryImplantHediff != null)
            {
                var existing = pawn.health.hediffSet.GetFirstHediffOfDef(Props.memoryImplantHediff);
                if (existing != null)
                    pawn.health.RemoveHediff(existing);

                Hediff hediff = HediffMaker.MakeHediff(Props.memoryImplantHediff, pawn);
                hediff.Severity = Props.effectSeverity;
                pawn.health.AddHediff(hediff);
            }
        }

        private void ApplyMoodManipulation(Pawn pawn)
        {
            if (pawn.needs?.mood?.thoughts?.memories == null)
                return;

            bool positive = Props.moodChange >= 0;
            ThoughtDef thoughtToUse = positive ? Props.positiveMoodThought : Props.negativeMoodThought;

            if (thoughtToUse != null)
            {
                Thought_Memory thought = (Thought_Memory)ThoughtMaker.MakeThought(thoughtToUse);
                pawn.needs.mood.thoughts.memories.TryGainMemory(thought);

                string moodType = positive ? "positive" : "negative";
                Messages.Message($"Manipulated {pawn.LabelShort}'s mood ({moodType})",
                    pawn, positive ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.NegativeEvent);
            }
        }

        private void ApplySkillTransfer(Pawn pawn)
        {
            if (pawn.skills == null) return;

            Pawn caster = parent.pawn;
            if (caster?.skills == null) return;

            var availableSkills = pawn.skills.skills.Where(s => s.Level > 0 && !s.TotallyDisabled).ToList();
            if (!availableSkills.Any()) return;

            var randomSkill = availableSkills.RandomElement();
            int transferAmount = Rand.RangeInclusive(1, 3);

            var targetSkill = pawn.skills.GetSkill(randomSkill.def);
            var casterSkill = caster.skills.GetSkill(randomSkill.def);

            if (Props.transferFromTarget)
            {
                if (casterSkill.TotallyDisabled) return;

                transferAmount = Math.Min(transferAmount, targetSkill.Level);
                targetSkill.Level = Math.Max(0, targetSkill.Level - transferAmount);
                casterSkill.Level = Math.Min(20, casterSkill.Level + transferAmount);

                Messages.Message($"Drained {transferAmount} {randomSkill.def.LabelCap} from {pawn.LabelShort}",
                    caster, MessageTypeDefOf.PositiveEvent);
            }
            else
            {
                if (casterSkill.Level <= 0) return;

                transferAmount = Math.Min(transferAmount, casterSkill.Level);
                casterSkill.Level = Math.Max(0, casterSkill.Level - transferAmount);
                targetSkill.Level = Math.Min(20, targetSkill.Level + transferAmount);

                Messages.Message($"Transferred {transferAmount} {randomSkill.def.LabelCap} to {pawn.LabelShort}",
                    caster, MessageTypeDefOf.PositiveEvent);
            }
        }

        private void AddEffects(Pawn pawn)
        {
            Props.effectSound?.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));

            if (Props.dreamEffecter != null)
            {
                Effecter effecter = Props.dreamEffecter.Spawn();
                effecter.Trigger(new TargetInfo(pawn.Position, pawn.Map), TargetInfo.Invalid);
                effecter.Cleanup();
            }
        }

        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest)
        {
            return base.CanApplyOn(target, dest) &&
                   target.HasThing &&
                   target.Thing is Pawn pawn &&
                   IsTargetAsleep(pawn);
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Pawn pawn = target.Pawn;
            if (pawn == null)
            {
                if (throwMessages)
                    Messages.Message("Must target a pawn", MessageTypeDefOf.RejectInput);
                return false;
            }

            if (!IsTargetAsleep(pawn))
            {
                if (throwMessages)
                    Messages.Message($"{pawn.LabelShort} must be asleep", MessageTypeDefOf.RejectInput);
                return false;
            }

            return base.Valid(target, throwMessages);
        }
    }

    public enum DreamEffectType
    {
        Nightmare,
        PleasantDream,
        MemoryExtraction,
        MemoryImplantation,
        MoodManipulation,
        SkillTransfer
    }

    public class CompProperties_AbilityEffect_DreamManipulation : CompProperties_AbilityEffect
    {
        public DreamEffectType dreamEffectType = DreamEffectType.Nightmare;
        public float effectSeverity = 1.0f;
        public bool showMessage = true;

        public HediffDef nightmareHediff = null;
        public HediffDef pleasantDreamHediff = null;
        public ThoughtDef nightmareThought = null;
        public ThoughtDef pleasantDreamThought = null;
        public float restPenalty = 0.3f;
        public float restBonus = 0.3f;

        public HediffDef memoryExtractionHediff = null;
        public HediffDef memoryImplantHediff = null;
        public List<ThoughtDef> implantedMemoryThoughts = null;

        public float moodChange = 0f;
        public ThoughtDef positiveMoodThought = null;
        public ThoughtDef negativeMoodThought = null;

        public SkillDef skillToTransfer = null;
        public int skillTransferAmount = 1;
        public bool transferFromTarget = true;

        public SoundDef effectSound = null;
        public EffecterDef dreamEffecter = null;

        public CompProperties_AbilityEffect_DreamManipulation()
        {
            compClass = typeof(CompAbilityEffect_DreamManipulation);
        }
    }
}