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


    public class CompAbilityEffect_ToggleHediff : CompAbilityEffect
    {
        public new CompProperties_ToggleHediff Props => (CompProperties_ToggleHediff)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            Pawn pawn = this.parent.pawn;
            Hediff existingHediff = this.parent.pawn.health.hediffSet.GetFirstHediffOfDef(Props.hediffDef);

            if (existingHediff != null)
            {
                pawn.health.RemoveHediff(existingHediff);
                Messages.Message("Removed " + Props.hediffDef.label + " from " + pawn.Label,
                    MessageTypeDefOf.NeutralEvent);
            }
            else
            {
                Hediff hediff = HediffMaker.MakeHediff(Props.hediffDef, pawn);
                pawn.health.AddHediff(hediff);
                Messages.Message("Added " + Props.hediffDef.label + " to " + pawn.Label,
                    MessageTypeDefOf.NeutralEvent);
            }
        }
    }
}
