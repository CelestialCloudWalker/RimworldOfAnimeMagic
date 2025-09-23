using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI.Group;

namespace AnimeArsenal
{
    public static class DemonUtility
    {
        public static List<WorkTypeDef> WantedWorkTypes = new List<WorkTypeDef>()
        {
            WorkTypeDefOf.Mining,
            WorkTypeDefOf.Growing,
            WorkTypeDefOf.Construction,
            WorkTypeDefOf.Hunting,
            WorkTypeDefOf.Hauling,
            WorkTypeDefOf.Cleaning,
            WorkTypeDefOf.PlantCutting,
            WorkTypeDefOf.Smithing
        };

        public static void ForceIntoDemonSlave(Pawn pawn, Pawn slaver, int skillLevels = 5,
            BackstoryDef childhoodOverride = null, BackstoryDef adulthoodOverride = null)
        {
            if (pawn?.story == null || slaver?.Faction == null) return;

            pawn.story.Childhood = childhoodOverride ?? CelestialDefof.DemonChildhoodStory;
            pawn.story.Adulthood = adulthoodOverride ?? CelestialDefof.DemonAdulthoodStory;

            pawn.jobs.ClearQueuedJobs(false);
            pawn.guest.SetGuestStatus(slaver.Faction, GuestStatus.Slave);

            SetSkillLevels(pawn, skillLevels);

            var traits = pawn.story.traits.allTraits.ToList();
            traits.ForEach(t => pawn.story.traits.RemoveTrait(t));

            ConfigureWorkPriorities(pawn);

            slaver.GetLord()?.AddPawn(pawn);

            pawn.playerSettings.medCare = MedicalCareCategory.NoCare;
            pawn.mindState?.mentalStateHandler.Reset();
        }

        private static void ConfigureWorkPriorities(Pawn pawn)
        {
            var enabledWork = new[]
            {
                WorkTypeDefOf.Mining, WorkTypeDefOf.Growing, WorkTypeDefOf.Construction,
                WorkTypeDefOf.Crafting, WorkTypeDefOf.Hauling, WorkTypeDefOf.Cleaning,
                WorkTypeDefOf.PlantCutting, WorkTypeDefOf.Smithing
            };

            var disabledWork = new[]
            {
                WorkTypeDefOf.Childcare, WorkTypeDefOf.Warden, WorkTypeDefOf.Research,
                WorkTypeDefOf.DarkStudy, WorkTypeDefOf.Firefighter, WorkTypeDefOf.Handling,
                WorkTypeDefOf.Doctor, WorkTypeDefOf.Hunting
            };

            foreach (var work in enabledWork)
                SetWorkTypePriority(pawn, work, Pawn_WorkSettings.DefaultPriority);

            foreach (var work in disabledWork)
                SetWorkTypePriority(pawn, work, 0);
        }

        public static void SetSkillLevels(Pawn pawn, int level)
        {
            foreach (var skill in pawn.skills.skills)
            {
                skill.Level = level;
                skill.passion = Passion.None;
            }
        }

        public static void SetWorkTypePriority(Pawn pawn, WorkTypeDef workType, int priority = 1)
        {
            if (workType != null && pawn.workSettings.GetPriority(workType) > 0)
                pawn.workSettings.SetPriority(workType, priority);
        }
    }
}