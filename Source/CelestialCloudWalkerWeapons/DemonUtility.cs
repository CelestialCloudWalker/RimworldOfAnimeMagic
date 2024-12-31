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


        public static void ForceIntoDemonSlave(Pawn Pawn, Pawn Slaver, int SkillLevels = 5, BackstoryDef ChildHoodBackStoryOverWrite = null, BackstoryDef AdultHoodBackStoryOverwrite = null)
        {
            if (Pawn == null)
            {
                return;
            }

            if (Slaver == null)
            {
                return;
            }

            Pawn.story.Childhood = ChildHoodBackStoryOverWrite == null ? CelestialDefof.DemonChildhoodStory : ChildHoodBackStoryOverWrite;
            Pawn.story.Adulthood = AdultHoodBackStoryOverwrite == null ? CelestialDefof.DemonAdulthoodStory : AdultHoodBackStoryOverwrite;


            Pawn.jobs.ClearQueuedJobs(false);
            Pawn.guest.SetGuestStatus(Slaver.Faction, GuestStatus.Slave);

            SetSkillLevels(Pawn, SkillLevels);

            foreach (var item in Pawn.story.traits.allTraits.ToList())
            {
                Pawn.story.traits.RemoveTrait(item);
            }

            SetWorkTypePriority(Pawn, WorkTypeDefOf.Mining, Pawn_WorkSettings.DefaultPriority);
            SetWorkTypePriority(Pawn, WorkTypeDefOf.Growing, Pawn_WorkSettings.DefaultPriority);
            SetWorkTypePriority(Pawn, WorkTypeDefOf.Construction, Pawn_WorkSettings.DefaultPriority);
            SetWorkTypePriority(Pawn, WorkTypeDefOf.Handling, Pawn_WorkSettings.DefaultPriority);
            SetWorkTypePriority(Pawn, WorkTypeDefOf.Crafting, Pawn_WorkSettings.DefaultPriority);
            SetWorkTypePriority(Pawn, WorkTypeDefOf.Hauling, Pawn_WorkSettings.DefaultPriority);
            SetWorkTypePriority(Pawn, WorkTypeDefOf.Cleaning, Pawn_WorkSettings.DefaultPriority);
            SetWorkTypePriority(Pawn, WorkTypeDefOf.PlantCutting, Pawn_WorkSettings.DefaultPriority);
            SetWorkTypePriority(Pawn, WorkTypeDefOf.Smithing, Pawn_WorkSettings.DefaultPriority);

            SetWorkTypePriority(Pawn, WorkTypeDefOf.Childcare, 0);
            SetWorkTypePriority(Pawn, WorkTypeDefOf.Warden, 0);
            SetWorkTypePriority(Pawn, WorkTypeDefOf.Research, 0);
            SetWorkTypePriority(Pawn, WorkTypeDefOf.DarkStudy, 0);
            SetWorkTypePriority(Pawn, WorkTypeDefOf.Firefighter, 0);
            SetWorkTypePriority(Pawn, WorkTypeDefOf.Handling, 0);
            SetWorkTypePriority(Pawn, WorkTypeDefOf.DarkStudy, 0);
            SetWorkTypePriority(Pawn, WorkTypeDefOf.Doctor, 0);
            SetWorkTypePriority(Pawn, WorkTypeDefOf.Hunting, 0);



            if (Slaver.GetLord() != null)
            {
                Slaver.GetLord().AddPawn(Pawn);
            }

            if (Pawn.playerSettings != null)
            {
                // Disable health care
                Pawn.playerSettings.medCare = MedicalCareCategory.NoCare;
            }


            if (Pawn.mindState != null)
            {
                // Modify behavior to focus on work
                Pawn.mindState.mentalStateHandler.Reset();
            }
        }

        public static void SetSkillLevels(Pawn pawn, int Level)
        {
            foreach (SkillRecord skill in pawn.skills.skills)
            {
                skill.Level = Level; // Set to a moderate skill level
                skill.passion = Passion.None;
            }
        }

        public static void SetWorkTypePriority(Pawn pawn, WorkTypeDef workType, int prio = 1)
        {
            if (workType != null)
            {
                int CurrentPrio = pawn.workSettings.GetPriority(workType);
                if (CurrentPrio > 0)
                {
                    pawn.workSettings.SetPriority(workType, prio);
                }
            }
        }
    }



}