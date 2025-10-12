using RimWorld;
using RimWorld.QuestGen;
using Verse;

namespace AnimeArsenal
{
    public class CompProperties_UseEffect_StartQuest : CompProperties_UseEffect
    {
        public QuestScriptDef questScriptDef;
        public string successMessage = "{0} has started a quest.";
        public string failMessage = "Cannot start quest at this time.";

        public CompProperties_UseEffect_StartQuest()
        {
            compClass = typeof(CompUseEffect_StartQuest);
        }
    }

    public class CompUseEffect_StartQuest : CompUseEffect
    {
        public CompProperties_UseEffect_StartQuest Props => (CompProperties_UseEffect_StartQuest)props;

        public override void DoEffect(Pawn usedBy)
        {
            base.DoEffect(usedBy);

            if (Props.questScriptDef == null)
            {
                Log.Error("CompUseEffect_StartQuest: questScriptDef is null");
                return;
            }

            Map map = usedBy.Map;
            if (map == null)
            {
                Messages.Message(Props.failMessage, MessageTypeDefOf.RejectInput);
                return;
            }

            int freeColonists = map.mapPawns.FreeColonistsSpawnedCount;
            if (freeColonists < 3)
            {
                Messages.Message("You need at least 3 free colonists to participate in the Final Selection.",
                    MessageTypeDefOf.RejectInput);
                return;
            }

            Slate slate = new Slate();
            slate.Set("map", map);
            slate.Set("points", StorytellerUtility.DefaultThreatPointsNow(map));

            Quest quest = QuestUtility.GenerateQuestAndMakeAvailable(Props.questScriptDef, slate);

            if (quest != null)
            {
                quest.Accept(usedBy);

                string message = string.Format(Props.successMessage, usedBy.LabelShort);
                Messages.Message(message, usedBy, MessageTypeDefOf.PositiveEvent);
            }
            else
            {
                Messages.Message(Props.failMessage, MessageTypeDefOf.RejectInput);
            }
        }
    }
}