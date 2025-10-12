using RimWorld;
using System;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace AnimeArsenal
{
    public class Building_TrainingItem : Building
    {
        private TrainingItemExtension extension;

        public TrainingItemExtension Extension
        {
            get
            {
                if (extension == null)
                {
                    extension = def.GetModExtension<TrainingItemExtension>();
                }
                return extension;
            }
        }

        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn selPawn)
        {
            foreach (FloatMenuOption opt in base.GetFloatMenuOptions(selPawn))
            {
                yield return opt;
            }

            if (!selPawn.CanReach(this, PathEndMode.InteractionCell, Danger.Some))
            {
                yield return new FloatMenuOption("CannotUseNoPath".Translate(), null);
                yield break;
            }

            if (Extension == null)
            {
                yield return new FloatMenuOption("Training item not configured", null);
                yield break;
            }

            TrainingComp comp = selPawn.GetComp<TrainingComp>();
            if (comp != null && comp.IsOnCooldown(def.defName, out int ticksRemaining))
            {
                string cooldownStr = ticksRemaining.ToStringTicksToPeriod();
                yield return new FloatMenuOption($"Train at {this.Label} (On cooldown: {cooldownStr})", null);
                yield break;
            }

            string label = $"Train at {this.Label}";
            Action action = delegate
            {
                Job job = JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("AnimeArsenal_UseTrainingItem"), this);
                selPawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
            };

            yield return new FloatMenuOption(label, action);
        }
    }
}