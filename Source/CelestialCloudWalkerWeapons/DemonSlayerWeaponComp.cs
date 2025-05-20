using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace AnimeArsenal
{
    // CompProperties class for our weapon component
    public class CompProperties_DemonSlayerWeapon : CompProperties
    {
        // List of gene def names that can be granted
        public List<string> breathingStyleGenes = new List<string>();

        // Whether the gene should be random or if the weapon is tied to a specific style
        public bool randomGene = true;

        // Constructor
        public CompProperties_DemonSlayerWeapon()
        {
            compClass = typeof(CompDemonSlayerWeapon);
        }
    }

    // The actual component that will be attached to weapons
    public class CompDemonSlayerWeapon : ThingComp
    {
        // Track if we've already granted a gene
        private bool geneGranted = false;

        // Reference to our properties
        public CompProperties_DemonSlayerWeapon Props => (CompProperties_DemonSlayerWeapon)props;

        // Save/load our state
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref geneGranted, "geneGranted", defaultValue: false);
        }

        // Called when a pawn equips the item
        public override void Notify_Equipped(Pawn pawn)
        {
            base.Notify_Equipped(pawn);

            // Check if we've already granted a gene to anyone
            if (!geneGranted && pawn != null)
            {
                TryGrantBreathingStyle(pawn);
            }
        }

        // For good measure, also check on first use if it wasn't caught by equip
        // We'll use a harmony patch to catch weapon use since ThingComp doesn't have a direct "used weapon" notification
        public void CheckOnWeaponUse(Pawn pawn)
        {
            if (!geneGranted && pawn != null)
            {
                TryGrantBreathingStyle(pawn);
            }
        }

        // Method to grant a breathing style gene
        private void TryGrantBreathingStyle(Pawn pawn)
        {
            // Make sure the pawn has a gene tracker (i.e., is a humanlike creature)
            if (pawn.genes == null)
            {
                return;
            }

            // Check if we have any genes defined
            if (Props.breathingStyleGenes.NullOrEmpty())
            {
                Log.Error("DemonSlayer mod: No breathing style genes defined for " + parent.LabelCap);
                return;
            }

            // Get the gene def to grant
            string geneDefName;

            if (Props.randomGene)
            {
                // Pick a random gene from the list
                geneDefName = Props.breathingStyleGenes.RandomElement();
            }
            else
            {
                // Use the first gene in the list (for weapon-specific breathing styles)
                geneDefName = Props.breathingStyleGenes[0];
            }

            GeneDef geneDef = DefDatabase<GeneDef>.GetNamedSilentFail(geneDefName);

            if (geneDef == null)
            {
                Log.Error("DemonSlayer mod: Could not find gene def with name " + geneDefName);
                return;
            }

            // Check if the pawn already has this gene or any other breathing style gene
            bool alreadyHasBreathingStyle = false;

            foreach (string geneName in Props.breathingStyleGenes)
            {
                GeneDef existingGeneDef = DefDatabase<GeneDef>.GetNamedSilentFail(geneName);
                if (existingGeneDef != null && pawn.genes.HasGene(existingGeneDef))
                {
                    alreadyHasBreathingStyle = true;
                    break;
                }
            }

            if (!alreadyHasBreathingStyle)
            {
                // Add the gene to the pawn
                Gene gene = pawn.genes.AddGene(geneDef, true);

                if (gene != null)
                {
                    // Mark as granted
                    geneGranted = true;

                    // Show a message to the player
                    Messages.Message(pawn.LabelShort + " has awakened the " + geneDef.label + " breathing style!",
                        MessageTypeDefOf.PositiveEvent);

                    // Optional: play a sound or visual effect here
                    // EffecterDefOf.YourCustomEffecter.Spawn(pawn.Position, pawn.Map);
                }
            }
        }

        // Provide tooltip information
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