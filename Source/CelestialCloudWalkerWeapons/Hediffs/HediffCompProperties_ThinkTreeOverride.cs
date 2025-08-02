using Verse;

namespace AnimeArsenal
{
    public class HediffCompProperties_ThinkTreeOverride : HediffCompProperties
    {
        public ThinkTreeDef overrideThinkTreeDef;

        public HediffCompProperties_ThinkTreeOverride()
        {
            compClass = typeof(HediffComp_ThinkTreeOverride);
        }
    }

    public class HediffComp_ThinkTreeOverride : HediffComp
    {
        public ThinkTreeDef overrideThinkTreeDef;

        HediffCompProperties_ThinkTreeOverride Props => (HediffCompProperties_ThinkTreeOverride)props;

        public override void CompExposeData()
        {
            Scribe_Defs.Look(ref overrideThinkTreeDef, "overrideThinkTreeDef");
        }

        public ThinkTreeDef GetTreeOverride()
        {
            return overrideThinkTreeDef;
        }
    }
}

