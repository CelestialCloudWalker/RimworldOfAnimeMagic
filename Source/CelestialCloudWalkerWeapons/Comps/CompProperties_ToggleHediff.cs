using RimWorld;
using Verse;

namespace AnimeArsenal
{
    public class CompProperties_ToggleHediff : CompProperties_AbilityEffect
    {
        public HediffDef hediffDef;

        public CompProperties_ToggleHediff()
        {
            compClass = typeof(CompAbilityEffect_ToggleHediff);
        }
    }

    public class CompAbilityEffect_ToggleHediff : CompAbilityEffect_Toggleable
    {
        public new CompProperties_ToggleHediff Props => (CompProperties_ToggleHediff)props;

        public override bool CanStart()
        {
            return true;
        }

        public override void OnToggleOff()
        {
            Hediff existingHediff = this.parent.pawn.health.hediffSet.GetFirstHediffOfDef(Props.hediffDef);
            Pawn pawn = this.parent.pawn;
            if (existingHediff != null)
            {
                pawn.health.RemoveHediff(existingHediff);
                Messages.Message("Removed " + Props.hediffDef.label + " from " + pawn.Label,
                    MessageTypeDefOf.NeutralEvent);
            }
        }

        public override void OnToggleOn()
        {
            Pawn pawn = this.parent.pawn;
            Hediff hediff = HediffMaker.MakeHediff(Props.hediffDef, pawn);
            pawn.health.AddHediff(hediff);
            Messages.Message("Added " + Props.hediffDef.label + " to " + pawn.Label,
                MessageTypeDefOf.NeutralEvent);
        }
    }
}