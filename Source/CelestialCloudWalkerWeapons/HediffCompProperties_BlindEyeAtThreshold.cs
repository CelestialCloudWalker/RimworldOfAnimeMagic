using System.Linq;
using Verse;

namespace AnimeArsenal
{
    public class HediffCompProperties_BlindEyeAtThreshold : HediffCompProperties
    {
        public float severityThreshold = 0.95f;
        public HediffDef hediffDef;

        public HediffCompProperties_BlindEyeAtThreshold()
        {
            compClass = typeof(HediffComp_BlindEyeAtThreshold);
        }
    }

    public class HediffComp_BlindEyeAtThreshold : HediffComp
    {
        private bool triggered = false;

        public HediffCompProperties_BlindEyeAtThreshold Props =>
            (HediffCompProperties_BlindEyeAtThreshold)props;

        public override void CompPostTick(ref float severityAdjustment)
        {
            if (triggered) return;
            if (parent.Severity < Props.severityThreshold) return;
            if (Pawn == null || Pawn.Dead) return;

            BodyPartRecord eye = Pawn.health.hediffSet.GetNotMissingParts()
                .Where(p =>
                    p.def.defName == "Eye" &&
                    !Pawn.health.hediffSet.HasHediff(Props.hediffDef, p))
                .RandomElementWithFallback(null);

            if (eye == null) return;

            Pawn.health.AddHediff(Props.hediffDef, eye);
            triggered = true;
        }

        public override void CompExposeData()
        {
            Scribe_Values.Look(ref triggered, "triggered", false);
        }
    }
}