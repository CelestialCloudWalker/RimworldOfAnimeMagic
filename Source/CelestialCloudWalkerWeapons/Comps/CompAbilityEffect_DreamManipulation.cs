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
            if (targetPawn == null || targetPawn.Dead)
                return;

            
            if (!IsTargetAsleep(targetPawn))
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
            try
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
            catch (Exception ex)
            {
                Log.Error($"AnimeArsenal: Error applying dream effect to {pawn?.LabelShort}: {ex}");
            }
        }

        private void ApplyNightmare(Pawn pawn)
        {
            
            if (pawn.needs?.mood?.thoughts?.memories != null && Props.nightmareThought != null)
            {
                try
                {
                    Thought_Memory thought = (Thought_Memory)ThoughtMaker.MakeThought(Props.nightmareThought);
                    if (thought != null)
                    {
                        pawn.needs.mood.thoughts.memories.TryGainMemory(thought);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"AnimeArsenal: Failed to add nightmare thought to {pawn.LabelShort}: {ex.Message}");
                }
            }

            
            if (Props.nightmareHediff != null)
            {
                try
                {
                    Hediff hediff = HediffMaker.MakeHediff(Props.nightmareHediff, pawn);
                    if (hediff != null)
                    {
                        hediff.Severity = Props.effectSeverity;
                        pawn.health.AddHediff(hediff);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"AnimeArsenal: Failed to add nightmare hediff to {pawn.LabelShort}: {ex.Message}");
                }
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
                try
                {
                    Thought_Memory thought = (Thought_Memory)ThoughtMaker.MakeThought(Props.pleasantDreamThought);
                    if (thought != null)
                    {
                        pawn.needs.mood.thoughts.memories.TryGainMemory(thought);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"AnimeArsenal: Failed to add pleasant dream thought to {pawn.LabelShort}: {ex.Message}");
                }
            }

            
            if (Props.pleasantDreamHediff != null)
            {
                try
                {
                    Hediff hediff = HediffMaker.MakeHediff(Props.pleasantDreamHediff, pawn);
                    if (hediff != null)
                    {
                        hediff.Severity = Props.effectSeverity;
                        pawn.health.AddHediff(hediff);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"AnimeArsenal: Failed to add pleasant dream hediff to {pawn.LabelShort}: {ex.Message}");
                }
            }

            
            if (pawn.needs?.rest != null)
            {
                pawn.needs.rest.CurLevel = Math.Min(1f, pawn.needs.rest.CurLevel + Props.restBonus);
            }
        }

        private void ApplyMemoryExtraction(Pawn pawn)
        {
            
            List<string> extractedInfo = new List<string>();

            
            if (pawn.Faction != null)
                extractedInfo.Add($"Faction: {pawn.Faction.Name}");

            
            if (pawn.skills != null)
            {
                var topSkill = pawn.skills.skills.OrderByDescending(s => s.Level).FirstOrDefault();
                if (topSkill != null)
                    extractedInfo.Add($"Best skill: {topSkill.def.LabelCap} ({topSkill.Level})");
            }

            
            if (pawn.story?.Childhood != null)
                extractedInfo.Add($"Childhood: {pawn.story.Childhood.title}");

            
            if (pawn.story?.traits != null && pawn.story.traits.allTraits.Any())
            {
                var trait = pawn.story.traits.allTraits.First();
                extractedInfo.Add($"Trait: {trait.LabelCap}");
            }

            
            if (extractedInfo.Any())
            {
                string info = string.Join(", ", extractedInfo);
                Messages.Message($"Memory extracted from {pawn.LabelShort}'s dreams: {info}",
                    pawn, MessageTypeDefOf.PositiveEvent);

                
                Log.Message($"[Dream Extraction] Successfully extracted: {info}");
            }
            else
            {
                Messages.Message($"Failed to extract meaningful memories from {pawn.LabelShort}'s dreams.",
                    pawn, MessageTypeDefOf.NeutralEvent);
                Log.Warning($"[Dream Extraction] No information could be extracted from {pawn.LabelShort}");
            }

            
            if (Props.memoryExtractionHediff != null)
            {
                try
                {
                    
                    var existing = pawn.health.hediffSet.GetFirstHediffOfDef(Props.memoryExtractionHediff);
                    if (existing != null)
                    {
                        pawn.health.RemoveHediff(existing);
                    }

                    Hediff hediff = HediffMaker.MakeHediff(Props.memoryExtractionHediff, pawn);
                    if (hediff != null)
                    {
                        hediff.Severity = Props.effectSeverity;
                        pawn.health.AddHediff(hediff);

                        Messages.Message($"{pawn.LabelShort} looks confused and disoriented.",
                            pawn, MessageTypeDefOf.NeutralEvent);
                        Log.Message($"[Dream Extraction] Added confusion hediff to {pawn.LabelShort}");
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"AnimeArsenal: Failed to add memory extraction hediff to {pawn.LabelShort}: {ex}");
                }
            }
        }

        private void ApplyMemoryImplantation(Pawn pawn)
        {
            int implantedCount = 0;

            
            if (pawn.needs?.mood?.thoughts?.memories != null && Props.implantedMemoryThoughts != null)
            {
                foreach (var thoughtDef in Props.implantedMemoryThoughts)
                {
                    if (thoughtDef != null)
                    {
                        try
                        {
                            Thought_Memory thought = (Thought_Memory)ThoughtMaker.MakeThought(thoughtDef);
                            if (thought != null)
                            {
                                pawn.needs.mood.thoughts.memories.TryGainMemory(thought);
                                implantedCount++;
                                Log.Message($"[Memory Implant] Added thought {thoughtDef.defName} to {pawn.LabelShort}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Error($"AnimeArsenal: Failed to add implanted memory thought {thoughtDef.defName} to {pawn.LabelShort}: {ex}");
                        }
                    }
                }
            }

            
            if (implantedCount > 0)
            {
                Messages.Message($"Implanted {implantedCount} false memories into {pawn.LabelShort}'s mind.",
                    pawn, MessageTypeDefOf.NeutralEvent);
            }
            else
            {
                Messages.Message($"Failed to implant memories into {pawn.LabelShort}'s mind.",
                    pawn, MessageTypeDefOf.RejectInput);
                Log.Warning($"[Memory Implant] No memories were successfully implanted into {pawn.LabelShort}");
            }

            
            if (Props.memoryImplantHediff != null)
            {
                try
                {
                    
                    var existing = pawn.health.hediffSet.GetFirstHediffOfDef(Props.memoryImplantHediff);
                    if (existing != null)
                    {
                        pawn.health.RemoveHediff(existing);
                    }

                    Hediff hediff = HediffMaker.MakeHediff(Props.memoryImplantHediff, pawn);
                    if (hediff != null)
                    {
                        hediff.Severity = Props.effectSeverity;
                        pawn.health.AddHediff(hediff);

                        Messages.Message($"{pawn.LabelShort} appears disoriented from the false memories.",
                            pawn, MessageTypeDefOf.NeutralEvent);
                        Log.Message($"[Memory Implant] Added disorientation hediff to {pawn.LabelShort}");
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"AnimeArsenal: Failed to add memory implant hediff to {pawn.LabelShort}: {ex}");
                }
            }
        }

        private void ApplyMoodManipulation(Pawn pawn)
        {
            if (pawn.needs?.mood?.thoughts?.memories == null)
            {
                Log.Warning($"[Mood Control] {pawn.LabelShort} has no mood/thoughts system");
                return;
            }

            
            bool applyPositive = Props.moodChange >= 0;

          

            ThoughtDef thoughtToUse = applyPositive ? Props.positiveMoodThought : Props.negativeMoodThought;
            string moodType = applyPositive ? "positive" : "negative";

            if (thoughtToUse != null)
            {
                try
                {
                    Thought_Memory thought = (Thought_Memory)ThoughtMaker.MakeThought(thoughtToUse);
                    if (thought != null)
                    {
                        pawn.needs.mood.thoughts.memories.TryGainMemory(thought);

                        Messages.Message($"Manipulated {pawn.LabelShort}'s mood through dreams ({moodType} effect).",
                            pawn, applyPositive ? MessageTypeDefOf.PositiveEvent : MessageTypeDefOf.NegativeEvent);
                        Log.Message($"[Mood Control] Applied {moodType} mood manipulation to {pawn.LabelShort}");

                        
                        if (pawn.needs.mood != null)
                        {
                            float moodLevel = pawn.needs.mood.CurLevel;
                            Log.Message($"[Mood Control] {pawn.LabelShort}'s mood level is now {moodLevel:F2}");
                        }
                    }
                    else
                    {
                        Log.Warning($"[Mood Control] Failed to create thought from {thoughtToUse.defName}");
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"AnimeArsenal: Failed to add mood manipulation thought to {pawn.LabelShort}: {ex}");
                }
            }
            else
            {
                Log.Warning($"[Mood Control] No {moodType} mood thought defined in properties");
                Messages.Message($"Mood manipulation failed - no {moodType} effect configured.",
                    pawn, MessageTypeDefOf.RejectInput);
            }
        }

        private void ApplySkillTransfer(Pawn pawn)
        {
            if (pawn.skills == null) return;

            Pawn caster = parent.pawn;
            if (caster?.skills == null) return;

            
            var transferableSkills = new List<SkillRecord>();

            
            foreach (var skillRecord in pawn.skills.skills)
            {
                if (skillRecord != null && skillRecord.Level > 0 && !skillRecord.TotallyDisabled)
                {
                    transferableSkills.Add(skillRecord);
                }
            }

            
            if (!transferableSkills.Any())
            {
                Messages.Message($"{pawn.LabelShort} has no skills that can be drained!",
                    caster, MessageTypeDefOf.RejectInput);
                Log.Warning($"[Skill Transfer] {pawn.LabelShort} has no transferable skills");
                return;
            }

            
            var randomSkillRecord = transferableSkills.RandomElement();
            var randomSkill = randomSkillRecord.def;

            
            int transferAmount = Rand.RangeInclusive(1, 3);

            var targetSkill = pawn.skills.GetSkill(randomSkill);
            var casterSkill = caster.skills.GetSkill(randomSkill);

            if (targetSkill != null && casterSkill != null)
            {
                
                transferAmount = Math.Min(transferAmount, targetSkill.Level);

                if (Props.transferFromTarget)
                {
                    
                    int oldTargetLevel = targetSkill.Level;
                    int oldCasterLevel = casterSkill.Level;

                    
                    if (casterSkill.TotallyDisabled)
                    {
                        Messages.Message($"Cannot transfer {randomSkill.LabelCap} - {caster.LabelShort} is incapable of this skill!",
                            caster, MessageTypeDefOf.RejectInput);
                        return;
                    }

                    targetSkill.Level = Math.Max(0, targetSkill.Level - transferAmount);
                    casterSkill.Level = Math.Min(20, casterSkill.Level + transferAmount);

                    Messages.Message($"Drained {transferAmount} levels of {randomSkill.LabelCap} from {pawn.LabelShort}! ({oldTargetLevel}→{targetSkill.Level}) {caster.LabelShort} gained: ({oldCasterLevel}→{casterSkill.Level})",
                        caster, MessageTypeDefOf.PositiveEvent);

                    Log.Message($"[Skill Transfer] Transferred {transferAmount} {randomSkill.LabelCap} from {pawn.LabelShort} (now {targetSkill.Level}) to {caster.LabelShort} (now {casterSkill.Level})");
                }
                else
                {
                    
                    if (casterSkill.Level <= 0)
                    {
                        Messages.Message($"{caster.LabelShort} has no {randomSkill.LabelCap} skill to transfer!",
                            caster, MessageTypeDefOf.RejectInput);
                        return;
                    }

                    int oldTargetLevel = targetSkill.Level;
                    int oldCasterLevel = casterSkill.Level;

                    transferAmount = Math.Min(transferAmount, casterSkill.Level);

                    casterSkill.Level = Math.Max(0, casterSkill.Level - transferAmount);
                    targetSkill.Level = Math.Min(20, targetSkill.Level + transferAmount);

                    Messages.Message($"Transferred {transferAmount} levels of {randomSkill.LabelCap} to {pawn.LabelShort}! ({oldCasterLevel}→{casterSkill.Level}) Target gained: ({oldTargetLevel}→{targetSkill.Level})",
                        caster, MessageTypeDefOf.PositiveEvent);
                }

                
                string[] flavorTexts = {
                    $"Knowledge of {randomSkill.LabelCap} flows through the dream connection...",
                    $"Memories of {randomSkill.LabelCap} training are absorbed...",
                    $"Experience with {randomSkill.LabelCap} transfers through the ethereal link...",
                    $"The essence of {randomSkill.LabelCap} mastery shifts between minds..."
                };

                Messages.Message(flavorTexts.RandomElement(), caster, MessageTypeDefOf.NeutralEvent);
            }
        }

        private void AddEffects(Pawn pawn)
        {
           
            if (Props.effectSound != null)
            {
                Props.effectSound.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
            }

            
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
                    Messages.Message($"{pawn.LabelShort} must be asleep for dream manipulation", MessageTypeDefOf.RejectInput);
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