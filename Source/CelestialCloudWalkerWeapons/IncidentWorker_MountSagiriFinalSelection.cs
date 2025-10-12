using RimWorld;
using Verse;
using RimWorld.QuestGen;

namespace AnimeArsenal
{
    public class IncidentWorker_MountSagiriFinalSelection : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            return base.CanFireNowSub(parms) &&
                   parms.target is Map map &&
                   map.mapPawns.FreeColonistsSpawnedCount >= 3;
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Slate slate = new Slate();
            slate.Set("points", parms.points);

            Quest quest = QuestUtility.GenerateQuestAndMakeAvailable(
                DefDatabase<QuestScriptDef>.GetNamed("AnimeArsenal_MountSagiriFinalSelection"),
                slate
            );

            if (quest != null)
            {
                Letter letter = LetterMaker.MakeLetter(
                    label: "Final Selection - Mount Sagiri",
                    text: quest.description,
                    def: LetterDefOf.PositiveEvent,
                    lookTargets: null,
                    relatedFaction: null,
                    quest: quest
                );
                Find.LetterStack.ReceiveLetter(letter);
                return true;
            }

            return false;
        }
    }
}