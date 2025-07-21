using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace AnimeArsenal
{
    public class IncidentWorker_RaidNightOnly : IncidentWorker_RaidEnemy
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            if (!base.CanFireNowSub(parms))
                return false;

            // More robust faction checking
            if (parms.faction == null || parms.faction.def.defName != "AA_Twelve_Demon_Moons")
                return base.CanFireNowSub(parms);

            Map map = (Map)parms.target;
            if (map?.skyManager == null)
                return false;

            float skyGlow = map.skyManager.CurSkyGlow;
            return skyGlow < 0.3f;
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            // Double-check time restriction even in dev mode
            if (parms.faction?.def.defName == "AA_Twelve_Demon_Moons")
            {
                Map map = (Map)parms.target;
                if (map?.skyManager == null)
                {
                    Log.Warning("Demon raid blocked - no sky manager");
                    return false;
                }

                float skyGlow = map.skyManager.CurSkyGlow;

                if (skyGlow >= 0.3f)
                {
                    Log.Message("Demon raid blocked - too bright outside (skyGlow: " + skyGlow + ")");
                    return false;
                }
            }

            bool result = base.TryExecuteWorker(parms);

            if (result && parms.faction?.def.defName == "AA_Twelve_Demon_Moons")
            {
                Map map = (Map)parms.target;
                Lord raidLord = map.lordManager.lords.LastOrDefault(l => l.faction == parms.faction);

                if (raidLord != null)
                {

                    LordJob_AssaultColonyNightRaid nightRaidJob = new LordJob_AssaultColonyNightRaid(
                        parms.faction,
                        true,
                        true,
                        false,
                        false,
                        true
                    );

                    raidLord.SetJob(nightRaidJob);
                }
            }

            return result;
        }
    }




    public class LordJob_AssaultColonyNightRaid : LordJob_AssaultColony
    {
        private int lastLightCheck = -1;
        private bool hasRetreated = false;

        public LordJob_AssaultColonyNightRaid() { }

        public LordJob_AssaultColonyNightRaid(Faction faction, bool canKidnap, bool canTimeoutOrFlee, bool sappers, bool useAvoidGridSmart, bool canSteal)
            : base(faction, canKidnap, canTimeoutOrFlee, sappers, useAvoidGridSmart, canSteal) { }

        public override void LordJobTick()
        {
            base.LordJobTick();

            if (hasRetreated) return;

            if (Find.TickManager.TicksGame - lastLightCheck > 60)
            {
                lastLightCheck = Find.TickManager.TicksGame;
                CheckForSunrise();
            }
        }

        private void CheckForSunrise()
        {
            if (lord?.Map == null) return;

            float skyGlow = lord.Map.skyManager.CurSkyGlow;

            if (skyGlow > 0.5f)
            {
                hasRetreated = true;
                lord.ReceiveMemo("SunUp");
            }
        }

        public override StateGraph CreateGraph()
        {
            StateGraph stateGraph = base.CreateGraph();

            LordToil_SunriseRetreat sunriseRetreat = new LordToil_SunriseRetreat();
            stateGraph.AddToil(sunriseRetreat);

            foreach (LordToil toil in stateGraph.lordToils)
            {
                if (toil != sunriseRetreat)
                {
                    Transition transition = new Transition(toil, sunriseRetreat);
                    transition.AddTrigger(new Trigger_Memo("SunUp"));
                    stateGraph.AddTransition(transition);
                }
            }

            return stateGraph;
        }
    }

    public class LordToil_SunriseRetreat : LordToil_ExitMap
    {
        public override void Init()
        {
            base.Init();

            Messages.Message("The Demons flee as the sun rises!", MessageTypeDefOf.NeutralEvent);
        }

        public override void UpdateAllDuties()
        {
            for (int i = 0; i < lord.ownedPawns.Count; i++)
            {
                Pawn pawn = lord.ownedPawns[i];
                pawn.mindState.duty = new PawnDuty(DutyDefOf.ExitMapBest);
            }
        }
    }
}