using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace AnimeArsenal
{
    public class CompProperties_DemonSlayerWeapon : CompProperties
    {
        public List<string> breathingStyleGenes = new List<string>();

        public bool randomGene = true;

        public CompProperties_DemonSlayerWeapon()
        {
            compClass = typeof(CompDemonSlayerWeapon);
        }
    }

    public class CompDemonSlayerWeapon : ThingComp
    {
        private bool geneGranted = false;

        public CompProperties_DemonSlayerWeapon Props => (CompProperties_DemonSlayerWeapon)props;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref geneGranted, "geneGranted", defaultValue: false);
        }

        public override void Notify_Equipped(Pawn pawn)
        {
            base.Notify_Equipped(pawn);

            if (!geneGranted && pawn != null)
            {
                TryGrantBreathingStyle(pawn);
            }
        }

        public void CheckOnWeaponUse(Pawn pawn)
        {
            if (!geneGranted && pawn != null)
            {
                TryGrantBreathingStyle(pawn);
            }
        }

        private void TryGrantBreathingStyle(Pawn pawn)
        {
            if (pawn.genes == null)
            {
                return;
            }

            if (Props.breathingStyleGenes.NullOrEmpty())
            {
                Log.Error("DemonSlayer mod: No breathing style genes defined for " + parent.LabelCap);
                return;
            }

            string geneDefName;

            if (Props.randomGene)
            {
                geneDefName = Props.breathingStyleGenes.RandomElement();
            }
            else
            {
                geneDefName = Props.breathingStyleGenes[0];
            }

            GeneDef geneDef = DefDatabase<GeneDef>.GetNamedSilentFail(geneDefName);

            if (geneDef == null)
            {
                Log.Error("DemonSlayer mod: Could not find gene def with name " + geneDefName);
                return;
            }

            bool alreadyHasBreathingStyle = false;

            foreach (string geneName in Props.breathingStyleGenes)
            {
                GeneDef existingGeneDef = DefDatabase<GeneDef>.GetNamedSilentFail(geneName);
                if (existingGeneDef != null && pawn.genes.HasActiveGene(existingGeneDef))
                {
                    alreadyHasBreathingStyle = true;
                    break;
                }
            }

            if (!alreadyHasBreathingStyle)
            {
                Gene gene = pawn.genes.AddGene(geneDef, true);

                if (gene != null)
                {
                    geneGranted = true;

                    Messages.Message(pawn.LabelShort + " has awakened the " + geneDef.label + " breathing style!",
                        MessageTypeDefOf.PositiveEvent);

                }
            }
        }

        public override string CompInspectStringExtra()
        {
            if (geneGranted)
            {
                return "Breathing style already awakened.";
            }

            return "Breathing style not yet awakened.";
        }
    }
}