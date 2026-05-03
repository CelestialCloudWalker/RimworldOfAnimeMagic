using RimWorld;
using Verse;
using EMF;

namespace AnimeArsenal
{
    public class HediffCompProperties_DrainResource : HediffCompProperties
    {
        public AbilityResourceDef resourceDef;
        public float resourceCostPerInterval = 1f;
        public int intervalTicks = 60;
        public bool removeOnEmpty = true;
        public float severityPerInterval = 0f;

        public HediffCompProperties_DrainResource()
        {
            compClass = typeof(HediffComp_DrainResource);
        }
    }

    public class HediffComp_DrainResource : HediffComp
    {
        public new HediffCompProperties_DrainResource Props =>
            (HediffCompProperties_DrainResource)props;

        private int tickCounter = 0;

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            tickCounter++;
            if (tickCounter < Props.intervalTicks) return;
            tickCounter = 0;

            Gene_BasicResource resourceGene = GetResourceGene();

            if (resourceGene == null)
            {
                if (Props.removeOnEmpty)
                    MarkForRemoval();
                return;
            }

            if (resourceGene.ResourceIsUnavailable(out _))
            {
                if (Props.removeOnEmpty)
                    MarkForRemoval();
                return;
            }

            if (!resourceGene.Has(Props.resourceCostPerInterval))
            {
                if (Props.removeOnEmpty)
                    MarkForRemoval();
                return;
            }

            resourceGene.Consume(Props.resourceCostPerInterval);

            if (Props.severityPerInterval != 0f)
                severityAdjustment -= Props.severityPerInterval;
        }

        public override string CompDescriptionExtra
        {
            get
            {
                if (Props.resourceDef == null) return null;

                Gene_BasicResource gene = GetResourceGene();
                float current = gene?.Value ?? 0f;
                float max = gene?.Max ?? 0f;

                string resourceName = Props.resourceDef.resourceName ?? Props.resourceDef.defName;
                string costLabel = $"{Props.resourceCostPerInterval}/{Props.intervalTicks}t";

                return $"\nDraining {resourceName}: -{Props.resourceCostPerInterval} every {Props.intervalTicks} ticks"
                     + $"\nCurrent {resourceName}: {current:F0} / {max:F0}";
            }
        }

        private Gene_BasicResource GetResourceGene()
        {
            if (Pawn?.genes?.GenesListForReading == null || Props.resourceDef == null)
                return null;
            return Pawn.GetGeneForResourceDef(Props.resourceDef);
        }

        private void MarkForRemoval()
        {
            if (parent.TryGetComp<HediffComp_Disappears>() != null)
            {
                parent.Severity = 0f;
            }
            else
            {
                Pawn.health.RemoveHediff(parent);
            }
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref tickCounter, "drainTickCounter", 0);
        }
    }
}