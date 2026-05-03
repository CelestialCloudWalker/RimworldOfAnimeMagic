using RimWorld;
using System.Collections.Generic;
using Verse;

namespace AnimeArsenal
{
    public class CompProperties_RequiresHediff : CompProperties_AbilityEffect
    {
        public HediffDef requiredHediff;
        public List<HediffDef> requiredHediffs;
        public float minimumSeverity = 0f;
        public string failMessage = "Requires a specific condition to be active.";

        public CompProperties_RequiresHediff()
        {
            compClass = typeof(CompAbilityEffect_RequiresHediff);
        }
    }

    public class CompAbilityEffect_RequiresHediff : CompAbilityEffect
    {
        new CompProperties_RequiresHediff Props =>
            (CompProperties_RequiresHediff)props;

        public override bool CanCast
        {
            get
            {
                Pawn pawn = parent.pawn;
                if (pawn?.health?.hediffSet == null) return false;

                if (Props.requiredHediff != null)
                {
                    Hediff h = pawn.health.hediffSet.GetFirstHediffOfDef(Props.requiredHediff);
                    if (h != null && h.Severity >= Props.minimumSeverity)
                        return true;
                }

                if (!Props.requiredHediffs.NullOrEmpty())
                {
                    foreach (HediffDef def in Props.requiredHediffs)
                    {
                        Hediff h = pawn.health.hediffSet.GetFirstHediffOfDef(def);
                        if (h != null && h.Severity >= Props.minimumSeverity)
                            return true;
                    }
                }

                return false;
            }
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (!CanCast)
            {
                if (throwMessages)
                    Messages.Message(Props.failMessage,
                        MessageTypeDefOf.RejectInput, false);
                return false;
            }
            return base.Valid(target, throwMessages);
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest) { }
    }
}