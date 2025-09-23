using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace AnimeArsenal
{
    public class CompProperties_RangeToggle : CompProperties
    {
        public CompProperties_RangeToggle()
        {
            compClass = typeof(CompRangeToggle);
        }
    }

    public class CompRangeToggle : ThingComp
    {
        private bool isExtended = false;
        private const float EXTENDED_RANGE = 500f;
        private const float NORMAL_RANGE = 1.5f;

        public override void Notify_Equipped(Pawn pawn)
        {
            var verb = GetExtendedVerb();
            verb?.SetRange(EXTENDED_RANGE);
        }

        public override void PostExposeData()
        {
            Scribe_Values.Look(ref isExtended, "extendedRange", false);
        }

        public override IEnumerable<Gizmo> CompGetWornGizmosExtra()
        {
            yield return CreateToggleButton();
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            yield return CreateToggleButton();
        }

        private Verb_ExtendedMeleeAttack GetExtendedVerb()
        {
            return parent.GetComp<CompEquippable>()?.AllVerbs
                .OfType<Verb_ExtendedMeleeAttack>()
                .FirstOrDefault();
        }

        private Command_Action CreateToggleButton()
        {
            return new Command_Action
            {
                defaultLabel = "Extended Range",
                defaultDesc = "Toggle extended range attacks",
                icon = ContentFinder<Texture2D>.Get("UI/Commands/Attack"),
                action = ToggleRange
            };
        }

        private void ToggleRange()
        {
            isExtended = !isExtended;
            var verb = GetExtendedVerb();
            if (verb != null)
            {
                verb.SetRange(isExtended ? EXTENDED_RANGE : NORMAL_RANGE);
            }
        }
    }
}