using RimWorld;
using System.Linq;
using Verse;
using System;
using System.Collections.Generic;

namespace AnimeArsenal
{
    public class IngestionOutcomeDoer_BlueSpiderLily : IngestionOutcomeDoer
    {
        protected override void DoIngestionOutcomeSpecial(Pawn pawn, Thing ingested, int ingestedCount)
        {
            if (pawn == null || pawn.Dead)
            {
                return;
            }

            bool isDemon = IsDemon(pawn);

            if (isDemon)
            {
                CureDemon(pawn);
            }
            else
            {
                TransformIntoDemon(pawn);
            }
        }

        private bool IsDemon(Pawn pawn)
        {
            if (pawn?.genes == null) return false;

            return pawn.genes.GenesListForReading.Any(g =>
                g.Active && g.def is BloodDemonArtsGeneDef);
        }

        private void CureDemon(Pawn pawn)
        {
            GeneDef cureGene = DefDatabase<GeneDef>.GetNamedSilentFail("BlueSpiderLilyCureGene");

            if (cureGene != null)
            {
                if (pawn.genes.HasActiveGene(cureGene))
                {
                    Messages.Message(
                        $"{pawn.Name.ToStringShort} has already been cured of sunlight weakness!",
                        pawn,
                        MessageTypeDefOf.RejectInput
                    );
                    return;
                }

                pawn.genes.AddGene(cureGene, xenogene: true);

                Messages.Message(
                    $"{pawn.Name.ToStringShort} has been cured! They can now walk in sunlight!",
                    pawn,
                    MessageTypeDefOf.PositiveEvent
                );

                FleckMaker.ThrowLightningGlow(pawn.DrawPos, pawn.Map, 2f);
                MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, "Sunlight Cured!", 3f);
            }
        }

        private void TransformIntoDemon(Pawn pawn)
        {
            GeneDef demonGene = DefDatabase<GeneDef>.GetNamedSilentFail("BloodDemonArt");

            if (demonGene == null)
            {
                Log.Error("[AnimeArsenal] BloodDemonArt gene not found!");
                return;
            }

            if (pawn.genes.HasActiveGene(demonGene))
            {
                Messages.Message(
                    $"{pawn.Name.ToStringShort} is already a demon!",
                    pawn,
                    MessageTypeDefOf.RejectInput
                );
                return;
            }

            pawn.genes.AddGene(demonGene, xenogene: true);

            Messages.Message(
                $"{pawn.Name.ToStringShort} has transformed into a demon! They are now vulnerable to sunlight but possess incredible regeneration.",
                pawn,
                MessageTypeDefOf.NegativeEvent
            );

            FleckMaker.ThrowLightningGlow(pawn.DrawPos, pawn.Map, 3f);
            FleckMaker.ThrowSmoke(pawn.DrawPos, pawn.Map, 1.5f);
            MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, "Demon Transformation!", 3f);

            Hediff stunHediff = HediffMaker.MakeHediff(HediffDefOf.PsychicShock, pawn);
            stunHediff.Severity = 0.5f;
            pawn.health.AddHediff(stunHediff);
        }
    }
}