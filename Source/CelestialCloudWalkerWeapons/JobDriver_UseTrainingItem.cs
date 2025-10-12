using System.Collections.Generic;
using Verse;
using Verse.AI;
using RimWorld;
using UnityEngine;

namespace AnimeArsenal
{
    public class JobDriver_UseTrainingItem : JobDriver
    {
        private Building_TrainingItem trainingItem;
        private Building_TrainingItem TrainingItem
        {
            get
            {
                if (trainingItem == null)
                {
                    trainingItem = (Building_TrainingItem)job.targetA.Thing;
                }
                return trainingItem;
            }
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);

            Toil train = new Toil();
            train.defaultCompleteMode = ToilCompleteMode.Delay;

            TrainingItemExtension ext = TrainingItem?.Extension;
            if (ext != null)
            {
                train.defaultDuration = ext.trainingTime;
                Log.Message($"Training duration set to: {ext.trainingTime} ticks ({ext.trainingTime / 2500f} hours)");
            }
            else
            {
                train.defaultDuration = 60000;
                Log.Error("Extension is null, using 60000 tick fallback");
            }

            train.tickAction = delegate
            {
                if (pawn != null && job.targetA.Thing != null)
                {
                    pawn.rotationTracker.FaceTarget(job.targetA);
                }
            };

            train.WithProgressBarToilDelay(TargetIndex.A);
            train.PlaySustainerOrSound(() => SoundDefOf.Interact_CleanFilth);
            yield return train;

            Toil applyResults = new Toil();
            applyResults.initAction = delegate { ApplyTrainingResults(); };
            applyResults.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return applyResults;
        }

        private void ApplyTrainingResults()
        {
            TrainingItemExtension ext = TrainingItem?.Extension;
            if (ext == null)
            {
                Log.Error("Extension is null in ApplyTrainingResults!");
                return;
            }

            if (pawn == null || pawn.Destroyed || pawn.Map == null)
            {
                Log.Warning("Pawn is null or destroyed during training completion");
                return;
            }

            TrainingComp comp = pawn.GetComp<TrainingComp>();
            if (comp == null)
            {
                Log.Error($"Pawn {pawn.LabelShort} missing TrainingComp!");
                return;
            }

            bool success = Rand.Chance(ext.successChance);

            if (success)
            {
                if (!ext.capacityToBoost.NullOrEmpty())
                {
                    comp.AddCapacityBoost(ext.capacityToBoost, ext.increaseAmount);
                }
                else if (!ext.statToBoost.NullOrEmpty())
                {
                    comp.AddStatBoost(ext.statToBoost, ext.increaseAmount);
                }

                if (TrainingItem != null)
                {
                    comp.SetCooldown(TrainingItem.def.defName, ext.cooldownTime);
                }

                Messages.Message(
                    $"{pawn.LabelShort} successfully trained and gained permanent bonuses!",
                    pawn,
                    MessageTypeDefOf.PositiveEvent
                );

                if (pawn.Spawned && pawn.Map != null)
                {
                    MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, "Success!", Color.green, 3.5f);
                }
            }
            else
            {
                DamageBodyParts(ext);

                if (TrainingItem != null)
                {
                    comp.SetCooldown(TrainingItem.def.defName, ext.cooldownTime);
                }

                if (pawn != null && !pawn.Destroyed)
                {
                    Messages.Message(
                        $"{pawn.LabelShort} failed training and was injured!",
                        pawn,
                        MessageTypeDefOf.NegativeEvent
                    );

                    if (pawn.Spawned && pawn.Map != null)
                    {
                        MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, "Failed!", Color.red, 3.5f);
                    }
                }
                else
                {
                    Messages.Message(
                        "Training failure was fatal!",
                        MessageTypeDefOf.NegativeEvent
                    );
                }
            }

            if (pawn != null && !pawn.Destroyed && pawn.health != null)
            {
                pawn.health.capacities.Notify_CapacityLevelsDirty();
            }
        }

        private void DamageBodyParts(TrainingItemExtension ext)
        {
            if (ext.failureBodyParts == null || ext.failureBodyParts.Count == 0)
            {
                pawn.TakeDamage(new DamageInfo(DamageDefOf.Blunt, ext.failureDamage));
                return;
            }

            string bodyPartName = ext.failureBodyParts.RandomElement();
            BodyPartDef bodyPartDef = DefDatabase<BodyPartDef>.GetNamedSilentFail(bodyPartName);

            if (bodyPartDef == null)
            {
                pawn.TakeDamage(new DamageInfo(DamageDefOf.Blunt, ext.failureDamage));
                return;
            }

            BodyPartRecord bodyPart = pawn.health.hediffSet.GetNotMissingParts().FirstOrFallback(
                x => x.def == bodyPartDef
            );

            if (bodyPart != null)
            {
                DamageInfo dinfo = new DamageInfo(
                    DamageDefOf.Blunt,
                    ext.failureDamage,
                    0f,
                    -1f,
                    null,
                    bodyPart
                );
                pawn.TakeDamage(dinfo);
            }
            else
            {
                pawn.TakeDamage(new DamageInfo(DamageDefOf.Blunt, ext.failureDamage));
            }
        }
    }
}